using System.Collections.Generic;

namespace TraderTools.Simulation
{
    public class ReadOnlyTradeCollection
    {
        private readonly TradeWithIndexingCollection _trades;

        public ReadOnlyTradeCollection(TradeWithIndexingCollection trades)
        {
            _trades = trades;
        }

        public IEnumerable<IReadOnlyTrade> OpenTrades => _trades.OpenTrades;
        public IEnumerable<IReadOnlyTrade> AllTrades => _trades.AllTrades;
        public bool AnyOpen => _trades.AnyOpen;

    }
}