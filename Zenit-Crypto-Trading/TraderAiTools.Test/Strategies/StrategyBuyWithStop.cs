using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;
using TraderTools.Simulation;

namespace TraderTools.Test.Strategies
{
    public class StrategyBuyWithStop : StrategyBase
    {
        public StrategyBuyWithStop()
        {
            SetMarkets("DOWN");
            SetTimeframes(Timeframe.H1);
        }

        public override void ProcessCandles(List<AddedCandleTimeframe> addedCandleTimeframes)
        {
            if (addedCandleTimeframes.All(c => c.Timeframe != Timeframe.H1)) return;

            var candles = Candles["DOWN"][Timeframe.H1];
            var candle = candles[candles.Count - 1];
            if (candle.OpenAsk >= 15000 && !Trades.AnyOpen)
            {
                MarketLong("DOWN", Balance, 7000M);
            }
        }
    }
}