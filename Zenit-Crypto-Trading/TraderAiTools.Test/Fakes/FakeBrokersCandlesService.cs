using System;
using System.Collections.Generic;
using TraderTools.Basics;

namespace TraderTools.Test.Fakes
{
    public class FakeBrokersCandlesService : IBrokersCandlesService
    {
        public List<Candle> GetCandles(IBroker broker, string market, Timeframe timeframe, bool updateCandles, DateTime? minOpenTimeUtc = null,
            DateTime? maxCloseTimeUtc = null, bool cacheData = true, bool forceUpdate = false, Action<string> progressUpdate = null,
            bool saveCandles = true)
        {
            if (market == "UP")
            {
                return GetUpCandles();
            }

            if (market == "DOWN")
            {
                return GetDownCandles();
            }

            throw new ApplicationException("Market not found");
        }

        private static List<Candle> GetUpCandles()
        {
            var ret = new List<Candle>();
            var price = 1000F;
            for (long dateTicks = 1000; dateTicks <= 10000; dateTicks += 1000)
            {
                ret.Add(new Candle
                {
                    CloseAsk = price + 30F,
                    CloseBid = price + 30F - 10F,
                    CloseTimeTicks = dateTicks + 500,
                    OpenTimeTicks = dateTicks,
                    HighAsk = price + 50F,
                    HighBid = price + 50F - 10F,
                    IsComplete = 1,
                    LowAsk = price - 50F,
                    LowBid = price - 50F - 10F,
                    OpenAsk = price,
                    OpenBid = price - 10F
                });

                price += 1000F;
            }

            return ret;
        }

        private static List<Candle> GetDownCandles()
        {
            var ret = new List<Candle>();
            var price = 15000F;
            for (long dateTicks = 1000; dateTicks <= 10000; dateTicks += 1000)
            {
                ret.Add(new Candle
                {
                    CloseAsk = price + 30F,
                    CloseBid = price + 30F - 10F,
                    CloseTimeTicks = dateTicks + 1000,
                    OpenTimeTicks = dateTicks,
                    HighAsk = price + 50F,
                    HighBid = price + 50F - 10F,
                    IsComplete = 1,
                    LowAsk = price - 50F,
                    LowBid = price - 50F - 10F,
                    OpenAsk = price,
                    OpenBid = price - 10F
                });

                price -= 1000F;
            }

            return ret;
        }


        public void UpdateCandles(IBroker broker, string market, Timeframe timeframe, bool forceUpdate = true,
            bool saveCandles = true)
        {
            throw new NotImplementedException();
        }

        public void UnloadCandles(string market, Timeframe timeframe, IBroker broker)
        {
            throw new NotImplementedException();
        }

        public string GetBrokerCandlesPath(IBroker broker, string market, Timeframe timeframe)
        {
            throw new NotImplementedException();
        }
    }
}