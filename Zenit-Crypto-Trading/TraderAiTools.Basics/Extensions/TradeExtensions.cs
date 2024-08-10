using System;
using System.Linq;
using System.Reflection;
using log4net;
using TraderTools.Basics.Helpers;

namespace TraderTools.Basics.Extensions
{
    public static class TradeExtensions
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static decimal GetTradeProfit(this Trade trade, DateTime dateTimeUTC, Timeframe candlesTimeframe,
            IBrokersCandlesService candlesService, MarketDetails marketDetails, IBroker broker, bool updateCandles)
        {
            if (trade.EntryPrice == null || trade.EntryDateTime == null)
            {
                return 0M;
            }

            if (trade.CloseDateTime != null && trade.CloseDateTime.Value <= dateTimeUTC)
            {
                return trade.Profit ?? 0M;
            }

            if (trade.EntryDateTime >= dateTimeUTC)
            {
                return 0M;
            }

            var latestCandle = candlesService.GetLastClosedCandle(
                trade.Market, broker, candlesTimeframe, dateTimeUTC, updateCandles);

            if (latestCandle != null && trade.PricePerPip != null)
            {
                var closePriceToUse = trade.TradeDirection == TradeDirection.Long
                    ? (decimal)latestCandle.Value.CloseBid
                    : (decimal)latestCandle.Value.CloseAsk;
                var profitPips = PipsHelper.GetPriceInPips(trade.TradeDirection == TradeDirection.Long ? closePriceToUse - trade.EntryPrice.Value : trade.EntryPrice.Value - closePriceToUse, marketDetails);
                var totalRunningTime = (DateTime.UtcNow - trade.EntryDateTime.Value).TotalDays;
                var runningTime = (trade.EntryDateTime.Value - dateTimeUTC).TotalDays;

                var tradeProfit = trade.PricePerPip.Value * profitPips +
                                  (!totalRunningTime.Equals(0.0) && trade.Rollover != null
                                      ? trade.Rollover.Value * (decimal)(runningTime / totalRunningTime)
                                      : 0M);

                return tradeProfit;
            }

            return 0M;
        }

        public static decimal GetProfitForLatestDay(this Trade trade, IBrokersCandlesService candlesService, IBrokersService brokersService, IMarketDetailsService marketDetailsService)
        {
            var broker = brokersService.Brokers.FirstOrDefault(x => x.Name == trade.Broker);

            if (broker != null)
            {
                var marketDetails = marketDetailsService.GetMarketDetails(broker.Name, trade.Market);

                var now = DateTime.UtcNow;
                var endDate = trade.CloseDateTime != null
                    ? new DateTime(trade.CloseDateTime.Value.Year, trade.CloseDateTime.Value.Month, trade.CloseDateTime.Value.Day, 23,
                        59, 59, DateTimeKind.Utc)
                    : new DateTime(now.Year, now.Month, now.Day, 23, 59, 59, DateTimeKind.Utc);
                var startDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 0, 0, 0, DateTimeKind.Utc);
                return trade.GetTradeProfit(endDate, Basics.Timeframe.D1, candlesService, marketDetails, broker, false)
                       - trade.GetTradeProfit(startDate, Basics.Timeframe.D1, candlesService, marketDetails, broker, false);
            }

            return decimal.MinValue;
        }

        public static void SimulateTrade(this Trade trade, Candle candle, out bool updated)
        {
            SimulateTrade(trade, candle.LowBid, candle.HighBid, candle.CloseBid,
                candle.LowAsk, candle.HighAsk, candle.CloseAsk,
                candle.OpenTimeTicks, candle.CloseTimeTicks, out updated);
        }

        public static void SimulateTrade(this Trade trade,
            float candleBidLow, float candleBidHigh, float candleBidClose,
            float candleAskLow, float candleAskHigh, float candleAskClose,
            long candleOpenTimeTicks, long candleCloseTimeTicks, out bool updated)
        {
            updated = false;

            if (trade.CloseDateTime != null) return;
            if (trade.TradeDirection == null) return;

            // Ask = buy price, Bid = sell price

            // Try to close trade
            if (trade.EntryPrice != null && trade.CloseReason == null)
            {
                ProcessClose(trade, candleBidLow, candleBidHigh, candleAskLow, candleAskHigh, candleCloseTimeTicks, ref updated);
            }

            // Process market orders
            if (trade.EntryPrice == null && trade.OrderPrice == null
                                         && candleCloseTimeTicks >= trade.OrderDateTime.Value.Ticks)
            {
                ProcessMarketEntry(trade, candleBidClose, candleAskClose, candleCloseTimeTicks, ref updated);
            }

            // Try to fill order
            if (trade.EntryPrice == null && trade.OrderPrice != null
                                         && ((candleOpenTimeTicks <= trade.OrderDateTime.Value.Ticks
                                            && candleCloseTimeTicks >= trade.OrderDateTime.Value.Ticks) || candleOpenTimeTicks >= trade.OrderDateTime.Value.Ticks))
            {
                ProcessOrder(trade, candleBidLow, candleBidHigh, candleBidClose, candleAskLow, candleAskHigh, candleAskClose, candleCloseTimeTicks, ref updated);
            }
        }

        private static void ProcessClose(Trade trade, float candleBidLow, float candleBidHigh, float candleAskLow,
            float candleAskHigh, long candleCloseTimeTicks, ref bool updated)
        {
            var openTrade = trade;

            if (openTrade.StopPrice != null)
            {
                if (openTrade.TradeDirection.Value == TradeDirection.Long && candleBidLow <= openTrade.StopPriceFloat.Value)
                {
                    var stopPrice = Math.Min((decimal)candleBidHigh, openTrade.StopPrice.Value);
                    openTrade.SetClose(new DateTime(candleCloseTimeTicks, DateTimeKind.Utc), stopPrice,
                        TradeCloseReason.HitStop);
                    updated = true;
                    return;
                }
                else if (openTrade.TradeDirection.Value == TradeDirection.Short && candleAskHigh >= openTrade.StopPriceFloat.Value)
                {
                    var stopPrice = Math.Max((decimal)candleAskLow, openTrade.StopPrice.Value);
                    openTrade.SetClose(new DateTime(candleCloseTimeTicks, DateTimeKind.Utc), stopPrice,
                        TradeCloseReason.HitStop);
                    updated = true;
                    return;
                }
            }

            if (openTrade.LimitPrice != null)
            {
                if (openTrade.TradeDirection.Value == TradeDirection.Long && candleBidHigh >= openTrade.LimitPriceFloat.Value)
                {
                    var limitPrice = Math.Max((decimal)candleBidLow, openTrade.LimitPrice.Value);
                    openTrade.SetClose(new DateTime(candleCloseTimeTicks, DateTimeKind.Utc), limitPrice,
                        TradeCloseReason.HitLimit);
                    updated = true;
                    return;
                }
                else if (openTrade.TradeDirection.Value == TradeDirection.Short && candleAskLow <= openTrade.LimitPriceFloat.Value)
                {
                    var limitPrice = Math.Min((decimal)candleAskHigh, openTrade.LimitPrice.Value);
                    openTrade.SetClose(new DateTime(candleCloseTimeTicks, DateTimeKind.Utc), limitPrice,
                        TradeCloseReason.HitLimit);
                    updated = true;
                    return;
                }
            }
        }

        private static void ProcessMarketEntry(Trade trade, float candleBidClose, float candleAskClose,
            long candleCloseTimeTicks, ref bool updated)
        {
            if (trade.OrderDateTime == null ||
                (trade.OrderDateTime != null && trade.OrderDateTime.Value.Ticks <= candleCloseTimeTicks))
            {
                if (trade.TradeDirection == TradeDirection.Long)
                {
                    var entry = (decimal)candleAskClose;
                    if (trade.StopPrice == null || trade.StopPrice.Value <= entry)
                    {
                        trade.SetEntry(new DateTime(candleCloseTimeTicks, DateTimeKind.Utc), entry, trade.OrderAmount.Value);
                        updated = true;
                    }
                    else
                    {
                        Log.Warn($"Long trade has stop price: {trade.StopPrice.Value:0.00000} above entry price: {entry:0.00000} - ignoring trade");
                    }
                }
                else
                {
                    var entry = (decimal)candleBidClose;
                    if (trade.StopPrice == null || trade.StopPrice.Value >= entry)
                    {
                        trade.SetEntry(new DateTime(candleCloseTimeTicks, DateTimeKind.Utc), entry, trade.OrderAmount.Value);
                        updated = true;
                    }
                    else
                    {
                        Log.Warn($"Short trade has stop price: {trade.StopPrice.Value:0.00000} below entry price: {entry:0.00000} - ignoring trade");
                    }
                }
            }
        }

        private static void ApplyOrderType(Trade t, float candleCloseAsk, float candleCloseBid)
        {
            if (t.OrderPrice != null)
            {
                if (t.TradeDirection == TradeDirection.Long)
                {
                    t.OrderType = t.OrderPrice.Value <= (decimal)candleCloseAsk
                        ? OrderType.LimitEntry
                        : OrderType.StopEntry;
                }
                else
                {
                    t.OrderType = t.OrderPrice.Value <= (decimal)candleCloseBid
                        ? OrderType.StopEntry
                        : OrderType.LimitEntry;
                }
            }
        }

        private static void ProcessOrder(Trade trade, float candleBidLow, float candleBidHigh, float candleBidClose,
            float candleAskLow, float candleAskHigh, float candleAskClose, long candleCloseTimeTicks, ref bool updated)
        {
            var order = trade;

            if (order.OrderPrice != null)
            {
                if (order.OrderType == null) ApplyOrderType(trade, candleAskClose, candleBidClose);

                var orderType = order.OrderType ?? OrderType.LimitEntry;
                var direction = order.TradeDirection;
                var orderPrice = order.OrderPriceFloat;

                switch (orderType)
                {
                    case OrderType.LimitEntry: // Buy below current market price or sell above current market price
                        {
                            //                ___
                            //                | |
                            //  - - - - - - - | |
                            //                | |
                            //                ---
                            if (direction == TradeDirection.Long && candleAskLow <= orderPrice && candleAskHigh >= orderPrice)
                            {
                                order.SetEntry(new DateTime(candleCloseTimeTicks, DateTimeKind.Utc), (decimal)orderPrice, trade.OrderAmount.Value);
                                updated = true;
                            }
                            // - - - - - - - -
                            //                ___
                            //                | |
                            //                | |
                            //                ---
                            else if (direction == TradeDirection.Long && candleAskHigh <= orderPrice)
                            {
                                order.SetEntry(new DateTime(candleCloseTimeTicks, DateTimeKind.Utc), (decimal)candleAskHigh, trade.OrderAmount.Value);
                                updated = true;
                            }
                            //                ___
                            //                | |
                            //  - - - - - - - | |
                            //                | |
                            //                ---
                            else if (direction == TradeDirection.Short && candleBidLow <= orderPrice && candleBidHigh >= orderPrice)
                            {
                                order.SetEntry(new DateTime(candleCloseTimeTicks, DateTimeKind.Utc), (decimal)orderPrice, trade.OrderAmount.Value);
                                updated = true;
                            }
                            //                ___
                            //                | |
                            //                | |
                            //                ---
                            // - - - - - - - -
                            else if (direction == TradeDirection.Short && candleBidLow >= orderPrice)
                            {
                                order.SetEntry(new DateTime(candleCloseTimeTicks, DateTimeKind.Utc), (decimal)candleBidLow, trade.OrderAmount.Value);
                                updated = true;
                            }

                            break;
                        }

                    case OrderType.StopEntry: // Buy above current price or sell below current market price
                        {
                            //                ___
                            //                | |
                            //  - - - - - - - | |
                            //                | |
                            //                ---
                            if (order.TradeDirection == TradeDirection.Long && candleAskLow <= orderPrice && candleAskHigh >= orderPrice)
                            {
                                order.SetEntry(new DateTime(candleCloseTimeTicks, DateTimeKind.Utc), (decimal)orderPrice, trade.OrderAmount.Value);
                                updated = true;
                            }
                            //                ___
                            //                | |
                            //                | |
                            //                ---
                            // - - - - - - - -
                            else if (direction == TradeDirection.Long && candleAskLow >= orderPrice)
                            {
                                order.SetEntry(new DateTime(candleCloseTimeTicks, DateTimeKind.Utc), (decimal)candleAskLow, trade.OrderAmount.Value);
                                updated = true;
                            }
                            //                ___
                            //                | |
                            //  - - - - - - - | |
                            //                | |
                            //                ---
                            else if (order.TradeDirection == TradeDirection.Short && candleBidLow <= orderPrice && candleBidHigh >= orderPrice)
                            {
                                order.SetEntry(new DateTime(candleCloseTimeTicks, DateTimeKind.Utc), (decimal)orderPrice, trade.OrderAmount.Value);
                                updated = true;
                            }
                            // - - - - - - - -
                            //                ___
                            //                | |
                            //                | |
                            //                ---
                            else if (direction == TradeDirection.Short && candleBidHigh <= orderPrice)
                            {
                                order.SetEntry(new DateTime(candleCloseTimeTicks, DateTimeKind.Utc), (decimal)candleBidHigh, trade.OrderAmount.Value);
                                updated = true;
                            }

                            break;
                        }
                }
            }

            if (trade.EntryPrice == null && order.OrderExpireTime != null &&
                candleCloseTimeTicks >= order.OrderExpireTime.Value.Ticks)
            {
                order.SetExpired(new DateTime(candleCloseTimeTicks, DateTimeKind.Utc));
                updated = true;
            }
        }

        public static void AddLimitPrice(this Trade trade, DateTime date, decimal? price)
        {
            trade.AddLimitPrice(string.Empty, date, price);
        }

        public static void AddLimitPrice(this Trade trade, string id, DateTime date, decimal? price)
        {
            if (trade.LimitPrices.Count > 0 && trade.LimitPrices.Last().Price == price)
            {
                return;
            }

            if (trade.LimitPrices.Count > 0 && trade.LimitPrices.Last().Date == date)
            {
                trade.LimitPrices.RemoveAt(trade.OrderPrices.Count - 1);
            }

            if (trade.UpdateMode == TradeUpdateMode.Unchanging) throw new ApplicationException("Trade set to untouched mode cannot change it's limit price after being set");

            trade.LimitPrices.Add(new DatePrice(id, date, price));

            if (trade.LimitPrices.Count > 1)
            {
                trade.LimitPrices = trade.LimitPrices.OrderBy(x => x.Date).ToList();
            }

            TradeCalculator.UpdateLimit(trade);

            if (!trade.CalculateOptions.HasFlag(CalculateOptions.ExcludePipsCalculations))
            {
                TradeCalculator.UpdateLimitPips(trade);

                if (trade.LimitPrices.Count == 1)
                {
                    TradeCalculator.UpdateInitialLimitPips(trade);
                }
            }
        }

        public static void RemoveLimitPrice(this Trade trade, int index)
        {
            if (index >= trade.LimitPrices.Count)
            {
                return;
            }

            if (trade.UpdateMode == TradeUpdateMode.Unchanging) throw new ApplicationException("Trade set to untouched mode cannot change it's stop price after being set");

            trade.LimitPrices.RemoveAt(index);
        }

        public static void SetClose(this Trade trade, DateTime dateTime, decimal? price, TradeCloseReason reason)
        {
            trade.ClosePrice = price;
            trade.CloseDateTime = dateTime;
            trade.CloseReason = reason;
        }

        public static void SetExpired(this Trade trade, DateTime dateTime)
        {
            trade.CloseDateTime = dateTime;
            trade.CloseReason = TradeCloseReason.HitExpiry;
        }
    }
}