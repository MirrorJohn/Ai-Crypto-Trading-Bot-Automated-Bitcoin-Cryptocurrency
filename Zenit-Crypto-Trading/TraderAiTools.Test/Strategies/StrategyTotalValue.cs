using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;
using TraderTools.Simulation;

namespace TraderTools.Test.Strategies
{
    public class StrategyTotalValue : StrategyBase
    {
        public Trade Trade1 { get; private set; }
        public Trade Trade2 { get; private set; }
        public Dictionary<long, decimal> TotalValuesBeforeTrades { get; } = new Dictionary<long, decimal>();
        public Dictionary<long, decimal> TotalValuesAfterTrades { get; } = new Dictionary<long, decimal>();

        public StrategyTotalValue()
        {
            SetMarkets("UP");
            SetTimeframes(Timeframe.H1);
        }

        public override void ProcessCandles(List<AddedCandleTimeframe> addedCandleTimeframes)
        {
            if (addedCandleTimeframes.All(c => c.Timeframe != Timeframe.H1)) return;

            var candlesUp = Candles["UP"][Timeframe.H1];
            var up = candlesUp[candlesUp.Count - 1];

            TotalValuesBeforeTrades.Add(up.CloseTimeTicks, TotalValue);

            if (up.CloseTimeTicks == 3500)
            {
                Trade1 = MarketLong("UP", 3);
            }

            if (up.CloseTimeTicks == 4500)
            {
                Trade2 = MarketLong("UP", 2);
            }

            if (up.CloseTimeTicks == 7500)
            {
                CloseTrade(Trade1);
            }

            TotalValuesAfterTrades.Add(up.CloseTimeTicks, TotalValue);
        }
    }
}