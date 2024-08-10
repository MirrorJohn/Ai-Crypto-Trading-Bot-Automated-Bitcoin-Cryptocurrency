using System;
using TraderTools.Basics;

namespace TraderTools.Simulation
{
    public class TfTwoConfirmedAction<T> where T : class
    {
        private readonly Timeframe _tf1;
        private readonly Timeframe _tf2;
        private readonly Func<Candle, (T Context, bool Confirmed)> _getTf1ConfirmAction;
        private readonly Func<Candle, (T Context, bool Confirmed)> _getTf2ConfirmAction;
        private readonly Action _timeframesConfirmedAction;
        public T Tf1Context { get; private set; }
        public bool Tf1Confirmed { get; private set; }

        public TfTwoConfirmedAction(
            Timeframe tf1,
            Timeframe tf2,
            Func<Candle, (T Context, bool Confirmed)> getTf1ConfirmAction,
            Func<Candle, (T Context, bool Confirmed)> getTf2ConfirmAction,
            Action timeframesConfirmedAction)
        {
            _tf1 = tf1;
            _tf2 = tf2;
            _getTf1ConfirmAction = getTf1ConfirmAction;
            _getTf2ConfirmAction = getTf2ConfirmAction;
            _timeframesConfirmedAction = timeframesConfirmedAction;
        }

        public TradeDirection? CurrentDirection { get; private set; } = null;
        public TradeDirection? ConfirmedCurrentDirection { get; private set; } = null;

        public void ProcessCandle(Candle candle, Timeframe tf)
        {
            if (tf == _tf1)
            {
                var r = _getTf1ConfirmAction(candle);
                Tf1Context = r.Context;
                Tf1Confirmed = r.Confirmed;
            }
            
            if (tf == _tf2 && Tf1Confirmed)
            {
                var r = _getTf2ConfirmAction(candle);

                if (r.Confirmed)
                {
                    _timeframesConfirmedAction();
                    Tf1Context = null;
                    Tf1Confirmed = false;
                }
            }
        }
    }
}