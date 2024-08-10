using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hallupa.Library.Extensions;
using Hallupa.TraderTools.Basics;
using log4net;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.Extensions;
using TraderTools.Simulation;

namespace Hallupa.TraderTools.Simulation
{
    public class StrategyRunner
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly IBrokersCandlesService _candleService;
        private readonly IBroker _broker;
        private readonly IBrokersService _brokersService;
        private const int LogIntervalSeconds = 5;

        public StrategyRunner(
            IBrokersCandlesService candleService,
            IBroker broker,
            IBrokersService brokersService)
        {
            _candleService = candleService;
            _broker = broker;
            _brokersService = brokersService;
        }

        public Dictionary<string, AssetBalance> InitialAssetBalances { get; private set; }
        public Dictionary<string, AssetBalance> CurrentAssetBalances { get; private set; }

        public decimal CalculateUSDTValue(IBroker broker, DateTime currentTime)
        {
            var value = 0M;

            foreach (var assetBalance in CurrentAssetBalances)
            {
                if (assetBalance.Value.Asset == "USDT")
                {
                    value += assetBalance.Value.Balance;
                    continue;
                }

                var candles = _candleService.GetCandles(broker, $"{assetBalance.Value.Asset}USDT", Timeframe.H1, false, maxCloseTimeUtc: currentTime);
                var candle = candles.Last();
                var assetValue = assetBalance.Value.Balance * (decimal)candle.CloseBid;
                value += assetValue;
            }

            return value;
        }

        public List<Trade> Run(
            StrategyBase strategy,
            Func<bool> getShouldStopFunc = null,
            DateTime? startTime = null,
            DateTime? endTime = null)
        {
            if (strategy.Markets == null || strategy.Markets.Length == 0) throw new ArgumentException("Strategy must set markets");
            if (strategy.Timeframes == null || strategy.Timeframes.Length == 0) throw new ArgumentException("Timesframes must set markets");
            if (strategy.InitialSimulationBalances == null || strategy.InitialSimulationBalances.Length == 0) throw new ArgumentException("Simulation initial asset balances must be set");

            InitialAssetBalances = strategy.InitialSimulationBalances.ToDictionary(z => z.Asset, z => z);

            var updateCandles = true;
            var runCandles = GetRunCandles(strategy, strategy.SimulationGranularity, startTime, endTime, updateCandles);
            var currentCandles = CreateCurrentCandles(strategy.Markets, strategy.Timeframes);
            var strategyCandles = GetTfOrderedStrategyCandles(strategy, startTime, endTime, updateCandles);


            var candleLatestDateTimes = runCandles.Select(c => (c.Market, c.Timeframe, c.Candles.Last().CloseTime())).ToList();
            candleLatestDateTimes.AddRange(strategyCandles.Select(x => (x.Market, x.Timeframe, x.Candles.Last().CloseTime())));
            var latest = candleLatestDateTimes.OrderByDescending(x => x).First();
            var earliest = candleLatestDateTimes.OrderBy(x => x).First();

            var latestCandleDateTime = latest.Item3;
            if (latestCandleDateTime > DateTime.UtcNow) latestCandleDateTime = DateTime.UtcNow;
            if (earliest.Item3 < latestCandleDateTime.AddDays(-1))
                throw new ApplicationException($"{earliest.Market} {earliest.Timeframe} missing recent candles");

            var trades = new TradeWithIndexingCollection();

            CurrentAssetBalances = InitialAssetBalances.ToDictionary(
                x => x.Value.Asset,
                x => new AssetBalance(x.Value.Asset, x.Value.Balance));

            var nextLogTime = DateTime.UtcNow.AddSeconds(LogIntervalSeconds);

            strategy.SetSimulationParameters(trades, currentCandles);
            strategy.SetInitialised(
                false,
                () => CurrentAssetBalances,
                (indexing, trade, candle) => ProcessTradeUpdatedOrAddedByStrategy(indexing, trade, trades, candle, strategy),
                new TradeFactory(),
                _brokersService);

            var currentBidPrices = new Dictionary<string, float>();
            var currentAskPrices = new Dictionary<string, float>();

            strategy.UpdateBalances();
            strategy.Starting();

            long currentTimeBeforeProgress = 0;

            while (true)
            {
                if (getShouldStopFunc != null && getShouldStopFunc()) return null;


                // Progress strategy candles
                var addedCandles = ProgressStrategyCandles(
                    strategyCandles,
                    currentCandles,
                    currentBidPrices,
                    currentAskPrices,
                    out var currentTime,
                    out var nextTime);

                foreach (var t in addedCandles.Select(a => a.Timeframe))
                {
                    long date = 0;
                    foreach (var m in addedCandles.Select(a => a.Market).Distinct())
                    {
                        if (date == 0)
                        {
                            date = currentCandles[m][t][currentCandles[m][t].Count - 1].OpenTimeTicks;
                        }
                    }
                }

                if (addedCandles.Count == 0)
                {
                    // Update profit for open trades
                    if (strategy.BrokerKind == BrokerKind.SpreadBet)
                    {
                        foreach (var t in trades.OpenTrades)
                        {
                            UpdateTradeNetProfitLossForOpenTrade(t.Trade, currentBidPrices[t.Trade.Market],
                                currentAskPrices[t.Trade.Market], strategy);
                        }
                    }
                    else
                    {
                        // To BrokerKind.Trade, each trade results in something being bought so profit doesn't need to be updated based on the 
                        // open trade value
                    }

                    break;
                }

                // Log progress
                if (DateTime.UtcNow > nextLogTime)
                {
                    LogProgress(trades, currentTime);
                    nextLogTime = DateTime.UtcNow.AddSeconds(LogIntervalSeconds);
                }

                if (currentTimeBeforeProgress > currentTime)
                {
                    throw new ApplicationException("StrategyRunner processing time out of order");
                }

                // Update value
                /*CurrentValue = Balance;
                foreach (var t in trades.OpenTrades)
                {
                    var p = t.Trade.TradeDirection == TradeDirection.Long
                        ? currentBidPrices[t.Trade.Market]
                        : currentAskPrices[t.Trade.Market];
                    var v = (decimal)p * t.Trade.EntryQuantity.Value;
                    CurrentValue += v - (v * strategy.Commission);
                }*/

                // Process strategy
                strategy.UpdateIndicators(addedCandles);
                strategy.UpdateBalances();
                strategy.ProcessCandles(addedCandles);

                /*foreach (var market in strategy.Markets)
                {
                    var smallestNonRunCandle = currentCandles[market][smallestNonRunTimeframe][currentCandles[market][smallestNonRunTimeframe].Count - 1];
                    RemoveInvalidTrades(market, strategy.TradesToProcess, smallestNonRunCandle.CloseBid, smallestNonRunCandle.CloseAsk);
                }*/

                if (trades.AnyOpenWithStopOrLimit || trades.AnyOrders)
                {
                    ProgressRunCandles(runCandles, currentTime, nextTime, trades, strategy);
                }

                currentTimeBeforeProgress = currentTime;
            }


            var ret = trades.AllTrades.Select(x => x.Trade).ToList();

            CompleteTradeDetails(ret);

            strategy.SimulationComplete();

            LogProgress(trades);

            Log.Info($"End USDT value: ${GetUsdtValueUseLatestCandles(currentCandles, CurrentAssetBalances):N} " +
                     $"Initial USDT value: ${GetUsdtValueUseOldestCandles(currentCandles, InitialAssetBalances):N}");

            return ret;
        }

        private decimal GetUsdtValueUseLatestCandles(
            Dictionary<string, TimeframeLookup<List<Candle>>> currentCandles, Dictionary<string, AssetBalance> assetBalances)
        {
            var value = 0M;
            foreach (var assetBalance in assetBalances)
            {
                if (assetBalance.Value.Asset == "USDT")
                {
                    value += assetBalance.Value.Balance;
                    continue;
                }

                var candles = currentCandles[$"{assetBalance.Value.Asset}USDT"].First(
                    x => x.Value != null && x.Value.Count > 0);
                var candle = candles.Value[^1];
                var assetValue = assetBalance.Value.Balance * (decimal)candle.CloseBid;
                value += assetValue;
            }

            return value;
        }

        private decimal GetUsdtValueUseOldestCandles(
            Dictionary<string, TimeframeLookup<List<Candle>>> currentCandles, Dictionary<string, AssetBalance> assetBalances)
        {
            var value = 0M;
            foreach (var assetBalance in assetBalances)
            {
                if (assetBalance.Value.Asset == "USDT")
                {
                    value += assetBalance.Value.Balance;
                    continue;
                }

                var candles = currentCandles[$"{assetBalance.Value.Asset}USDT"].First(
                    x => x.Value != null && x.Value.Count > 0);
                var candle = candles.Value[0];
                var assetValue = assetBalance.Value.Balance * (decimal)candle.CloseBid;
                value += assetValue;
            }

            return value;
        }

        private void RemoveInvalidTrades(string market, List<Trade> newTrades, float latestBidPrice, float latestAskPrice)
        {
            // Validate trades
            for (var i = newTrades.Count - 1; i >= 0; i--)
            {
                var t = newTrades[i];
                if (t.Market != market) continue;

                var removed = false;
                if (t.OrderPrice != null)
                {
                    if (t.LimitPrice != null)
                    {
                        if (t.TradeDirection == TradeDirection.Long && t.LimitPrice < t.OrderPrice.Value)
                        {
                            Log.Error(
                                $"Long trade for {t.Market} has limit price below order price. Ignoring trade");
                            newTrades.RemoveAt(i);
                            removed = true;
                        }
                        else if (t.TradeDirection == TradeDirection.Short && t.LimitPrice > t.OrderPrice.Value)
                        {
                            Log.Error(
                                $"Short trade for {t.Market} has limit price above order price. Ignoring trade");
                            newTrades.RemoveAt(i);
                            removed = true;
                        }
                    }

                    if (t.StopPrices != null && !removed)
                    {
                        if (t.TradeDirection == TradeDirection.Long && t.StopPrice > t.OrderPrice.Value)
                        {
                            Log.Error(
                                $"Long trade for {t.Market} has stop price above order price. Ignoring trade");
                            newTrades.RemoveAt(i);
                            removed = true;
                        }
                        else if (t.TradeDirection == TradeDirection.Short && t.StopPrice < t.OrderPrice.Value)
                        {
                            Log.Error(
                                $"Short trade for {t.Market} has stop price below order price. Ignoring trade");
                            newTrades.RemoveAt(i);
                            removed = true;
                        }
                    }

                    if (!removed)
                    {
                        if (t.TradeDirection == TradeDirection.Long && t.OrderType == OrderType.LimitEntry &&
                            t.OrderPrice.Value > (decimal)latestAskPrice)
                        {
                            Log.Error($"Long trade for {t.Market} has limit entry but order price is above latest price. Ignoring trade");
                            newTrades.RemoveAt(i);
                            removed = true;
                        }
                        else if (t.TradeDirection == TradeDirection.Long && t.OrderType == OrderType.StopEntry &&
                                 t.OrderPrice.Value < (decimal)latestAskPrice)
                        {
                            Log.Error($"Long trade for {t.Market} has stop entry but order price is below latest price. Ignoring trade");
                            newTrades.RemoveAt(i);
                            removed = true;
                        }
                        else if (t.TradeDirection == TradeDirection.Short && t.OrderType == OrderType.LimitEntry &&
                                 t.OrderPrice.Value < (decimal)latestBidPrice)
                        {
                            Log.Error($"Short trade for {t.Market} has limit entry but order price is below latest price. Ignoring trade");
                            newTrades.RemoveAt(i);
                            removed = true;
                        }
                        else if (t.TradeDirection == TradeDirection.Short && t.OrderType == OrderType.StopEntry &&
                                 t.OrderPrice.Value > (decimal)latestBidPrice)
                        {
                            Log.Error($"Short trade for {t.Market} has stop entry but order price is above latest price. Ignoring trade");
                            newTrades.RemoveAt(i);
                            removed = true;
                        }
                    }

                    /*var maxPips = 4;
                    if (!removed && t.StopPrice != null)
                    {
                        var stopPips = PipsHelper.GetPriceInPips(Math.Abs(t.StopPrice.Value - t.OrderPrice.Value),
                            marketDetailsService.GetMarketDetails("FXCM", t.Market));
                        if (stopPips <= maxPips)
                        {
                            Log.Error($"Trade for {t.Market} has stop within {maxPips} pips. Ignoring trade");
                            newTrades.RemoveAt(i);
                            removed = true;
                        }
                    }

                    if (!removed && t.LimitPrice != null)
                    {
                        var limitPips = PipsHelper.GetPriceInPips(Math.Abs(t.LimitPrice.Value - t.OrderPrice.Value),
                            marketDetailsService.GetMarketDetails("FXCM", t.Market));
                        if (limitPips <= maxPips)
                        {
                            Log.Error($"Trade for {t.Market} has stop within {maxPips} pips. Ignoring trade");
                            newTrades.RemoveAt(i);
                            removed = true;
                        }
                    }*/
                }
                else
                {
                    // Market trade
                    // decimal latestBidPrice, decimal latestAskPrice,
                    if (t.LimitPrice != null)
                    {
                        if (t.TradeDirection == TradeDirection.Long && t.LimitPrice < t.EntryPrice)
                        {
                            Log.Error(
                                $"Long trade for {t.Market} has limit price below current price. Ignoring trade");
                            newTrades.RemoveAt(i);
                            removed = true;
                        }
                        else if (t.TradeDirection == TradeDirection.Short && t.LimitPrice > t.EntryPrice)
                        {
                            Log.Error(
                                $"Short trade for {t.Market} has limit price above current price. Ignoring trade");
                            newTrades.RemoveAt(i);
                            removed = true;
                        }
                    }

                    if (t.StopPrices != null && !removed)
                    {
                        if (t.TradeDirection == TradeDirection.Long && t.StopPrice > t.EntryPrice)
                        {
                            Log.Error(
                                $"Long trade for {t.Market} has stop price above current price. Ignoring trade");
                            newTrades.RemoveAt(i);
                            removed = true;
                        }
                        else if (t.TradeDirection == TradeDirection.Short && t.StopPrice < t.EntryPrice)
                        {
                            Log.Error(
                                $"Short trade for {t.Market} has stop price below current price. Ignoring trade");
                            newTrades.RemoveAt(i);
                            removed = true;
                        }
                    }
                }
            }
        }

        private void ProcessTradeUpdatedOrAddedByStrategy(
            TradeWithIndexing tradeWithIndexing,
            Trade trade,
            TradeWithIndexingCollection trades,
            Candle candle,
            StrategyBase strategy)
        {
            if (tradeWithIndexing == null)
            {
                // New trade
                if (trade.EntryPrice != null)
                {
                    var tradeUpdater = new TradeAmountUpdater();
                    tradeUpdater.UpdateTradeAndBalance(trade, strategy.Commission, strategy.BrokerKind, CurrentAssetBalances);

                    if (trade.EntryQuantity != null && trade.EntryQuantity >= 0M)
                        trades.AddOpenTrade(trade);
                }
                else if (trade.OrderPrice != null)
                {
                    trades.AddOrderTrade(trade);
                }
            }
            else
            {
                if (trade.ClosePrice != null && trade.EntryPrice != null)
                {
                    // Closed trade
                    UpdateBalanceForClosedTrade(trade, strategy);
                    UpdateTradeNetProfitLossForClosedTrade(trade, strategy);
                    trades.MoveOpenToClose(tradeWithIndexing);
                }
                else if (trade.OrderPrice != null && trade.EntryPrice == null && trade.ClosePrice != null)
                {
                    // Order
                    trades.MoveOrderToClosed(tradeWithIndexing);
                }
            }
        }


        private void ProgressRunCandles(
            List<CandlesWithIndex> runCandles,
            long currentTime,
            long nextTime,
            TradeWithIndexingCollection trades,
            StrategyBase strategy)
        {
            foreach (var c in runCandles)
            {
                var startIndex = c.Candles.BinarySearchGetItem(
                    i => c.Candles[i].CloseTimeTicks,
                    c.NextIndex,
                    currentTime,
                    BinarySearchMethod.NextHigherValue);

                if (startIndex == -1) continue;

                for (var i = startIndex; i < c.Candles.Count; i++)
                {
                    var candle = c.Candles[i];
                    if (candle.CloseTimeTicks > nextTime) break;
                    c.NextIndex = i + 1;

                    // Try to close open trades
                    foreach (var t in trades
                        .OpenTrades
                        .Where(z => z.Trade.Market == c.Market)
                        .ToList())
                    {
                        if (t.Trade.ClosePrice != null)
                        {
                            throw new ApplicationException("Open trades list contains a closed trade");
                        }


                        t.Trade.SimulateTrade(candle, out var updated);

                        if (t.Trade.CloseDateTime != null)
                        {
                            // Processed closed open trade
                            trades.MoveOpenToClose(t);
                            UpdateBalanceForClosedTrade(t.Trade, strategy);
                            UpdateTradeNetProfitLossForClosedTrade(t.Trade, strategy);
                        }
                    }

                    // Try to fill orders
                    foreach (var t in trades
                        .OrderTradesAsc
                        .Where(z => z.Trade.Market == c.Market)
                        .ToList())
                    {
                        if (t.Trade.OrderDateTime != null && candle.CloseTimeTicks < t.Trade.OrderDateTime.Value.Ticks)
                        {
                            continue;
                        }

                        t.Trade.SimulateTrade(candle, out _);

                        if (t.Trade.EntryDateTime != null)
                        {
                            // Process filled order
                            trades.MoveOrderToOpen(t);
                            var tradeUpdater = new TradeAmountUpdater();
                            tradeUpdater.UpdateTradeAndBalance(t.Trade, strategy.Commission, strategy.BrokerKind, CurrentAssetBalances);
                        }
                        else if (t.Trade.CloseDateTime != null)
                        {
                            // Order was closed without filling it
                            trades.MoveOrderToClosed(t);
                        }
                    }
                }
            }
        }

        private void UpdateBalanceForClosedTrade(Trade t, StrategyBase strategy)
        {
            var sellReturn = t.EntryQuantity.Value * t.ClosePrice.Value;
            var fee = sellReturn * strategy.Commission;

            CurrentAssetBalances[t.BaseAsset]
                = new AssetBalance(t.BaseAsset, CurrentAssetBalances[t.BaseAsset].Balance + sellReturn - fee);
        }

        private void UpdateTradeNetProfitLossForClosedTrade(Trade t, StrategyBase strategy)
        {
            var sellReturn = t.EntryQuantity.Value * t.ClosePrice.Value;
            var fee = sellReturn * strategy.Commission;
            var profit = sellReturn - t.RiskAmount.Value - fee;

            t.NetProfitLoss = profit;
        }

        private void UpdateTradeNetProfitLossForOpenTrade(Trade t, float bid, float ask, StrategyBase strategy)
        {
            var price = t.TradeDirection == TradeDirection.Long ? bid : ask;
            var sellReturn = t.EntryQuantity.Value * (decimal)price;
            var fee = sellReturn * strategy.Commission;
            var profit = sellReturn - t.RiskAmount.Value - fee;

            t.NetProfitLoss = profit;
        }

        private Dictionary<string, TimeframeLookup<List<Candle>>> CreateCurrentCandles(string[] markets, Timeframe[] timeframes)
        {
            var ret = new Dictionary<string, TimeframeLookup<List<Candle>>>();

            foreach (var m in markets)
            {
                var lookup = new TimeframeLookup<List<Candle>>();

                foreach (var t in timeframes)
                {
                    lookup.Add(t, new List<Candle>(100000));
                }

                ret.Add(m, lookup);
            }

            return ret;
        }

        private void LogProgress(TradeWithIndexingCollection trades, long? currentDateTimeTicks = null)
        {
            var closedTrades = new List<Trade>(5000);
            closedTrades.AddRange(trades.ClosedTrades.Select(x => x.Trade));

            var orderTrades = new List<Trade>(5000);
            orderTrades.AddRange(trades.OrderTradesAsc.Select(x => x.Trade));

            var tradesForExpectancy = closedTrades.Where(x => x.RMultiple != null).ToList();
            var expectancy = TradingCalculator.CalculateExpectancy(tradesForExpectancy);

            Log.Info(
                currentDateTimeTicks != null ? $"Up to: {new DateTime(currentDateTimeTicks.Value): dd-MM-yy HH:mm} " : String.Empty
                // /* {r.PercentComplete:0.00}% /. Running: {r.SecondsRunning}s. "
                + $"Created: {orderTrades.Count() + closedTrades.Count + trades.OpenTrades.Count()} "
                + $"Open: {trades.OpenTrades.Count()}. Orders: {orderTrades.Count()} "
                + $"Closed: {closedTrades.Count} (Hit stop: {closedTrades.Count(x => x.CloseReason == TradeCloseReason.HitStop)} "
                + $"Hit limit: {closedTrades.Count(x => x.CloseReason == TradeCloseReason.HitLimit)} "
                + $"Hit expiry: {closedTrades.Count(x => x.CloseReason == TradeCloseReason.HitExpiry)} "
                + $"Cached trades: {trades.CachedTradesCount}) "
                + $"Expectancy: {expectancy:0.00}");
        }


        private List<AddedCandleTimeframe> ProgressStrategyCandles(
            List<CandlesWithIndex> tfOrderedStrategyCandles,
            Dictionary<string, TimeframeLookup<List<Candle>>> currentCandles,
            Dictionary<string, float> currentBidPrices,
            Dictionary<string, float> currentAskPrices,
            out long currentTimeTicks,
            out long nextTimeTicks)
        {
            var ret = new List<AddedCandleTimeframe>();
            currentTimeTicks = long.MaxValue;
            nextTimeTicks = long.MaxValue;

            if (tfOrderedStrategyCandles[0].NextIndex >= tfOrderedStrategyCandles[0].Candles.Count) return ret;

            // Get earliest next time
            foreach (var c in tfOrderedStrategyCandles)
            {
                if (c.NextIndex >= c.Candles.Count) continue;
                var closeTimeTicks = c.Candles[c.NextIndex].CloseTimeTicks;
                if (closeTimeTicks < currentTimeTicks) currentTimeTicks = closeTimeTicks;
            }

            foreach (var c in tfOrderedStrategyCandles)
            {
                for (var i = c.NextIndex; i < c.Candles.Count; i++)
                {
                    var closeTimeTicks = c.Candles[i].CloseTimeTicks;
                    if (closeTimeTicks > currentTimeTicks) break;

                    if (ret.Any(z => z.Market == c.Market && z.Timeframe == c.Timeframe))
                    {
                        throw new ApplicationException();
                    }

                    currentBidPrices[c.Market] = c.Candles[i].CloseBid;
                    currentAskPrices[c.Market] = c.Candles[i].CloseAsk;
                    ret.Add(new AddedCandleTimeframe(c.Market, c.Timeframe, c.Candles[i]));
                    currentCandles[c.Market][c.Timeframe].Add(c.Candles[i]);
                    c.NextIndex = i + 1;
                }
            }

            // Get earliest next time
            foreach (var c in tfOrderedStrategyCandles)
            {
                if (c.NextIndex >= c.Candles.Count) continue;
                var closeTimeTicks = c.Candles[c.NextIndex].CloseTimeTicks;
                if (closeTimeTicks < nextTimeTicks) nextTimeTicks = closeTimeTicks;
            }

            return ret;
        }

        private void CompleteTradeDetails(List<Trade> trades)
        {
            foreach (var t in trades)
            {
                TradeCalculator.UpdateInitialStopPips(t);
                TradeCalculator.UpdateInitialLimitPips(t);
                TradeCalculator.UpdateRMultiple(t);
            }
        }

        private List<CandlesWithIndex> GetRunCandles(StrategyBase strategy, Timeframe runTimeframe, DateTime? startTime = null, DateTime? endTime = null, bool updateCandles = false)
        {
            var ret = new List<CandlesWithIndex>();
            foreach (var m in strategy.Markets)
            {
                var candles = _candleService.GetDerivedCandles(_broker, m, runTimeframe, updateCandles, minOpenTimeUtc: startTime, maxCloseTimeUtc: endTime, forceCreateDerived: true);
                if (candles.Count == 0) throw new ApplicationException($"No candles found for {m}");

                ret.Add(new CandlesWithIndex(m, runTimeframe, candles));
            }

            return ret;
        }

        private List<CandlesWithIndex> GetTfOrderedStrategyCandles(StrategyBase strategy, DateTime? startTime = null, DateTime? endTime = null, bool updateCandles = false)
        {
            var ret = new List<CandlesWithIndex>();
            foreach (var tf in strategy.Timeframes.OrderBy(x => x))
            {
                foreach (var m in strategy.Markets)
                {
                    var candles = _candleService.GetDerivedCandles(_broker, m, tf, updateCandles, minOpenTimeUtc: startTime, maxCloseTimeUtc: endTime);
                    if (candles.Count == 0) throw new ApplicationException($"No candles found for {m}");
                    ret.Add(new CandlesWithIndex(m, tf, candles));
                }
            }

            return ret;
        }

        private class CandlesWithIndex
        {
            public CandlesWithIndex(string market, Timeframe timeframe, List<Candle> candles)
            {
                Market = market;
                Timeframe = timeframe;
                Candles = candles;
            }

            public string Market { get; }
            public Timeframe Timeframe { get; }
            public List<Candle> Candles { get; }
            public int NextIndex { get; set; } = 0;
        }
    }

    public class AddedCandleTimeframe
    {
        public AddedCandleTimeframe(string market, Timeframe timeframe, Candle candle)
        {
            Market = market;
            Timeframe = timeframe;
            Candle = candle;
        }

        public string Market { get; }
        public Timeframe Timeframe { get; }
        public Candle Candle { get; }
    }

}