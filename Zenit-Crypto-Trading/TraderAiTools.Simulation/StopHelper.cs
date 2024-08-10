using System;
using Hallupa.Library.Extensions;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;

namespace TraderTools.Simulation
{
    /// <summary>
    /// https://help.fxcm.com/markets/Trading/Execution-Rollover/Order-Types/876471181/What-is-a-Trailing-Stop-and-how-do-I-place-it.htm
    /// </summary>
    public static class StopHelper
    {
        public static void TrailIndicatorValues(Trade trade, Candle candle, IndicatorValues indicatorValues)
        {
            if (trade.EntryDateTime == null || trade.EntryPrice == null || !indicatorValues.HasValue || trade.InitialStop == null) return;

            // Get current
            var currentIndicatorValue = (decimal)indicatorValues.Value;

            // Get value at the time of the trade
            var index = indicatorValues.Values.BinarySearchGetItem(i => indicatorValues.Values[i].TimeTicks, 0, trade.EntryDateTime.Value.Ticks, BinarySearchMethod.PrevLowerValueOrValue);
            if (index == -1) return;


            var initialIndicator = indicatorValues[index];

            if (initialIndicator == null) return;

            var initialIndicatorValue = initialIndicator.Value;

            var stopDist = Math.Abs((decimal)initialIndicatorValue - trade.InitialStop.Value);
            var newStop = trade.TradeDirection == TradeDirection.Long
                ? currentIndicatorValue - stopDist
                : currentIndicatorValue + stopDist;

            if (trade.StopPrice != newStop)
            {
                if (trade.TradeDirection == TradeDirection.Long && newStop > trade.StopPrice)
                {
                    trade.AddStopPrice(candle.CloseTime(), newStop);
                }
                else if (trade.TradeDirection == TradeDirection.Short && newStop < trade.StopPrice)
                {
                    trade.AddStopPrice(candle.CloseTime(), newStop);
                }
            }
        }
    }
}