using System;

namespace TraderTools.Basics
{
    [Flags]
    public enum CalculateOptions
    {
        Default = 0,
        ExcludePipsCalculations = 1,
        IncludeOpenTradesInRMultipleCalculation = 2
    }

    public interface ITradeDetailsAutoCalculatorService
    {
        void RemoveTrade(Trade trade);
        void RecalculateTrade(Trade trade);
    }
}