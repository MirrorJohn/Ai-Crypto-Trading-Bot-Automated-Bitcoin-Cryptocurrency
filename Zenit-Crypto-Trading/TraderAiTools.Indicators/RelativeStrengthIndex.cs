using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class RelativeStrengthIndex : LengthIndicator
    {
        private readonly SmoothedMovingAverage _gain;
        private readonly SmoothedMovingAverage _loss;
        private bool _isInitialized;
        private double _last;

        public RelativeStrengthIndex()
        {
            _gain = new SmoothedMovingAverage();
            _loss = new SmoothedMovingAverage();

            Length = 14;
            _loss.Length = _gain.Length = Length;
        }

        /// <summary>
        /// Whether the indicator is set.
        /// </summary>
        public override bool IsFormed => _gain.IsFormed;

        public override string Name => "RSI";


        public override SignalAndValue Process(Candle candle)
        {
            var newValue = candle.CloseBid;

            if (!_isInitialized)
            {
                if (candle.IsComplete == 1)
                {
                    _last = newValue;
                    _isInitialized = true;
                }

                return new SignalAndValue(0F, false);
            }

            var delta = newValue - _last;

            var gainValue = _gain.Process(new Candle
            {
                CloseBid = (float)(delta > 0 ? delta : 0.0),
                IsComplete = candle.IsComplete
            });
            var lossValue = _loss.Process(new Candle
            {
                CloseBid = (float)(delta > 0 ? 0.0 : -delta),
                IsComplete = candle.IsComplete
            });

            if (candle.IsComplete == 1)
                _last = newValue;

            if (lossValue.Value.Equals(0.0F))
                return new SignalAndValue((float)100.0, IsFormed);

            if ((gainValue.Value / lossValue.Value).Equals(1F))
                return new SignalAndValue((float)0.0, IsFormed);

            return new SignalAndValue((float)(100.0 - 100.0 / (1.0 + gainValue.Value / lossValue.Value)), IsFormed);
        }
    }
}