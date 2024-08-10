using System;
using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    class StandardDeviation : LengthIndicator
    {
        private readonly SimpleMovingAverage _sma;

        public StandardDeviation()
            : this(10)
        {

        }

        public StandardDeviation(int length)
        {
            _sma = new SimpleMovingAverage(length);
            Length = length;
        }

        public override string Name => "Std Deviation";

        public override bool IsFormed => _sma.IsFormed;

        public override SignalAndValue Process(Candle candle)
        {
            var newValue = candle.CloseAsk;
            var smaValue = _sma.Process(newValue, candle.IsComplete == 1);

            var buff = Buffer;

            if (candle.IsComplete == 0)
            {
                buff = Buffer.ToList();
            }
            
            buff.Add(newValue);

            if (buff.Count > Length)
                buff.RemoveAt(0);
            

            var std = buff.Select(t1 => t1 - smaValue.Value).Select(t => t * t).Sum();

            var ret = new SignalAndValue((float)Math.Sqrt((double)(std / Length)), IsFormed);
            CurrentValue = ret.Value;
            return ret;
        }

        public float CurrentValue { get; set; }
    }
}