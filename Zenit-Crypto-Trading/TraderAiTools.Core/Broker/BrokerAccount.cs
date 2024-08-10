using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Hallupa.Library;
using log4net;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;

namespace TraderTools.Core.Broker
{
    public class BrokerAccountUpdated
    {
        public BrokerAccountUpdated(BrokerAccount account)
        {
            Account = account;
        }

        public BrokerAccount Account { get; }
    }

    public class BrokerAccount : IBrokerAccount
    {
        #region Fields
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly AutoResetEvent _saveLock = new AutoResetEvent(true);
        private DateTime? _lastBackedUp = null;

        public string BrokerName { get; set; }

        public DateTime? AccountLastUpdated { get; set; }

        public string CustomJson { get; set; }

        public List<Trade> Trades { get; set; } = new List<Trade>();

        private Subject<BrokerAccountUpdated> _brokerAccountUpdatedSubject = new Subject<BrokerAccountUpdated>();

        public List<DepositWithdrawal> DepositsWithdrawals { get; set; } = new List<DepositWithdrawal>();
        #endregion

        public IObservable<BrokerAccountUpdated> AccountUpdatedObservable => _brokerAccountUpdatedSubject.AsObservable();

        public BrokerAccount()
        {
            DependencyContainer.ComposeParts(this);
        }

        public static BrokerAccount LoadAccount(IBroker broker, string mainDirectoryWithApplicationName)
        {
            var accountPath = Path.Combine(mainDirectoryWithApplicationName, "BrokerAccounts", $"{broker.Name}_Account.json");

            if (!File.Exists(accountPath))
            {
                return null;
            }

            var account = JsonConvert.DeserializeObject<BrokerAccount>(File.ReadAllText(accountPath));
            DependencyContainer.ComposeParts(account);

            foreach (var trade in account.Trades)
            {
                if (trade.DataVersion == 0)
                {
                    trade.DataVersion = Trade.CurrentDataVersion;
                }
            }

            return account;
        }

        public decimal GetBalance(DateTime? dateTimeUtc = null)
        {
            var depositWithdrawTotal = DepositsWithdrawals
                .Where(x => dateTimeUtc == null || x.Time <= dateTimeUtc)
                .Sum(x => x.Amount);

            if (dateTimeUtc == null)
            {
                return Trades.Where(x => x.NetProfitLoss != null).Sum(x => x.NetProfitLoss.Value) + depositWithdrawTotal;
            }

            var openTradesTotal = Trades
                .Where(x => x.EntryDateTime != null && x.CloseDateTime == null && x.NetProfitLoss != null)
                .Sum(x => x.NetProfitLoss.Value);
            var closedTradesTotal = Trades
                .Where(x => x.CloseDateTime != null && x.NetProfitLoss != null && x.CloseDateTime.Value <= dateTimeUtc)
                .Sum(x => x.NetProfitLoss.Value);
            return depositWithdrawTotal + openTradesTotal + closedTradesTotal;
        }

        public void SaveAccount(string mainDirectoryWithApplicationName)
        {
            _saveLock.WaitOne();
            var json = JsonConvert.SerializeObject(this);

            var mainPath = Path.Combine(mainDirectoryWithApplicationName, "BrokerAccounts");

            var t = Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(mainPath))
                    {
                        Directory.CreateDirectory(mainPath);
                    }

                    var accountFinalPath = Path.Combine(mainPath, $"{BrokerName}_Account.json");

                    if (_lastBackedUp == null || _lastBackedUp < DateTime.UtcNow.AddMinutes(-30))
                    {
                        // Rename backups
                        int maxBackups = 50;
                        for (var i = maxBackups; i >= 1; i--)
                        {
                            var accountBackupPath = Path.Combine(mainPath, $"{BrokerName}_Account_{i}.json");

                            if (i == maxBackups && File.Exists(accountBackupPath))
                            {
                                File.Delete(accountBackupPath);
                            }

                            if (i != maxBackups && File.Exists(accountBackupPath))
                            {
                                var accountNewBackupPath = Path.Combine(mainPath, $"{BrokerName}_Account_{i + 1}.json");
                                File.Move(accountBackupPath, accountNewBackupPath);
                            }
                        }

                        if (File.Exists(accountFinalPath))
                        {
                            var accountBackupPath = Path.Combine(mainPath, $"{BrokerName}_Account_1.json");
                            File.Move(accountFinalPath, accountBackupPath);
                        }

                        _lastBackedUp = DateTime.UtcNow;
                    }

                    var accountTmpPath = Path.Combine(mainPath, $"{BrokerName}_Account_tmp.json");
                    File.WriteAllText(accountTmpPath, json);

                    if (File.Exists(accountFinalPath)) File.Delete(accountFinalPath);

                    File.Copy(accountTmpPath, accountFinalPath);

                    File.Delete(accountTmpPath);
                }
                finally
                {

                    _saveLock.Set();
                }
            });
        }

        public void UpdateBrokerAccount(
            IBroker broker,
            IBrokersCandlesService candleService,
            IMarketDetailsService marketsService,
            ITradeDetailsAutoCalculatorService tradeCalculateService,
            UpdateOption option = UpdateOption.OnlyIfNotRecentlyUpdated)
        {
            void UpdateProgressAction(string txt)
            {
            }

            UpdateBrokerAccount(broker, candleService, marketsService, tradeCalculateService, UpdateProgressAction, option);
        }

        public void RecalculateTrade(Trade trade, IBrokersCandlesService candleService, IMarketDetailsService marketsService, IBroker broker)
        {
            // Update price per pip
            if (trade.EntryQuantity != null && trade.EntryDateTime != null)
            {

                trade.PricePerPip = candleService.GetGBPPerPip(marketsService, broker, trade.Market,
                    trade.EntryQuantity.Value, trade.EntryDateTime.Value, true);
            }

            // Update risk
            if (trade.InitialStopInPips == null || trade.PricePerPip == null)
            {
                trade.RiskPercentOfBalance = null;
                trade.RiskAmount = null;
                trade.RiskPercentOfBalance = null;
            }
            else
            {
                trade.RiskAmount = trade.PricePerPip.Value * trade.InitialStopInPips.Value;

                var balance = GetBalance(trade.StartDateTime);
                if (balance != 0.0M)
                {
                    var startTime = trade.OrderDateTime ?? trade.EntryDateTime;
                    trade.RiskPercentOfBalance = (trade.RiskAmount * 100M) / GetBalance(startTime);
                }
                else
                {
                    trade.RiskPercentOfBalance = null;
                }
            }
        }

        public void UpdateBrokerAccount(
            IBroker broker,
            IBrokersCandlesService candleService,
            IMarketDetailsService marketsService,
            ITradeDetailsAutoCalculatorService tradeCalculateService,
            Action<string> updateProgressAction,
            UpdateOption option = UpdateOption.OnlyIfNotRecentlyUpdated)
        {
            if (option == UpdateOption.OnlyIfNotRecentlyUpdated && (AccountLastUpdated != null && (DateTime.UtcNow - AccountLastUpdated.Value).TotalHours < 24))
            {
                return;
            }

            Log.Debug($"Updating {broker.Name} account");

            foreach (var t in Trades)
            {
                tradeCalculateService.RemoveTrade(t);
            }

            try
            {
                broker.UpdateAccount(this, candleService, marketsService, updateProgressAction, out var addedOrUpdatedTrades);

                foreach (var trade in addedOrUpdatedTrades)
                {
                    RecalculateTrade(trade, candleService, marketsService, broker);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Unable to update account", ex);
            }

            AccountLastUpdated = DateTime.UtcNow;

            Log.Debug($"Completed updating {broker.Name} trades");
            _brokerAccountUpdatedSubject.OnNext(new BrokerAccountUpdated(this));
        }

        public void SetTrades(List<Trade> trades, ITradeDetailsAutoCalculatorService tradeCalculateService, IBroker broker)
        {
            AccountLastUpdated = DateTime.UtcNow;

            foreach (var t in Trades)
            {
                tradeCalculateService.RemoveTrade(t);
            }

            Trades.Clear();
            Trades.AddRange(trades);

            Log.Debug($"Completed updating {broker.Name} trades");
            _brokerAccountUpdatedSubject.OnNext(new BrokerAccountUpdated(this));
        }

        public void RemoveTrade(Trade tradeDetails)
        {
            Trades.Remove(tradeDetails);
            _brokerAccountUpdatedSubject.OnNext(new BrokerAccountUpdated(this));
        }
    }
}