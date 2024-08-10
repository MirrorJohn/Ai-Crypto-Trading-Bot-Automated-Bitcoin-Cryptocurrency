using System.Collections.Generic;

namespace TraderTools.Basics.Extensions
{
    public static class IndicatorExtension
    {
        public static List<SignalAndValue> ProcessIndicator(this IIndicator indicator, List<Candle> candles)
        {
            var ret = new List<SignalAndValue>();
            foreach (var c in candles)
            {
                ret.Add(indicator.Process(c));
            }

            return ret;
        }
    }
}