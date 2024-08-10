using System;
using System.Collections.Generic;
using Hallupa.TraderTools.Basics;

namespace TraderTools.Basics
{
    public enum UpdateOption
    {
        OnlyIfNotRecentlyUpdated,
        ForceUpdate
    }

    public interface IBrokerAccount
    {
        List<Trade> Trades { get; set; }

        DateTime? AccountLastUpdated { get; set; }

        string CustomJson { get; set; }

        List<DepositWithdrawal> DepositsWithdrawals { get; set; }

        void SaveAccount(string mainDirectoryWithApplicationName);

        void UpdateBrokerAccount(
            IBroker broker,
            IBrokersCandlesService candleService,
            IMarketDetailsService marketsService,
            ITradeDetailsAutoCalculatorService tradeCalculateService,
            UpdateOption option = UpdateOption.OnlyIfNotRecentlyUpdated);

        void UpdateBrokerAccount(
            IBroker broker,
            IBrokersCandlesService candleService,
            IMarketDetailsService marketsService,
            ITradeDetailsAutoCalculatorService tradeCalculateService,
            Action<string> updateProgressAction,
            UpdateOption option = UpdateOption.OnlyIfNotRecentlyUpdated);

        void RecalculateTrade(Trade trade, IBrokersCandlesService candleService, IMarketDetailsService marketsService, IBroker broker);
    }
}