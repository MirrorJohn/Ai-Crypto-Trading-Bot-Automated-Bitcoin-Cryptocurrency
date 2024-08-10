using System;
using System.Collections.Generic;

namespace TraderTools.Basics.Extensions
{
    public enum CandleColour
    {
        White,
        Black,
        None
    }

    public static class CandleExtensions
    {
        public static CandleColour Colour(this Candle candle)
        {
            if (candle.CloseBid > candle.OpenBid)
            {
                return CandleColour.White;
            }

            if (candle.CloseBid < candle.OpenBid)
            {
                return CandleColour.Black;
            }

            return CandleColour.None;
        }

        public static DateTime OpenTime(this Candle candle)
        {
            return new DateTime(candle.OpenTimeTicks, DateTimeKind.Utc);
        }

        public static DateTime CloseTime(this Candle candle)
        {
            return new DateTime(candle.CloseTimeTicks, DateTimeKind.Utc);
        }

        public static void AddHeikinAshiCandles(this List<Candle> existingHeikinAshiCandles, IEnumerable<Candle> newNormalCandles)
        {
            foreach (var c in newNormalCandles)
            {
                existingHeikinAshiCandles.AddHeikinAshiCandle(c);
            }
        }

        public static void AddHeikinAshiCandle(this List<Candle> existingHeikinAshiCandles, Candle newNormalCandle)
        {
            var closeAsk = (float)(1.0 / 4.0) * (newNormalCandle.OpenAsk + newNormalCandle.HighAsk + newNormalCandle.LowAsk + newNormalCandle.CloseAsk);
            var closeBid = (float)(1.0 / 4.0) * (newNormalCandle.OpenBid + newNormalCandle.HighBid + newNormalCandle.LowBid + newNormalCandle.CloseBid);
            var openAsk = existingHeikinAshiCandles.Count > 0
                ? (float)(1.0 / 2.0) * (existingHeikinAshiCandles[existingHeikinAshiCandles.Count - 1].OpenAsk + existingHeikinAshiCandles[existingHeikinAshiCandles.Count - 1].CloseAsk)
                : (float)(1.0 / 2.0) * (newNormalCandle.OpenAsk + newNormalCandle.CloseAsk);
            var openBid = existingHeikinAshiCandles.Count > 0
                ? (float)(1.0 / 2.0) * (existingHeikinAshiCandles[existingHeikinAshiCandles.Count - 1].OpenBid + existingHeikinAshiCandles[existingHeikinAshiCandles.Count - 1].CloseBid)
                : (float)(1.0 / 2.0) * (newNormalCandle.OpenBid + newNormalCandle.CloseBid);
            var highAsk = Math.Max(newNormalCandle.HighAsk, Math.Max(closeAsk, openAsk));
            var highBid = Math.Max(newNormalCandle.HighBid, Math.Max(closeBid, openBid));
            var lowAsk = Math.Min(newNormalCandle.LowAsk, Math.Min(openAsk, closeAsk));
            var lowBid = Math.Min(newNormalCandle.LowBid, Math.Min(openBid, closeBid));

            existingHeikinAshiCandles.Add(new Candle
            {
                CloseAsk = closeAsk,
                CloseBid = closeBid,
                OpenAsk = openAsk,
                OpenBid = openBid,
                HighAsk = highAsk,
                HighBid = highBid,
                LowAsk = lowAsk,
                LowBid = lowBid,
                OpenTimeTicks = newNormalCandle.OpenTimeTicks,
                CloseTimeTicks = newNormalCandle.CloseTimeTicks,
                IsComplete = newNormalCandle.IsComplete
            });
        }

        public static List<Candle> CreateHeikinAshiCandles(this List<Candle> candles)
        {
            var hk = new List<Candle>();
            hk.AddHeikinAshiCandles(candles);

            return hk;
        }
    }
}