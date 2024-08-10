using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.SpotData;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.ExchangeInterfaces;
using CryptoExchange.Net.Objects;
using Hallupa.Library.Extensions;
using Hallupa.TraderTools.Basics;
using log4net;
using Microsoft.Extensions.Configuration;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using OrderType = Binance.Net.Enums.OrderType;

namespace Hallupa.TraderTools.Brokers.Binance
{
    public class BinanceBroker : IBroker, ITradeFactory
    {
        public string Name => "Binance";
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private BinanceClient _client;
        private string _apiKey;
        private string _secretKey;
        private List<BinanceSymbol> _symbols;


        public List<Trade> GetOpenOrders()
        {
            var orders = _client.Spot.Order.GetOpenOrders();
            var trades = new List<Trade>();

            foreach (var o in orders.Data)
            {
                trades.Add(CreateTradeFromOrder(o));
            }

            return trades;
        }

        public bool UpdateAccount(
            IBrokerAccount account,
            IBrokersCandlesService candlesService,
            IMarketDetailsService marketsService,
            Action<string> updateProgressAction,
            out List<Trade> addedOrUpdatedTrades)
        {
            addedOrUpdatedTrades = new List<Trade>();

            var limit = 500;

            foreach (var symbol in GetSymbols())
            {
                Log.Debug($"Updating account for {symbol}");

                // Get highest order ID
                var maxId = account.Trades.Count(t => t.CloseDateTime != null && t.Market == symbol) == 0 ? 1 : account.Trades
                    .Where(t => t.CloseDateTime != null && t.Market == symbol)
                    .Max(t => Convert.ToInt64(t.Id));
                WebCallResult<IEnumerable<BinanceOrder>> orders = null;

                // Get orders
                while (orders == null || orders.Data.Count() == limit)
                {
                    orders = _client.Spot.Order.GetAllOrders(symbol, orderId: maxId);

                    if (orders.Success == false && orders.Error.Code == -1003)
                    {
                        Log.Info("Too many Binance requests - pausing requests");
                        // -1003 = Too many requests
                        Thread.Sleep(60 * 1000);
                        orders = null;
                        continue;
                    }

                    if (orders.Success)
                    {
                        AddOrUpdateOrders(account, addedOrUpdatedTrades, orders);
                    }
                    else
                    {
                        Log.Error($"Unable to get orders for symbol {symbol} - {orders.Error.Message}");
                        break;
                    }

                    maxId = account.Trades.Count(t => t.CloseDateTime != null && t.Market == symbol) == 0 ? 1 : account.Trades
                        .Where(t => t.CloseDateTime != null && t.Market == symbol)
                        .Max(t => Convert.ToInt64(t.Id));
                }
            }

            Log.Info($"Binance account updated - {addedOrUpdatedTrades} trades added or updated");


            return true;
        }

        private Trade CreateTradeFromOrder(BinanceOrder o)
        {
            var trade = new Trade
            {
                Broker = Name,
                Market = o.Symbol,
                TradeDirection = o.Side == OrderSide.Buy ? TradeDirection.Long : TradeDirection.Short,
                Id = o.OrderId.ToString()
            };

            trade.SetOrder(
                o.CreateTime,
                o.Price,
                o.Symbol,
                o.Side == OrderSide.Buy ? TradeDirection.Long : TradeDirection.Short,
                o.Quantity,
                null);

            trade.EntryQuantity = o.QuantityFilled;
            trade.EntryPrice = o.AverageFillPrice;

            if (trade.EntryQuantity == trade.OrderAmount)
            {
                trade.CloseDateTime = o.UpdateTime;
            }

            return trade;
        }

        private void AddOrUpdateOrders(IBrokerAccount account, List<Trade> addedOrUpdatedTrades, WebCallResult<IEnumerable<BinanceOrder>> orders)
        {
            foreach (var o in orders.Data)
            {
                var existing = account.Trades.FirstOrDefault(t => t.Id == o.OrderId.ToString());

                if (existing == null)
                {
                    var trade = CreateTradeFromOrder(o);
                    addedOrUpdatedTrades.Add(trade);
                    account.Trades.Add(trade);
                }
                else
                {
                    existing.EntryQuantity = o.QuantityFilled;
                    existing.EntryPrice = o.AverageFillPrice;

                    if (existing.EntryQuantity == existing.OrderAmount)
                    {
                        existing.CloseDateTime = o.UpdateTime;
                    }

                    addedOrUpdatedTrades.Add(existing);
                }
            }
        }

        public bool UpdateCandles(List<Candle> candles, string market, Timeframe timeframe, DateTime start, Action<string> progressUpdate)
        {
            // Remove any duplicates
            var foundCandles = new HashSet<long>();
            for (var i = 0; i < candles.Count; i++)
            {
                var c = candles[i];

                if (foundCandles.Contains(c.OpenTimeTicks))
                {
                    candles.RemoveAt(i);
                    i--;
                }

                foundCandles.Add(c.OpenTimeTicks);
            }

            while (true)
            {
                var fastRetries = 3;
                var slowRetries = 10;
                WebCallResult<IEnumerable<ICommonKline>> binanceCandles = null;
                while (fastRetries >= 0)
                {
                    binanceCandles = ((IExchangeClient)_client).GetKlinesAsync(
                        market,
                        TimeSpan.FromSeconds((int)timeframe),
                        start,
                        DateTime.UtcNow,
                        1000).Result;

                    if (binanceCandles.Error != null)
                    {
                        Log.Error($"Binance error: {binanceCandles.Error.Message}");
                    }
                    else
                    {
                        break;
                    }

                    if (fastRetries > 0)
                    {
                        Thread.Sleep(500);
                        fastRetries--;
                    }
                    else if (slowRetries > 0)
                    {
                        Thread.Sleep(30000);
                        slowRetries--;
                    }

                    if (fastRetries == 0 && slowRetries == 0)
                    {
                        Log.Error("Binance ran out of attempts");
                    }
                }

                foreach (var b in binanceCandles.Data)
                {
                    var candle = CreateCandle((IBinanceKline)b, market, timeframe);
                    if (candle == null) continue;
                    var c = candle.Value;

                    if (!foundCandles.Contains(c.OpenTimeTicks))
                    {
                        candles.Add(c);
                        foundCandles.Add(c.OpenTimeTicks);
                    }
                    else
                    {
                        var index = candles.BinarySearchGetItem(l => candles[l].OpenTimeTicks, 0, c.OpenTimeTicks, BinarySearchMethod.Value);
                        candles.RemoveAt(index);
                        candles.Insert(index, c);
                    }
                }

                if (!binanceCandles.Data.Any()) break;
                Log.Debug($"Got {binanceCandles.Data.Count()} candles for {market} {timeframe} - upto: {((IBinanceKline)binanceCandles.Data.Last()).CloseTime:hh:mm dd-MM-yyyy}");
                start = binanceCandles.Data.Last().CommonOpenTime.AddSeconds(1);
            }

            Log.Debug($"Update {market} {timeframe} complete");
            return true;
        }

        public void Connect()
        {
        }

        public Dictionary<string, AssetBalance> GetBalance(DateTime? dateTimeUtc = null)
        {
            var balances = _client.General.GetAccountInfo().Data.Balances;

            return balances.Where(x => x.Total > 0).ToDictionary(
                x => x.Asset,
                x => new AssetBalance(x.Asset, x.Total));

        }

        public List<string> GetSymbols()
        {
            return GetFullSymbols().Select(c => c.Name).ToList();
        }

        public List<BinanceSymbol> GetFullSymbols()
        {
            if (_symbols != null) return _symbols;

            var result = _client.Spot.System.GetExchangeInfo().Data.Symbols;

            _symbols = result.ToList();
            return _symbols;
        }

        public BinanceBroker()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("AppSettings.json", optional: true, reloadOnChange: true)
                .Build();

            var options = config.GetSection("Binance").Get<BinanceConfiguration>();
            _apiKey = options.ApiKey;
            _secretKey = options.SecretKey;

            _client = new BinanceClient(new BinanceClientOptions
            {
                ApiCredentials = new ApiCredentials(_apiKey, _secretKey),
                //LogVerbosity = LogVerbosity.Debug
            });
        }

        public BinanceBroker(string apiKey, string secretKey)
        {
            _apiKey = apiKey;
            _secretKey = secretKey;

            _client = new BinanceClient(new BinanceClientOptions
            {
                ApiCredentials = new ApiCredentials(_apiKey, _secretKey),
                //LogVerbosity = LogVerbosity.Debug
            });
        }

        public ConnectStatus Status => ConnectStatus.Connected;
        public BrokerKind Kind => BrokerKind.Trade;

        public List<TickData> GetTickData(IBroker broker, string market, DateTime utcStart, DateTime utcEnd)
        {
            throw new NotImplementedException();
        }

        public List<MarketDetails> GetMarketDetailsList()
        {
            throw new NotImplementedException();
        }

        public Candle? GetSingleCandle(string market, Timeframe timeframe, DateTime date)
        {
            throw new NotImplementedException();
        }

        private Candle? CreateCandle(IBinanceKline kline, string market, Timeframe timefame)
        {
            var c = new Candle
            {
                CloseTimeTicks = kline.CloseTime.Ticks,
                IsComplete = kline.CloseTime <= DateTime.UtcNow ? (byte)1 : (byte)0,
                CloseBid = (float)kline.CommonClose,
                HighBid = (float)kline.CommonHigh,
                LowBid = (float)kline.CommonLow,
                OpenTimeTicks = kline.OpenTime.Ticks,
                OpenBid = (float)kline.CommonOpen,
                Volume = (float)kline.CommonVolume,
                CloseAsk = (float)kline.CommonClose,
                HighAsk = (float)kline.CommonHigh,
                LowAsk = (float)kline.CommonLow,
                OpenAsk = (float)kline.CommonOpen
            };

            var interval = (c.CloseTime() - c.OpenTime()).TotalSeconds;
            if ((int)timefame != Math.Round(interval))
            {
                // throw new ApplicationException("Candle timeframe wrong"); // Some candles are wrong length (Seen some in 2018 date ranges)
            }

            if (c.OpenTimeTicks > c.CloseTimeTicks)
            {
                //throw new ApplicationException("Candle open time is later the close time");
                Log.Warn($"Binance candle ignored {market} {c} {timefame}");
                return null;
            }

            return c;
        }

        public Trade CreateOrder(string broker, decimal entryOrder,
            Candle latestCandle, TradeDirection direction, decimal amount,
            string market, string baseAsset, DateTime? orderExpireTime, decimal? stop, decimal? limit,
            CalculateOptions calculateOptions = CalculateOptions.Default)
        {
            if (broker != "Binance") throw new ApplicationException("Incorrect broker");
            if (stop != null) throw new ApplicationException("Stop needs to be implemented");
            if (limit != null) throw new ApplicationException("Limit needs to be implemented");
            if (orderExpireTime != null) throw new ApplicationException("Expire time needs to be implemented");

            var res = _client.Spot.Order.PlaceOrder(
                market,
                direction == TradeDirection.Long ? OrderSide.Buy : OrderSide.Sell,
                OrderType.Limit,
                amount,
                timeInForce: TimeInForce.GoodTillCancel,
                price: entryOrder);

            if (res.Success)
            {
                Log.Info("Order trade created");

                var trade = new Trade
                {
                    Broker = broker,
                    CalculateOptions = calculateOptions,
                    Market = market,
                    BaseAsset = baseAsset,
                    TradeDirection = direction,
                    Id = res.Data.OrderId.ToString()
                };

                trade.SetOrder(res.Data.CreateTime, entryOrder, market, direction,
                    amount, orderExpireTime);

                return trade;
            }
            else
            {
                Log.Info($"Failed to create order trade - {res.Error.Message}");

                return null;
            }
        }

        public void UpdateTrade(Trade t)
        {
            var order = _client.Spot.Order.GetOrder(t.Market, long.Parse(t.Id));

            if (order.Success)
            {
                t.EntryPrice = order.Data.AverageFillPrice;
                t.EntryQuantity = order.Data.QuantityFilled;
            }
            else
            {
                Log.Error($"Unable to update trade details - {order.Error.Message}");
                throw new ApplicationException("Unable to update trade");
            }
        }

        public Trade CreateMarketEntry(string broker, decimal entryPrice, DateTime entryTime,
            TradeDirection direction, decimal amount,
            string market, string baseAsset, decimal? stop, decimal? limit, Timeframe? timeframe = null, string strategies = null,
            string comments = null, bool alert = false, CalculateOptions calculateOptions = CalculateOptions.Default,
            TradeUpdateMode updateMode = TradeUpdateMode.Default)
        {
            if (broker != "Binance") throw new ApplicationException("Incorrect broker");
            if (stop != null) throw new ApplicationException("Stop needs to be implemented");
            if (limit != null) throw new ApplicationException("Limit needs to be implemented");

            var symbol = GetFullSymbols().First(x => x.Name == market);
            var updatedQuantity = (decimal) ((int) (amount / symbol.LotSizeFilter.StepSize)) * symbol.LotSizeFilter.StepSize;
            Log.Info($"{market} Using precision: {symbol.BaseAssetPrecision} and lot size: {symbol.LotSizeFilter.StepSize} updated quantity: {updatedQuantity}");

            var res = _client.Spot.Order.PlaceOrder(
                market,
                direction == TradeDirection.Long ? OrderSide.Buy : OrderSide.Sell,
                OrderType.Market,
                updatedQuantity);
            if (res.Success)
            {
                Log.Info("Market trade created");

                var trade = new Trade
                {
                    Id = res.Data.OrderId.ToString(),
                    Broker = Name,
                    CalculateOptions = calculateOptions,
                    Market = market,
                    BaseAsset = baseAsset,
                    TradeDirection = direction,
                    OrderAmount = updatedQuantity,
                    EntryPrice = res.Data.AverageFillPrice,
                    EntryDateTime = res.Data.CreateTime,
                    EntryQuantity = res.Data.QuantityFilled,
                    Timeframe = timeframe,
                    Alert = alert,
                    Comments = comments,
                    Strategies = strategies,
                    UpdateMode = updateMode
                };

                return trade;
            }
            else
            {
                Log.Info($"Failed to create market trade - {res.Error.Message}");

                return null;
            }
        }

        public decimal TruncateDecimal(decimal value, int precision)
        {
            decimal step = (decimal)Math.Pow(10, precision);
            decimal tmp = Math.Truncate(step * value);
            return tmp / step;
        }
    }
}