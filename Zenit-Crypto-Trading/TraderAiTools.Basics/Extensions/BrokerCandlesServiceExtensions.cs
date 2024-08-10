using System;
using System.Collections.Generic;
using Hallupa.Library.Extensions;

namespace TraderTools.Basics.Extensions
{
    public static class BrokerCandlesServiceExtensions
    {
        /// <summary>
        /// Uses a faster mechanism for finding candles.
        /// </summary>
        /// <param name="service"></param>
        /// <param name="market"></param>
        /// <param name="broker"></param>
        /// <param name="timeframe"></param>
        /// <param name="dateTime"></param>
        /// <param name="updateCandles"></param>
        /// <returns></returns>
        public static Candle? GetLastClosedCandle(this IBrokersCandlesService service, string market, IBroker broker, Timeframe timeframe, DateTime dateTime, bool updateCandles = false)
        {
            var candles = service.GetCandles(broker, market, timeframe, updateCandles);
            if (candles == null || candles.Count == 0)
            {
                return null;
            }

            var index = candles.BinarySearchGetItem(
                i => candles[i].CloseTimeTicks, 0, dateTime.Ticks,
                BinarySearchMethod.PrevLowerValueOrValue);
            if (index != -1) return candles[index];

            return null;
        }

        public static List<Candle> GetCandlesUptoSpecificTime(this IBrokersCandlesService brokerCandles,
            IBroker broker, string market,
            Timeframe timeframe, bool updateCandles, DateTime? startUtc, DateTime? endUtc,
            Timeframe smallestTimeframeForPartialCandle = Timeframe.M1)
        {
            var allLargeChartCandles = brokerCandles.GetCandles(broker, market, timeframe, updateCandles, cacheData: false, minOpenTimeUtc: startUtc, maxCloseTimeUtc: endUtc);
            var smallestTimeframeCandles = brokerCandles.GetCandles(broker, market, smallestTimeframeForPartialCandle, updateCandles, cacheData: false, maxCloseTimeUtc: endUtc);

            var largeChartCandles = new List<Candle>();
            var endTicks = endUtc?.Ticks ?? -1;
            var endTimeTicks = endUtc?.Ticks;

            // Add complete candle
            for (var i = 0; i < allLargeChartCandles.Count; i++)
            {
                var currentCandle = allLargeChartCandles[i];
                if (endTimeTicks == null || currentCandle.CloseTimeTicks <= endTimeTicks)
                {
                    largeChartCandles.Add(currentCandle);
                }
            }

            // Add incomplete candle
            var latestCandleTimeTicks = largeChartCandles[largeChartCandles.Count - 1].CloseTimeTicks;
            float? openBid = null, closeBid = null, highBid = null, lowBid = null;
            float? openAsk = null, closeAsk = null, highAsk = null, lowAsk = null;
            long? openTimeTicks = null, closeTimeTicks = null;

            foreach (var smallestTimeframeCandle in smallestTimeframeCandles)
            {
                if (smallestTimeframeCandle.OpenTimeTicks >= latestCandleTimeTicks && (smallestTimeframeCandle.CloseTimeTicks <= endTicks || endTicks == -1))
                {
                    if (openTimeTicks == null) openTimeTicks = smallestTimeframeCandle.OpenTimeTicks;

                    if (openBid == null || smallestTimeframeCandle.OpenBid < openBid) openBid = smallestTimeframeCandle.OpenBid;
                    if (highBid == null || smallestTimeframeCandle.HighBid > highBid) highBid = smallestTimeframeCandle.HighBid;
                    if (lowBid == null || smallestTimeframeCandle.LowBid < lowBid) lowBid = smallestTimeframeCandle.LowBid;
                    closeBid = smallestTimeframeCandle.CloseBid;

                    if (openAsk == null || smallestTimeframeCandle.OpenAsk < openAsk) openAsk = smallestTimeframeCandle.OpenAsk;
                    if (highAsk == null || smallestTimeframeCandle.HighAsk > highAsk) highAsk = smallestTimeframeCandle.HighAsk;
                    if (lowAsk == null || smallestTimeframeCandle.LowAsk < lowAsk) lowAsk = smallestTimeframeCandle.LowAsk;
                    closeAsk = smallestTimeframeCandle.CloseAsk;

                    closeTimeTicks = smallestTimeframeCandle.CloseTimeTicks;
                }

                if (smallestTimeframeCandle.CloseTime() > endUtc)
                {
                    break;
                }
            }

            if (openBid != null)
            {
                largeChartCandles.Add(new Candle
                {
                    OpenBid = openBid.Value,
                    CloseBid = closeBid.Value,
                    HighBid = highBid.Value,
                    LowBid = lowBid.Value,
                    OpenAsk = openAsk.Value,
                    CloseAsk = closeAsk.Value,
                    HighAsk = highAsk.Value,
                    LowAsk = lowAsk.Value,
                    CloseTimeTicks = closeTimeTicks.Value,
                    OpenTimeTicks = openTimeTicks.Value,
                    IsComplete = 0
                });
            }

            return largeChartCandles;
        }

        /// <summary>
        /// For most currency pairs, the 'pip' location is the fourth decimal place. In this example, if the GBP/USD moved from 1.42279 to 1.42289 you would have gained or lost one pip
        /// http://help.fxcm.com/markets/Trading/Education/Trading-Basics/32856512/How-to-calculate-PIP-value.htm
        /// </summary>
        /// <param name="price"></param>
        /// <returns></returns>
        public static decimal GetPriceInPips(this IMarketDetailsService marketsService, string brokerName, decimal price, string market)
        {
            var pipInDecimals = marketsService.GetOnePipInDecimals(brokerName, market);

            return pipInDecimals != 0M ? price / pipInDecimals : 0;
        }

        public static decimal GetPriceFromPips(this IMarketDetailsService marketsService, string brokerName, decimal pips, string market)
        {
            var pipInDecimals = marketsService.GetOnePipInDecimals(brokerName, market);

            return pips * pipInDecimals;
        }

        public static decimal GetGBPPerPip(
            this IBrokersCandlesService candleService,
            IMarketDetailsService marketsService,
            IBroker broker, string market, decimal lotSize,
            DateTime date, bool updateCandles)
        {
            var marketDetails = marketsService.GetMarketDetails(broker.Name, market);
            decimal price = 0M;

            // If market contains GBP, then use the market for the price
            if (market.Contains("GBP"))
            {
                price = (decimal)candleService.GetLastClosedCandle(market, broker, Timeframe.D1, date, updateCandles).Value.OpenBid;

                if (market.StartsWith("GBP"))
                {
                    price = 1M / price;
                }
            }
            else
            {
                // Try to get GBP candle, if it exists
                var marketForPrice = !market.Contains("/") ? $"GBP/{marketDetails.Currency}" : $"GBP/{market.Split('/')[1]}";

                if (!marketsService.HasMarketDetails(broker.Name, marketForPrice))
                {
                    marketForPrice = $"{marketForPrice.Split('/')[1]}/{marketForPrice.Split('/')[0]}";
                }

                if (marketForPrice == "GBP/GBP")
                {
                    price = 1M;
                }
                else
                {
                    // Get candle price, if it exists
                    if (marketsService.HasMarketDetails(broker.Name, marketForPrice))
                    {
                        price = (decimal)candleService.GetLastClosedCandle(marketForPrice, broker, Timeframe.D1, date, updateCandles).Value.OpenBid;
                    }
                    else
                    {
                        // Otherwise, try to get the USD candle and convert back to GBP
                        // Try to get $ candle and convert to £
                        var usdCandle = candleService.GetLastClosedCandle($"USD/{market.Split('/')[1]}", broker, Timeframe.D1, date, updateCandles);
                        var gbpUSDCandle = candleService.GetLastClosedCandle("GBP/USD", broker, Timeframe.D1, date, updateCandles);
                        price = (decimal)gbpUSDCandle.Value.OpenBid / (decimal)usdCandle.Value.OpenBid;
                    }
                }

                if (marketForPrice.StartsWith("GBP"))
                {
                    price = 1M / price;
                }

            }

            return lotSize * (decimal)marketDetails.ContractMultiplier * (decimal)marketDetails.PointSize * price;
        }

        public static decimal GetOnePipInDecimals(this IMarketDetailsService marketsService, string broker, string market)
        {
            if (marketsService == null) return 0M;

            var marketDetails = marketsService.GetMarketDetails(broker, market);

            return marketDetails.PointSize.Value;
        }
    }
}