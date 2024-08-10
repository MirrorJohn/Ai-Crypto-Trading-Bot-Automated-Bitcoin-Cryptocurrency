using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;
using TraderTools.Simulation;

namespace TraderTools.Test.Strategies
{
    public class StrategyBuyThenClose : StrategyBase
    {
        public StrategyBuyThenClose()
        {
            SetMarkets("UP");
            SetTimeframes(Timeframe.H1);
        }

        public override void ProcessCandles(List<AddedCandleTimeframe> addedCandleTimeframes)
        {
            if (addedCandleTimeframes.All(c => c.Timeframe != Timeframe.H1)) return;

            var candles = Candles["UP"][Timeframe.H1];
            var candle = candles[candles.Count - 1];
            if (candle.OpenAsk >= 3000 && !Trades.AnyOpen && !Trades.AllTrades.Any())
            {
                MarketLong("UP", Balance);
            }

            if (candle.OpenAsk >= 5000 && Trades.AnyOpen)
            {
                CloseTrade(Trades.OpenTrades.First());
            }
        }
    }
}