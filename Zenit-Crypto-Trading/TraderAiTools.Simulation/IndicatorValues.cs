using System;
using System.Collections.Generic;
using TraderTools.Basics;

namespace TraderTools.Simulation
{
    public class IndicatorValues
    {
        private float _value;

        // Value and time in ticks
        public List<(long TimeTicks, float? Value)> Values { get; } = new List<(long TimeTicks, float? Value)>(50000);

        public float Value
        {
            get
            {
                if (!HasValue) throw new ApplicationException("Indicator has no value. Check HasValue before calling Value");
                return _value;
            }
            set => _value = value;
        }

        public bool HasValue { get; set; }

        public void AddValue(SignalAndValue signalAndValue, Candle candle)
        {
            if (signalAndValue.IsFormed)
            {
                Values.Add((candle.CloseTimeTicks, signalAndValue.Value));
                Value = signalAndValue.Value;
                HasValue = true;
            }
            else
            {
                Values.Add((candle.CloseTimeTicks, null));
                Value = 0F;
                HasValue = false;
            }
        }

        public float? this[int i] => Values[i].Value;

        public bool IsFormed
        {
            get
            {
                if (Values.Count == 0) return false;
                return Values[^1].Value != null;
            }
        }

        public int Count => Values.Count;
    }
}