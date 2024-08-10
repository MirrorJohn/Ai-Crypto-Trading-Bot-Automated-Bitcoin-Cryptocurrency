using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Hallupa.Library;
using log4net;

namespace TraderTools.Basics.Helpers
{
    public static class CandlesHelper
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static Candle? GetFirstCandleThatClosesBeforeDateTime(IList<Candle> candles, DateTime dateTime)
        {
            // Candles will be ordered in ascending date order
            for (var i = candles.Count - 1; i >= 0; i--)
            {
                var c = candles[i];
                if (c.CloseTimeTicks <= dateTime.Ticks)
                {
                    return c;
                }
            }

            return null;
        }

        public static void UpdateCandles(IBroker broker, IBrokersCandlesService candlesService,
            IEnumerable<string> markets, IEnumerable<Timeframe> timeframes, int threads = 3, Action<string> updateProgressAction = null)
        {
            var completed = 0;
            var total = 0;
            var producerConsumer =
                new ProducerConsumer<(string Market, Timeframe Timeframe)>(threads,
                    d =>
                    {
                        Log.Info($"Updating {d.Data.Timeframe} candles for {d.Data.Market}");
                        candlesService.UpdateCandles(broker, d.Data.Market, d.Data.Timeframe);
                        candlesService.UnloadCandles(d.Data.Market, d.Data.Timeframe, broker);
                        Log.Info($"Updated {d.Data.Timeframe} candles for {d.Data.Market}");

                        Interlocked.Increment(ref completed);

                        updateProgressAction?.Invoke($"Updated {completed}/{total} markets/timeframes");

                        return ProducerConsumerActionResult.Success;
                    });


            foreach (var market in markets)
            {
                foreach (var timeframe in timeframes)
                {
                    total++;
                    producerConsumer.Add((market, timeframe));
                }
            }

            updateProgressAction?.Invoke($"Updating {total} markets/timeframes");

            producerConsumer.SetProducerCompleted();
            producerConsumer.Start();
            producerConsumer.WaitUntilConsumersFinished();
            Log.Info("Updated FX candles");

        }
    }
}