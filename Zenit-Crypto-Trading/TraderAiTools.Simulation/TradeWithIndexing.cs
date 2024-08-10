using System;
using TraderTools.Basics;

namespace TraderTools.Simulation
{
    public interface IReadOnlyTrade
    {
        public string Market { get; }
        public DateTime? CloseDateTime { get; }
    }

    public class TradeWithIndexing : IReadOnlyTrade
    {
        public Trade Trade { get; set; }
        public int LimitIndex { get; set; } = -1;
        public int StopIndex { get; set; } = -1;
        public int OrderIndex { get; set; } = -1;
        public TradeWithIndexing Next { get; set; }
        public TradeWithIndexing Prev { get; set; }

        public string Market => Trade.Market;
        public DateTime? CloseDateTime => Trade.CloseDateTime;
    }
}