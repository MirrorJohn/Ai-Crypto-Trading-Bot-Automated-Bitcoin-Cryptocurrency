using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;
using TraderTools.Simulation;

namespace TraderTools.Test.Strategies
{
    public class StrategyBuyCloseTwoMarkets : StrategyBase
    {
        public StrategyBuyCloseTwoMarkets()
        {
            SetMarkets("UP", "DOWN");
            SetTimeframes(Timeframe.H1);
        }

        public override void ProcessCandles(List<AddedCandleTimeframe> addedCandleTimeframes)
        {
            if (addedCandleTimeframes.All(c => c.Timeframe != Timeframe.H1)) return;

            var candlesUp = Candles["UP"][Timeframe.H1];
            var up = candlesUp[candlesUp.Count - 1];

            var candlesDown = Candles["DOWN"][Timeframe.H1];
            var down = candlesUp[candlesUp.Count - 1];

            if (up.CloseAsk == 4030F)
            {
                MarketLong("UP", Balance, limit: 6000M);
            }

            if (!Trades.AnyOpen && down.CloseAsk == 8030F)
            {
                MarketLong("DOWN", Balance, stop: 7000);
            }
        }
    }
}