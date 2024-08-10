using System.Resources;

namespace TraderTools.Basics
{
    public interface IIndicator
    {
        bool IsFormed { get; }

        string Name { get; }

        SignalAndValue Process(Candle candle);

        void Reset();
    }
}