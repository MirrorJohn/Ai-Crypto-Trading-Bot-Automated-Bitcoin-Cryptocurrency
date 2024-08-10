using System;
using System.Collections.Generic;
using System.Linq;
using Hallupa.Library;
using TraderTools.Basics.Extensions;

namespace TraderTools.Basics
{
    public static class TradeCalculator
    {
        private static IBrokersCandlesService _candlesService;
        private static IBrokersService _brokersService;
        private static IMarketDetailsService _marketsService;
        private static Dictionary<Trade, IBroker> _tradeBrokerLookup = new Dictionary<Trade, IBroker>();

        static TradeCalculator()
        {
            _candlesService = DependencyContainer.Container.GetExportedValue<IBrokersCandlesService>();
            _brokersService = DependencyContainer.Container.GetExportedValue<IBrokersService>();
            _marketsService = DependencyContainer.Container.GetExportedValue<IMarketDetailsService>();
        }

        public static void UpdateRMultiple(Trade trade)
        {
            if (trade.RiskAmount != null && trade.RiskAmount.Value != 0M && trade.Profit != null)
            {
                trade.RMultiple = trade.Profit / trade.RiskAmount;
            }
            else if (trade.EntryPrice != null && trade.EntryDateTime != null && trade.ClosePrice != null && trade.StopPrices.Count > 0)
            {
                // Get stop price at entry
                DatePrice entryStop = null;
                foreach (var stop in trade.StopPrices)
                {
                    if (entryStop == null || stop.Date <= trade.EntryDateTime.Value)
                    {
                        entryStop = stop;
                    }
                    else
                    {
                        break;
                    }
                }

                if (entryStop?.Price != null)
                {
                    var oneR = Math.Abs(trade.EntryPrice.Value - entryStop.Price.Value);
                    if (trade.TradeDirection == TradeDirection.Long)
                    {
                        trade.RMultiple = oneR != 0 ? (decimal?)(trade.ClosePrice.Value - trade.EntryPrice.Value) / oneR : null;
                    }
                    else if (oneR != 0)
                    {
                        trade.RMultiple = trade.EntryPrice.Value != trade.ClosePrice.Value ? (trade.EntryPrice.Value - trade.ClosePrice.Value) / oneR : 0;
                    }
                    else
                    {
                        trade.RMultiple = null;
                    }
                }
            }
            else if (trade.EntryPrice != null && trade.EntryDateTime != null && trade.ClosePrice == null && trade.InitialStop != null && trade.CalculateOptions.HasFlag(CalculateOptions.IncludeOpenTradesInRMultipleCalculation))
            {
                var stopPrice = trade.InitialStop.Value;
                var risk = Math.Abs(stopPrice - trade.EntryPrice.Value);
                var currentCandle = _candlesService.GetCandles(_brokersService.GetBroker(trade.Broker), trade.Market, Timeframe.D1, false, cacheData: false).Last();
                var currentClose = trade.TradeDirection == TradeDirection.Long
                    ? (decimal)currentCandle.CloseBid
                    : (decimal)currentCandle.CloseAsk;

                // Get stop price at entry
                DatePrice entryStop = null;
                foreach (var stop in trade.StopPrices)
                {
                    if (entryStop == null || stop.Date <= trade.EntryDateTime.Value)
                    {
                        entryStop = stop;
                    }
                    else
                    {
                        break;
                    }
                }

                if (entryStop?.Price != null)
                {
                    var oneR = Math.Abs(trade.EntryPrice.Value - entryStop.Price.Value);
                    if (trade.TradeDirection == TradeDirection.Long)
                    {
                        trade.RMultiple = (currentClose - trade.EntryPrice.Value) / oneR;
                    }
                    else
                    {
                        trade.RMultiple = (trade.EntryPrice.Value - currentClose) / oneR;
                    }
                }
            }
            else
            {
                trade.RMultiple = null;
            }
        }

        public static void UpdateLimit(Trade trade)
        {
            if (trade.LimitPrices.Count == 0) return;

            if (trade.LimitPrice != trade.LimitPrices[trade.LimitPrices.Count - 1].Price)
            {
                trade.LimitPrice = trade.LimitPrices[trade.LimitPrices.Count - 1].Price;

                if (trade.LimitPrices.Count == 1)
                {
                    trade.InitialLimit = trade.LimitPrice;
                }
            }
        }

        public static void UpdateInitialLimitPips(Trade trade)
        {
            if (trade.EntryPrice == null && trade.OrderPrice == null) return;

            var price = trade.EntryPrice ?? trade.OrderPrice.Value;

            // Update current Limit
            if (trade.InitialLimit != null && !string.IsNullOrEmpty(trade.Broker))
            {
                var LimitInPips = Math.Abs(
                    _marketsService.GetPriceInPips(trade.Broker, trade.InitialLimit.Value, trade.Market) -
                    _marketsService.GetPriceInPips(trade.Broker, price, trade.Market));
                trade.InitialLimitInPips = LimitInPips;
            }
            else
            {
                trade.InitialLimitInPips = null;
            }
        }

        public static void UpdateLimitPips(Trade trade)
        {
            if (trade.EntryPrice == null && trade.OrderPrice == null) return;

            var price = trade.EntryPrice ?? trade.OrderPrice.Value;

            // Update current Limit
            if (trade.LimitPrice != null && !string.IsNullOrEmpty(trade.Broker))
            {
                var LimitInPips = Math.Abs(
                    _marketsService.GetPriceInPips(trade.Broker, trade.LimitPrice.Value, trade.Market) -
                    _marketsService.GetPriceInPips(trade.Broker, price, trade.Market));
                trade.LimitInPips = LimitInPips;
            }
            else
            {
                trade.LimitInPips = null;
            }
        }

        public static void UpdateStop(Trade trade)
        {
            if (trade.StopPrices.Count == 0) return;

            if (trade.StopPrice != trade.StopPrices[trade.StopPrices.Count - 1].Price)
            {
                trade.StopPrice = trade.StopPrices[trade.StopPrices.Count - 1].Price;
                
                if (trade.StopPrices.Count == 1)
                {
                    trade.InitialStop = trade.StopPrice;
                }
            }
        }

        public static void UpdateInitialStopPips(Trade trade)
        {
            if (trade.EntryPrice == null && trade.OrderPrice == null) return;

            var price = trade.EntryPrice ?? trade.OrderPrice.Value;

            // Update current stop
            if (trade.InitialStop != null && !string.IsNullOrEmpty(trade.Broker))
            {
                var stopInPips = Math.Abs(
                    _marketsService.GetPriceInPips(trade.Broker, trade.InitialStop.Value, trade.Market) -
                    _marketsService.GetPriceInPips(trade.Broker, price, trade.Market));
                trade.InitialStopInPips = stopInPips;
            }
            else
            {
                trade.InitialStopInPips = null;
            }
        }

        public static void UpdateStopPips(Trade trade)
        {
            if (trade.EntryPrice == null && trade.OrderPrice == null) return;

            var price = trade.EntryPrice ?? trade.OrderPrice.Value;

            // Update current stop
            if (trade.StopPrice != null && !string.IsNullOrEmpty(trade.Broker))
            {
                var stopInPips = Math.Abs(
                    _marketsService.GetPriceInPips(trade.Broker, trade.StopPrice.Value, trade.Market) -
                    _marketsService.GetPriceInPips(trade.Broker, price, trade.Market));
                trade.StopInPips = stopInPips;
            }
            else
            {
                trade.StopInPips = null;
            }
        }

        private static IBroker GetBroker(Trade t)
        {
            lock (_tradeBrokerLookup)
            {
                if (_tradeBrokerLookup.TryGetValue(t, out var broker))
                {
                    return broker;
                }

                broker = _brokersService.GetBroker(t.Broker);
                _tradeBrokerLookup[t] = broker;
                return broker;
            }
        }
    }
}