using System.Collections.Generic;

namespace TraderTools.Basics
{
    public interface IBrokersService
    {
        void AddBrokers(IEnumerable<IBroker> brokers);

        void Connect();

        void LoadBrokerAccounts(ITradeDetailsAutoCalculatorService tradeCalculatorService, string mainDirectoryWithApplicationName);

        IBroker GetBroker(string broker);

        List<IBroker> Brokers { get; }

        Dictionary<IBroker, IBrokerAccount> AccountsLookup { get; }
    }
}