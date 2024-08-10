using System.Linq;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class SimpleMovingAverage : LengthIndicator
    {
        public SimpleMovingAverage()
        {
            Length = 32;
        }

        public SimpleMovingAverage(int length)
        {
            Length = length;
        }

        public override string Name => $"SMA{Length}";

        public override SignalAndValue Process(Candle candle)
        {
            var newValue = candle.CloseBid;
            return Process(newValue, candle.IsComplete == 1);
        }

        public SignalAndValue Process(float value, bool isComplete)
        {
            var buff = Buffer;
            if (!isComplete)
            {
                // Take copy
                buff = Buffer.ToList();
            }

            buff.Add(value);

            if (buff.Count > Length)
                buff.RemoveAt(0);

            SignalAndValue ret;
            ret = new SignalAndValue(Buffer.Sum() / Length, IsFormed);

            CurrentValue = ret.Value;
            return ret;
        }

        public float CurrentValue { get; set; }
    }
}