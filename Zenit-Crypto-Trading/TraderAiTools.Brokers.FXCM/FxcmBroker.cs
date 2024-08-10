using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using com.fxcm.report;
using fxcore2;
using Hallupa.Library;
using Hallupa.TraderTools.Basics;
using log4net;
using Newtonsoft.Json;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;

namespace TraderTools.Brokers.FXCM
{
    public class CustomJson
    {
        public Dictionary<string, DateTime> LastUpdateTime { get; set; } = new Dictionary<string, DateTime>();
    }

    public class FxcmBroker : IDisposable, IBroker
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private bool disposedValue = false;
        private O2SessionStatus _sessionStatus;
        private SessionStatusListener _sessionStatusListener;
        private Random _rnd;
        private string _user;
        private string _password;
        private string _connection;

        public FxcmBroker()
        {
            _rnd = new Random();
        }

        internal O2GSession Session { get; private set; }

        public List<MarketDetails> GetMarketDetailsList()
        {
            if (Status != ConnectStatus.Connected) return null;

            var loginRules = Session.getLoginRules();
            var offersResponse = loginRules.getTableRefreshResponse(O2GTableType.Offers);
            var factory = Session.getResponseReaderFactory();
            if (factory == null) return null;

            var accountsResponse = loginRules.getTableRefreshResponse(O2GTableType.Accounts);
            var accountsReader = factory.createAccountsTableReader(accountsResponse);
            var tradingSettingsProvider = loginRules.getTradingSettingsProvider();
            var account = accountsReader.getRow(0);
            var tableManager = GetTableManager();
            var readerFactory = Session.getResponseReaderFactory();
            var response = loginRules.getTableRefreshResponse(O2GTableType.Offers);
            var responseReader = readerFactory.createOffersTableReader(response);

            var ret = new List<MarketDetails>();
            for (int i = 0; i < responseReader.Count; i++)
            {
                var offerRow = responseReader.getRow(i);

                ret.Add(new MarketDetails(Name, offerRow.Instrument,
                    offerRow.ContractCurrency, (decimal?)offerRow.PointSize,
                    offerRow.Digits, tradingSettingsProvider.getMinQuantity(offerRow.Instrument, account),
                    (decimal?)offerRow.ContractMultiplier));
            }

            return ret;
        }

        public Candle? GetSingleCandle(string market, Timeframe timeframe, DateTime date)
        {
            throw new NotImplementedException();
        }

        public void SetUsernamePassword(string user, string password, string connection)
        {
            _user = user;
            _password = password;
            _connection = connection;
        }

        public void Connect()
        {
            Log.Info("FXCM connecting");

            Session = O2GTransport.createSession();
            _sessionStatusListener = new SessionStatusListener(Session, "", "");
            Session.useTableManager(O2GTableManagerMode.Yes, null);
            Session.subscribeSessionStatus(_sessionStatusListener);

            _sessionStatusListener.Reset();
            Session.login(_user, _password, "http://www.fxcorporate.com/Hosts.jsp", "Real");
            if (_sessionStatusListener.WaitEvents() && _sessionStatusListener.Connected)
            {
                Log.Info("FXCM Connected");
            }
            else if (!_sessionStatusListener.Connected)
            {
                Log.Error("Unable to connect to FXCM");
            }
        }

        public ConnectStatus Status
        {
            get
            {
                var currentStatus = Session?.getChartSessionStatus();

                switch (currentStatus)
                {
                    case null:
                        return ConnectStatus.Disconnected;

                    case O2GChartSessionStatusCode.Connected:
                        return ConnectStatus.Connected;

                    case O2GChartSessionStatusCode.Connecting:
                        return ConnectStatus.Connecting;

                    case O2GChartSessionStatusCode.Disconnecting:
                        return ConnectStatus.Disconnecting;
                }

                return ConnectStatus.Disconnected;
            }
        }

        public BrokerKind Kind => BrokerKind.SpreadBet;

        public bool IncludeReportInUpdates { get; set; } = true;

        public string Name => "FXCM";

        private O2GTableManager GetTableManager()
        {
            var tableManager = Session.getTableManager();
            if (tableManager == null)
            {
                Disconnect();
                return null;
            }

            var managerStatus = tableManager.getStatus();
            while (managerStatus == O2GTableManagerStatus.TablesLoading)
            {
                Thread.Sleep(50);
                managerStatus = tableManager.getStatus();
            }

            if (managerStatus == O2GTableManagerStatus.TablesLoadFailed)
            {
                throw new Exception("Cannot refresh all tables of table manager");
            }

            return tableManager;
        }

        public Dictionary<string, AssetBalance> GetBalance(DateTime? dateTimeUtc = null)
        {
            throw new NotImplementedException();
        }

        public List<string> GetSymbols()
        {
            throw new NotImplementedException();
        }

        public bool UpdateAccount(IBrokerAccount account, IBrokersCandlesService candlesService,
            IMarketDetailsService marketsService, Action<string> updateProgressAction, out List<Trade> addedOrUpdatedTrades)
        {
            var tableManager = GetTableManager();
            addedOrUpdatedTrades = new List<Trade>();

            if (tableManager == null)
            {
                return false;
            }

            var custom = new CustomJson();
            if (!string.IsNullOrEmpty(account.CustomJson))
            {
                custom = JsonConvert.DeserializeObject<CustomJson>(account.CustomJson);
            }

            // Get open trades
            Log.Debug("Getting open trades");
            updateProgressAction?.Invoke("Getting open trades");
            var updated = GetOpenTrades(account, candlesService, marketsService, tableManager, out var openTrades, addedOrUpdatedTrades);

            Log.Debug("Getting recently closed trades");
            updateProgressAction?.Invoke("Getting recently closed trades");
            updated = GetClosedTrades(account, candlesService, marketsService, tableManager, addedOrUpdatedTrades) || updated;

            Log.Debug("Getting orders");
            updateProgressAction?.Invoke("Getting orders");
            updated = GetOrders(account, candlesService, marketsService, tableManager, out var orders, addedOrUpdatedTrades) || updated;

            // Update trades from reports API
            updateProgressAction?.Invoke("Getting report");

            DateTime? lastUpdateTime = null;
            if (custom.LastUpdateTime.ContainsKey(_user)) lastUpdateTime = custom.LastUpdateTime[_user];

            if (IncludeReportInUpdates)
            {
                var reportLines = GetReport(lastUpdateTime);
                custom.LastUpdateTime[_user] = DateTime.UtcNow;
                account.CustomJson = JsonConvert.SerializeObject(custom);

                Log.Debug("Getting historic trades");
                updateProgressAction?.Invoke("Getting historic trades");
                updated = GetReportTrades(account, candlesService, marketsService, reportLines, addedOrUpdatedTrades) ||
                          updated;

                Log.Debug("Getting deposits/withdrawals");
                updateProgressAction?.Invoke("Updating deposits/withdrawals");
                UpdateDepositsWithdrawals(account, reportLines);
            }

            // Set any open trades to closed that aren't in the open list
            foreach (var trade in account.Trades.Where(t =>
                t.OrderDateTime != null && t.EntryPrice == null && t.CloseDateTime == null))
            {
                if (!openTrades.Contains(trade) && !orders.Contains(trade))
                {
                    if (trade.OrderExpireTime != null && trade.OrderExpireTime <= DateTime.UtcNow)
                    {
                        trade.CloseDateTime = trade.OrderExpireTime;
                        trade.CloseReason = TradeCloseReason.HitExpiry;

                        if (!addedOrUpdatedTrades.Contains(trade))
                        {
                            addedOrUpdatedTrades.Add(trade);
                        }
                    }
                    else
                    {
                        trade.CloseDateTime = DateTime.UtcNow;
                        trade.CloseReason = TradeCloseReason.OrderClosed;

                        if (!addedOrUpdatedTrades.Contains(trade))
                        {
                            addedOrUpdatedTrades.Add(trade);
                        }
                    }
                }
            }

            return updated;
        }

        private bool GetClosedTrades(IBrokerAccount account, IBrokersCandlesService candlesService,
            IMarketDetailsService marketsService, O2GTableManager tableManager, List<Trade> addedOrUpdatedTrades)
        {
            O2GTableIterator iterator;
            var openTrades = tableManager.getTable(O2GTableType.ClosedTrades);

            iterator = new O2GTableIterator();
            var addedOrUpdatedOpenTrade = false;

            while (openTrades.getNextGenericRow(iterator, out var row))
            {
                var tradeRow = (O2GClosedTradeTableRow)row;
                var trade = account.Trades.FirstOrDefault(x => x.Id == tradeRow.TradeID);

                if (trade == null)
                {
                    trade = new Trade();
                    account.Trades.Add(trade);
                    trade.Market = tradeRow.Instrument;
                    trade.Broker = "FXCM";
                    trade.Id = tradeRow.TradeID;
                    trade.EntryDateTime = DateTime.SpecifyKind(tradeRow.OpenTime, DateTimeKind.Utc); // Checked with Trading Station - these are returned in UTC
                    trade.CloseDateTime = DateTime.SpecifyKind(tradeRow.CloseTime, DateTimeKind.Utc);
                    trade.EntryPrice = (decimal)tradeRow.OpenRate;
                    trade.ClosePrice = (decimal)tradeRow.CloseRate;

                    trade.EntryQuantity = tradeRow.Amount;
                    trade.GrossProfitLoss = (decimal)tradeRow.GrossPL;
                    // tradeRow.NetPL is profit/loss in pips
                    trade.Rollover = (decimal)tradeRow.RolloverInterest;
                    trade.TradeDirection = tradeRow.BuySell == "S" ? TradeDirection.Short : TradeDirection.Long;
                    trade.PricePerPip = candlesService.GetGBPPerPip(
                        marketsService, this, trade.Market, trade.EntryQuantity.Value, trade.EntryDateTime.Value, true);

                    addedOrUpdatedOpenTrade = true;
                    if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                }
                else
                {
                    if (trade.EntryDateTime != DateTime.SpecifyKind(tradeRow.OpenTime, DateTimeKind.Utc))
                    {
                        trade.EntryDateTime = DateTime.SpecifyKind(tradeRow.OpenTime, DateTimeKind.Utc);
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (trade.CloseDateTime != DateTime.SpecifyKind(tradeRow.CloseTime, DateTimeKind.Utc))
                    {
                        trade.CloseDateTime = DateTime.SpecifyKind(tradeRow.CloseTime, DateTimeKind.Utc);
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (trade.GrossProfitLoss != (decimal)tradeRow.GrossPL)
                    {
                        trade.GrossProfitLoss = (decimal)tradeRow.GrossPL;
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    // tradeRow.NetPL is profit/loss in pips

                    if (trade.Rollover != (decimal)tradeRow.RolloverInterest)
                    {
                        trade.Rollover = (decimal)tradeRow.RolloverInterest;
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (trade.EntryPrice != (decimal)tradeRow.OpenRate)
                    {
                        trade.EntryPrice = (decimal)tradeRow.OpenRate;
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (trade.ClosePrice != (decimal)tradeRow.CloseRate)
                    {
                        trade.ClosePrice = (decimal) tradeRow.CloseRate;
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (trade.EntryQuantity != (decimal)tradeRow.Amount)
                    {
                        trade.EntryQuantity = (decimal)tradeRow.Amount;
                        trade.PricePerPip = candlesService.GetGBPPerPip(
                            marketsService, this, trade.Market, trade.EntryQuantity.Value, trade.EntryDateTime.Value, true);

                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (trade.TradeDirection != (tradeRow.BuySell == "S" ? TradeDirection.Short : TradeDirection.Long))
                    {
                        trade.TradeDirection = tradeRow.BuySell == "S" ? TradeDirection.Short : TradeDirection.Long;
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }
                }
            }

            return addedOrUpdatedOpenTrade;
        }

        private bool GetOpenTrades(IBrokerAccount account, IBrokersCandlesService candlesService, IMarketDetailsService marketsService,
            O2GTableManager tableManager, out List<Trade> openTradesFound, List<Trade> addedOrUpdatedTrades)
        {
            O2GTableIterator iterator;
            openTradesFound = new List<Trade>();
            var openTrades = tableManager.getTable(O2GTableType.Trades);

            iterator = new O2GTableIterator();
            var addedOrUpdatedOpenTrade = false;

            while (openTrades.getNextGenericRow(iterator, out var row))
            {
                var tradeRow = (O2GTradeTableRow)row;
                var trade = account.Trades.FirstOrDefault(x => x.Id == tradeRow.TradeID);

                if (trade == null)
                {
                    trade = new Trade();
                    account.Trades.Add(trade);
                    trade.Market = tradeRow.Instrument;
                    trade.Broker = "FXCM";
                    trade.Id = tradeRow.TradeID;
                    trade.OrderId = tradeRow.OpenOrderID;
                    trade.EntryDateTime = DateTime.SpecifyKind(tradeRow.OpenTime, DateTimeKind.Utc);
                    trade.EntryPrice = (decimal)tradeRow.OpenRate;

                    if (!tradeRow.Limit.Equals(0D))
                    {
                        trade.AddLimitPrice(tradeRow.LimitOrderID, trade.EntryDateTime.Value, (decimal)tradeRow.Limit);
                    }

                    if (!tradeRow.Stop.Equals(0D))
                    {
                        trade.AddStopPrice(tradeRow.StopOrderID, trade.EntryDateTime.Value, (decimal)tradeRow.Stop);
                    }

                    trade.EntryQuantity = tradeRow.Amount;
                    trade.Rollover = (decimal)tradeRow.RolloverInterest;
                    trade.GrossProfitLoss = (decimal)tradeRow.GrossPL;
                    // tradeRow.PL is pips profit/loss
                    trade.Rollover = (decimal)tradeRow.RolloverInterest;
                    trade.TradeDirection = tradeRow.BuySell == "S" ? TradeDirection.Short : TradeDirection.Long;
                    trade.PricePerPip = candlesService.GetGBPPerPip(
                        marketsService, this, trade.Market, trade.EntryQuantity.Value, trade.EntryDateTime.Value, true);

                    addedOrUpdatedOpenTrade = true;
                    if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                }
                else
                {
                    if (trade.EntryDateTime != DateTime.SpecifyKind(tradeRow.OpenTime, DateTimeKind.Utc))
                    {
                        trade.EntryDateTime = DateTime.SpecifyKind(tradeRow.OpenTime, DateTimeKind.Utc);
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (trade.GrossProfitLoss != (decimal)tradeRow.GrossPL)
                    {
                        trade.GrossProfitLoss = (decimal)tradeRow.GrossPL;
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (trade.Rollover != (decimal)tradeRow.RolloverInterest)
                    {
                        trade.Rollover = (decimal)tradeRow.RolloverInterest;
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    // tradeRow.PL is profit/loss in pips

                    if (trade.EntryPrice != (decimal)tradeRow.OpenRate)
                    {
                        trade.EntryPrice = (decimal)tradeRow.OpenRate;
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (trade.EntryQuantity != (decimal)tradeRow.Amount)
                    {
                        trade.EntryQuantity = (decimal)tradeRow.Amount;
                        trade.PricePerPip = candlesService.GetGBPPerPip(
                            marketsService, this, trade.Market, trade.EntryQuantity.Value, trade.EntryDateTime.Value, true);

                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (trade.CloseReason != null)
                    {
                        trade.CloseReason = null;
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (trade.CloseDateTime != null)
                    {
                        trade.CloseDateTime = null;
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (trade.TradeDirection != (tradeRow.BuySell == "S" ? TradeDirection.Short : TradeDirection.Long))
                    {
                        trade.TradeDirection = tradeRow.BuySell == "S" ? TradeDirection.Short : TradeDirection.Long;
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (!tradeRow.Limit.Equals(0))
                    {
                        if (trade.LimitPrices.Count == 0 || trade.LimitPrices[trade.LimitPrices.Count - 1].Price != (decimal)tradeRow.Limit)
                        {
                            trade.AddLimitPrice(DateTime.UtcNow, (decimal)tradeRow.Limit);
                            addedOrUpdatedOpenTrade = true;
                            if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                        }
                    }
                    else
                    {
                        if (trade.LimitPrices.Count > 0 && trade.LimitPrices[trade.LimitPrices.Count - 1].Price != null)
                        {
                            trade.AddLimitPrice(DateTime.UtcNow, null);
                            addedOrUpdatedOpenTrade = true;
                            if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                        }
                    }

                    if (!tradeRow.Stop.Equals(0))
                    {
                        if (trade.StopPrices.Count == 0 || trade.StopPrices[trade.StopPrices.Count - 1].Price != (decimal)tradeRow.Stop)
                        {
                            trade.AddStopPrice(DateTime.UtcNow, (decimal)tradeRow.Stop);
                            addedOrUpdatedOpenTrade = true;
                            if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                        }
                    }
                    else
                    {
                        if (trade.StopPrices.Count > 0 && trade.StopPrices[trade.StopPrices.Count - 1].Price != null)
                        {
                            trade.AddStopPrice(DateTime.UtcNow, null);
                            addedOrUpdatedOpenTrade = true;
                            if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                        }
                    }
                }

                openTradesFound.Add(trade);
            }

            return addedOrUpdatedOpenTrade;
        }

        private bool GetOrders(IBrokerAccount account, IBrokersCandlesService candlesService, IMarketDetailsService marketsService,
            O2GTableManager tableManager, out List<Trade> orders, List<Trade> addedOrUpdatedTrades)
        {
            orders = new List<Trade>();
            var iterator = new O2GTableIterator();
            var offers = tableManager.getTable(O2GTableType.Offers);
            var offersLookup = new Dictionary<string, O2GOfferTableRow>();
            while (offers.getNextGenericRow(iterator, out var row))
            {
                var offerRow = (O2GOfferTableRow)row;
                offersLookup.Add(offerRow.OfferID, offerRow);
            }

            var openTrades = tableManager.getTable(O2GTableType.Orders);

            // Get offer from offer Id to get instrument
            var addedOrUpdatedOpenTrade = false;

            // Get all orders
            var tradeRows = new Dictionary<string, List<O2GOrderTableRow>>();
            while (openTrades.getNextGenericRow(iterator, out var row))
            {
                var tradeRow = (O2GOrderTableRow)row;
                if (!tradeRows.ContainsKey(tradeRow.TradeID))
                {
                    tradeRows[tradeRow.TradeID] = new List<O2GOrderTableRow>();
                }

                tradeRows[tradeRow.TradeID].Add(tradeRow);
            }

            // Create trades
            foreach (var kvp in tradeRows)
            {
                var orderOrder = kvp.Value.FirstOrDefault(x => x.Type == "SE" || x.Type == "LE");
                var orderOffer = orderOrder != null ? offersLookup[orderOrder.OfferID] : null;
                var stopOrder = kvp.Value.FirstOrDefault(x => x.Type == "S" || x.Type == "ST"); // ST = Stop Trailing
                var stopOffer = stopOrder != null ? offersLookup[stopOrder.OfferID] : null;
                var limitOrder = kvp.Value.FirstOrDefault(x => x.Type == "L");
                var limitOffer = limitOrder != null ? offersLookup[limitOrder.OfferID] : null;

                if (orderOrder == null)
                {
                    continue;
                }

                var time = DateTime.SpecifyKind(orderOrder.StatusTime, DateTimeKind.Utc); // Checked with Trading Station - this is UTC Time
                var expiry = DateTime.SpecifyKind(orderOrder.ExpireDate, DateTimeKind.Utc);
                var instrument = orderOffer.Instrument;
                var orderPrice = orderOrder.Rate;
                var buySell = orderOrder.BuySell;
                
                if (string.IsNullOrEmpty(instrument))
                {
                    continue;
                }

                decimal? stop = GetStopPrice(stopOrder, candlesService, marketsService, instrument, (decimal)orderPrice, buySell);
                decimal? limit = GetLimitPrice(limitOrder, candlesService, marketsService, instrument, (decimal)orderPrice, buySell);
                var amount = orderOrder.Amount;
                var actualExpiry = orderOrder.ExpireDate.Year >= 1950 ? (DateTime?)expiry : null;

                var trade = account.Trades.FirstOrDefault(x => x.Id == orderOrder.TradeID);
                if (trade == null)
                {
                    trade = new Trade();
                    account.Trades.Add(trade);
                    trade.Broker = "FXCM";
                    trade.OrderAmount = orderOrder.Amount;
                    trade.Id = orderOrder.TradeID;
                    trade.Market = instrument;
                    trade.AddOrderPrice(time, (decimal)orderPrice);
                    trade.OrderDateTime = time;
                    trade.OrderExpireTime = actualExpiry;
                    trade.PricePerPip = candlesService.GetGBPPerPip(marketsService, this, trade.Market, orderOrder.Amount, time, true);

                    if (stop != null)
                    {
                        trade.AddStopPrice(time, stop);
                    }

                    if (limit != null)
                    {
                        trade.AddLimitPrice(time, limit);
                    }

                    trade.TradeDirection = buySell == "B" ? TradeDirection.Long : TradeDirection.Short;
                    trade.OrderAmount = amount;
                    addedOrUpdatedOpenTrade = true;
                    if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                }
                else
                {
                    if (trade.OrderPrice != (decimal)orderPrice)
                    {
                        trade.AddOrderPrice(time, (decimal)orderPrice);
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (trade.OrderAmount != orderOrder.Amount)
                    {
                        trade.OrderAmount = orderOrder.Amount;
                        trade.PricePerPip =
                            candlesService.GetGBPPerPip(marketsService, this, trade.Market, trade.OrderAmount.Value, trade.OrderDateTime.Value, true);
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (trade.OrderDateTime != time)
                    {
                        trade.OrderDateTime = time;
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (trade.OrderExpireTime != actualExpiry)
                    {
                        trade.OrderExpireTime = actualExpiry;
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (stop != null && trade.StopPrice != stop)
                    {
                        trade.AddStopPrice(time, stop);
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }

                    if (limit != null && trade.LimitPrice != limit)
                    {
                        trade.AddLimitPrice(time, limit);
                        addedOrUpdatedOpenTrade = true;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    }
                }

                if (orderOrder != null)
                {
                    if (orderOrder.Type == "SE")
                    {
                        if (trade.OrderType != OrderType.StopEntry)
                        {
                            trade.OrderType = OrderType.StopEntry;
                            addedOrUpdatedOpenTrade = true;
                            if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                        }
                    }
                    else if (orderOrder.Type == "LE")
                    {
                        if (trade.OrderType != OrderType.LimitEntry)
                        {
                            trade.OrderType = OrderType.LimitEntry;
                            if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                            addedOrUpdatedOpenTrade = true;
                        }
                    }
                }

                orders.Add(trade);
            }

            return addedOrUpdatedOpenTrade;
        }

        private decimal? GetStopPrice(O2GOrderTableRow stop, IBrokersCandlesService candles, IMarketDetailsService marketsService, string instrument, decimal orderPrice, string buySell)
        {
            if (stop == null)
            {
                return null;
            }

            decimal? ret;

            if (!stop.Rate.Equals(0.0))
            {
                ret = (decimal)stop.Rate;
            }
            else
            {
                ret = (decimal)orderPrice +
                      (buySell == "B"
                          ? -marketsService.GetPriceFromPips(this.Name, Math.Abs((decimal)stop.PegOffset), instrument)
                          : marketsService.GetPriceFromPips(this.Name, Math.Abs((decimal)stop.PegOffset), instrument));
            }

            return ret;
        }

        private decimal? GetLimitPrice(O2GOrderTableRow limit, IBrokersCandlesService candles, IMarketDetailsService marketsService, string instrument, decimal orderPrice, string buySell)
        {
            if (limit == null)
            {
                return null;
            }

            decimal? ret;

            if (!limit.Rate.Equals(0.0))
            {
                ret = (decimal)limit.Rate;
            }
            else
            {
                ret = (decimal)orderPrice +
                      (buySell == "B"
                          ? marketsService.GetPriceFromPips(this.Name, Math.Abs((decimal)limit.PegOffset), instrument)
                          : -marketsService.GetPriceFromPips(this.Name, Math.Abs((decimal)limit.PegOffset), instrument));
            }

            return ret;
        }

        private string[] GetReport(DateTime? lastUpdateTime = null)
        {
            var url = "https://fxpa2.fxcorporate.com/fxpa/getreport.app/";
            var account = _user;
            var report = "REPORT_NAME_CUSTOMER_ACCOUNT_STATEMENT";
            var startDate = "1/1/2013";
            if (lastUpdateTime != null)
            {
                startDate = lastUpdateTime.Value.AddDays(-5).ToString("M/d/yyyy");
            }

            var endDate = DateTime.UtcNow.ToString("M/d/yyyy");
            var outputFormat = "csv-web";
            var locale = "enu";
            var extra = "&timeformat=UTC";

            ReportServer server = new ReportServer();
            string[] lines;

            var reader = server.GetReport(url, _connection, report, _user, _password, account, locale, startDate, endDate, outputFormat, extra);

            try
            {
                using (var output = new MemoryStream())
                {
                    byte[] buff = new byte[1024];
                    int r;

                    while (true)
                    {
                        r = reader.Read(buff, 0, 1024);
                        if (r <= 0)
                        {
                            break;
                        }

                        output.Write(buff, 0, r);
                    }

                    lines = Encoding.ASCII.GetString(output.ToArray()).Split(new string[] { "\n" }, StringSplitOptions.None);
                }
            }
            finally
            {
                reader.Close();
            }

            return lines;
        }

        private bool GetReportTrades(IBrokerAccount brokerAccount,
            IBrokersCandlesService candlesService, IMarketDetailsService marketsService, string[] lines, List<Trade> addedOrUpdatedTrades)
        {
            var updated = false;

            var startLineIndex = GetLineStart(lines, "\"CLOSED TRADE LIST\"");
            var endLineIndex = GetLineStart(lines, "\"Total:");

            for (var i = startLineIndex + 2; i < endLineIndex; i += 2)
            {
                var values1 = CsvHelper.GetCsvValues(lines[i]);
                var values2 = CsvHelper.GetCsvValues(lines[i + 1]);

                if (values1[0] == "No data found for the statement period")
                {
                    break;
                }

                var id = values1[0];
                var market = values1[1];
                var quantity = values1[2];
                var date = values1[3];
                var sold = values1[4];
                var bought = values1[5];
                var grossProfitLoss = values1[6];
                var markupPips = values1[7];
                var comm = values1[8];
                var dividends = values1[9];
                var rollover = values1[10];
                var adj = values1[11];
                var netProfitLoss = values1[12];
                var condition = values1[13];

                var id2 = values2[0];
                var market2 = values2[1];
                var quantity2 = values2[2];
                var date2 = values2[3];
                var sold2 = values2[4];
                var bought2 = values2[5];
                var grossProfitLoss2 = values2[6];
                var markupPips2 = values2[7];
                var comm2 = values2[8];
                var dividends2 = values2[9];
                var rollover2 = values2[10];
                var adj2 = values2[11];
                var netProfitLoss2 = values2[12];
                var condition2 = values2[13];

                var existingTrade = brokerAccount.Trades.FirstOrDefault(t => t.Id == id);

                // Time here isn't in UTC time but is in Server Time

                var trade = new Trade
                {
                    Id = id,
                    Broker = "FXCM",
                    Market = market,
                    EntryQuantity = decimal.Parse(quantity),
                    EntryPrice = !string.IsNullOrEmpty(sold) ? decimal.Parse(sold) : decimal.Parse(bought),
                    EntryDateTime = DateTime.SpecifyKind(DateTime.ParseExact(date, "M/d/y h:m tt", CultureInfo.InvariantCulture), DateTimeKind.Utc),
                    ClosePrice = !string.IsNullOrEmpty(sold2) ? decimal.Parse(sold2) : decimal.Parse(bought2),
                    CloseDateTime = DateTime.SpecifyKind(DateTime.ParseExact(date2, "M/d/y h:m tt", CultureInfo.InvariantCulture), DateTimeKind.Utc),
                    TradeDirection = !string.IsNullOrEmpty(sold) ? TradeDirection.Short : TradeDirection.Long,
                    GrossProfitLoss = decimal.Parse(grossProfitLoss2),
                    NetProfitLoss = decimal.Parse(netProfitLoss2),
                    Rollover = decimal.Parse(rollover2)
                };

                switch (condition2)
                {
                    case "S":
                        trade.CloseReason = TradeCloseReason.HitStop;
                        break;
                    case "L":
                        trade.CloseReason = TradeCloseReason.HitLimit;
                        break;
                    case "S(t)": // Hit trailing stop
                        trade.CloseReason = TradeCloseReason.HitStop;
                        break;
                    case "C":
                        trade.CloseReason = TradeCloseReason.ManualClose;
                        break;
                    case "Mkt": // Not sure what Mkt means? Some trades show as 'Mkt Mkt'
                    case "MC": // Not sure what MC means? Market Close? Manual close?
                        trade.CloseReason = TradeCloseReason.ManualClose;
                        break;
                    default:
                        Debugger.Break();
                        break;
                }

                if (existingTrade == null)
                {
                    trade.PricePerPip = candlesService.GetGBPPerPip(
                        marketsService, this, trade.Market, trade.EntryQuantity.Value, trade.EntryDateTime.Value, true);
                    brokerAccount.Trades.Add(trade);

                    if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                    updated = true;
                }
                else
                {
                    if (trade.EntryQuantity != existingTrade.EntryQuantity)
                    {
                        existingTrade.EntryQuantity = trade.EntryQuantity;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                        updated = true;
                    }

                    if (trade.EntryDateTime != existingTrade.EntryDateTime)
                    {
                        existingTrade.EntryDateTime = trade.EntryDateTime;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                        updated = true;
                    }

                    if (trade.EntryPrice != existingTrade.EntryPrice)
                    {
                        existingTrade.EntryPrice = trade.EntryPrice;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                        updated = true;
                    }

                    if (trade.ClosePrice != existingTrade.ClosePrice)
                    {
                        existingTrade.ClosePrice = trade.ClosePrice;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                        updated = true;
                    }

                    if (trade.CloseDateTime != existingTrade.CloseDateTime)
                    {
                        existingTrade.CloseDateTime = trade.CloseDateTime;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                        updated = true;
                    }

                    if (trade.CloseReason != existingTrade.CloseReason)
                    {
                        existingTrade.CloseReason = trade.CloseReason;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                        updated = true;
                    }

                    if (trade.GrossProfitLoss != existingTrade.GrossProfitLoss)
                    {
                        existingTrade.GrossProfitLoss = trade.GrossProfitLoss;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                        updated = true;
                    }

                    if (trade.NetProfitLoss != existingTrade.NetProfitLoss)
                    {
                        existingTrade.NetProfitLoss = trade.NetProfitLoss;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                        updated = true;
                    }

                    if (trade.Rollover != existingTrade.Rollover)
                    {
                        existingTrade.Rollover = trade.Rollover;
                        if (!addedOrUpdatedTrades.Contains(trade)) addedOrUpdatedTrades.Add(trade);
                        updated = true;
                    }
                }
            }

            return updated;
        }

        private static int GetLineStart(string[] lines, string text, int startIndex = 0)
        {
            for (var i = startIndex; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(text))
                {
                    return i;
                }
            }


            return -1;
        }

        public List<TickData> GetTickData(IBroker broker, string market, DateTime utcStart, DateTime utcEnd)
        {
            GetHistoryPrices(market, "t1", Timeframe.T1, utcStart, utcEnd, null, out _, out var ret);

            return ret;
        }

        public bool UpdateAccount(IBrokerAccount account, IBrokersCandlesService candlesService, IMarketDetailsService marketsService,
            Action<string> updateProgressAction)
        {
            throw new NotImplementedException();
        }

        public bool UpdateAccount(IBrokerAccount account, IBrokersCandlesService candlesService, IMarketDetailsService marketsService,
            Action<string> updateProgressAction, DateTime? lastUpdateTime, out List<Trade> addedOrUpdatedTrades)
        {
            throw new NotImplementedException();
        }

        public bool UpdateCandles(List<Candle> candles, string market, Timeframe timeframe, DateTime start, Action<string> progressUpdate)
        {
            var to = DateTime.UtcNow;
            var updated = false;

            string interval;

            switch (timeframe)
            {
                case Timeframe.D1:
                    interval = "D1";
                    break;
                case Timeframe.H1:
                    interval = "H1";
                    break;
                case Timeframe.H2:
                    interval = "H2";
                    break;
                case Timeframe.H4:
                    interval = "H4";
                    break;
                case Timeframe.H8:
                    interval = "H8";
                    break;
                case Timeframe.M1:
                    interval = "m1";
                    break;
                case Timeframe.M5:
                    interval = "m5";
                    break;
                case Timeframe.M15:
                    interval = "m15";
                    break;
                case Timeframe.M30:
                    interval = "m30";
                    break;
                default:
                    throw new ApplicationException($"FXCM unable to update candles for interval {timeframe}");
            }

            GetHistoryPrices(market, interval, timeframe, start, to, progressUpdate, out var loadedCandles, out var _);


            var existingCandleLookup = new Dictionary<long, Candle>();
            candles.ForEach(x => existingCandleLookup[x.OpenTimeTicks] = x);

            if (loadedCandles != null)
            {
                foreach (var candle in loadedCandles.OrderBy(x => x.OpenTimeTicks))
                {
                    if (existingCandleLookup.TryGetValue(candle.OpenTimeTicks, out var existingCandle))
                    {
                        if (existingCandle.IsComplete != candle.IsComplete
                            || existingCandle.CloseTimeTicks != candle.CloseTimeTicks
                            || !existingCandle.OpenAsk.Equals(candle.OpenAsk)
                            || !existingCandle.CloseAsk.Equals(candle.CloseAsk)
                            || !existingCandle.HighAsk.Equals(candle.HighAsk)
                            || !existingCandle.LowAsk.Equals(candle.LowAsk)
                            || !existingCandle.OpenBid.Equals(candle.OpenBid)
                            || !existingCandle.CloseBid.Equals(candle.CloseBid)
                            || !existingCandle.HighBid.Equals(candle.HighBid)
                            || !existingCandle.LowBid.Equals(candle.LowBid)
                            || existingCandle.OpenTimeTicks != candle.OpenTimeTicks)
                        {
                            var index = candles.IndexOf(existingCandle);
                            candles.RemoveAt(index);
                            candles.Insert(index, candle);

                            existingCandleLookup[candle.OpenTimeTicks] = candle;
                            updated = true;
                        }
                    }
                    else
                    {
                        candles.Add(candle);
                        existingCandleLookup[candle.OpenTimeTicks] = candle;
                        updated = true;
                    }
                }
            }

            return updated;
        }

        private bool UpdateDepositsWithdrawals(IBrokerAccount brokerAccount, string[] lines)
        {
            var updated = false;
            foreach (var d in brokerAccount.DepositsWithdrawals)
            {
                d.Broker = "FXCM";
            }
            
            var startLineIndex = GetLineStart(lines, "\"ACCOUNT ACTIVITY\"");
            var endLineIndex = GetLineStart(lines, "\"Total:", startLineIndex);

            for (var i = startLineIndex + 2; i < endLineIndex; i += 1)
            {
                var values1 = CsvHelper.GetCsvValues(lines[i]);

                if (values1[0] == "No data found for the statement period")
                {
                    break;
                }

                var date = values1[0];
                var code = values1[1];
                var description = values1[2];
                var accountNumber = values1[3];
                var ticket = values1[4];
                var amount = values1[5];
                var balance = values1[6];

                if (code == "Depos" || code == "Withd")
                {
                    if (brokerAccount.DepositsWithdrawals.All(x => x.Description != description))
                    {
                        var deposit = new DepositWithdrawal
                        {
                            Time = DateTime.ParseExact(date, "M/d/y h:m tt", CultureInfo.InvariantCulture),
                            Amount = decimal.Parse(amount),
                            Description = description,
                            Broker = "FXCM"
                        };
                        brokerAccount.DepositsWithdrawals.Add(deposit);
                        updated = true;
                    }
                }
            }

            return updated;
        }

        /// <summary>
        /// Request historical prices for the specified timeframe of the specified period
        /// {INSTRUMENT} - An instrument, for which you want to get historical prices.
        /// For example, EUR/USD. Mandatory argument.
        /// {TIMEFRAME} - time period which forms a single candle. Mandatory argument.
        /// For example, m1 - for 1 minute, H1 - for 1 hour.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="instrument"></param>
        /// <param name="interval"></param>
        /// <param name="dtFrom"></param>
        /// <param name="dtTo"></param>
        /// <param name="responseListener"></param>
        public void GetHistoryPrices(string instrument, string interval, Timeframe timeframe, DateTime dtFrom, DateTime dtTo, Action<string> progressUpdate, out List<Candle> candles, out List<TickData> ticks)
        {
            if (Session == null)
            {
                Log.Warn($"FXCM not connected so unable to get {instrument} prices for {interval} for date {dtFrom}-{dtTo}");
                candles = null;
                ticks = null;
                return;
            }

            Log.Debug($"Getting FXCM {instrument} prices for {interval} for date {dtFrom}-{dtTo}");
            var factory = Session.getRequestFactory();

            if (factory == null)
            {
                Log.Debug("Unable to connect to FXCM");
                candles = null;
                ticks = null;
                return;
            }

            var responseListener = new ResponseListener(Session);
            Session.subscribeResponse(responseListener);
            var o2gTimeframe = factory.Timeframes[interval];
            candles = new List<Candle>();
            ticks = new List<TickData>();

            if (o2gTimeframe == null)
            {
                throw new Exception(string.Format("Timeframe '{0}' is incorrect!", interval));
            }

            var maxReturn = 600;
            O2GRequest request = factory.createMarketDataSnapshotRequestInstrument(instrument, o2gTimeframe, maxReturn);
            DateTime dtFirst = dtTo;
            int iteration = 1;
            List<(DateTime, DateTime)> fromToList = new List<(DateTime, DateTime)>();
            Log.Debug($"Getting FCXM prices for {dtFrom}-{dtTo} for {instrument} {timeframe}");
            var reversed = false;
            var downloadedCandles = 0;
            var progressUpdateTime = DateTime.Now;

            do // cause there is limit for returned candles amount
            {
                if ((dtFirst - dtFrom).TotalMilliseconds < 1000)
                {
                    dtFirst = dtFrom.AddSeconds(1);
                    reversed = true;
                }

                var attempt = 1;
                var gotData = false;
                while (attempt < 10 && !gotData)
                {
                    factory.fillMarketDataSnapshotRequestTime(request, dtFrom, dtFirst.AddSeconds(1) < dtTo ? dtFirst.AddSeconds(1) : dtFirst, false); // Returns latest candles in date time range
                    fromToList.Add((dtFrom, dtFirst));

                    responseListener.SetRequestID(request.RequestID);
                    Session.sendRequest(request);
                    if (responseListener.WaitEvents())
                    {
                        gotData = true;
                        break;
                    }

                    Thread.Sleep(_rnd.Next(5000, 40000));
                }

                if (!gotData)
                {
                    throw new Exception("Response waiting timeout expired");
                }

                // shift "to" bound to oldest datetime of returned data
                var response = responseListener.GetResponse();

                if (!string.IsNullOrEmpty(responseListener.Error) && !responseListener.Error.Contains("No data found for symbol"))
                {
                    throw new ApplicationException($"Error getting {instrument} for interval {interval} timeframe {timeframe} - error: {responseListener.Error}");
                }

                if (response != null && response.Type == O2GResponseType.MarketDataSnapshot)
                {
                    O2GResponseReaderFactory readerFactory = Session.getResponseReaderFactory();
                    if (readerFactory != null)
                    {
                        O2GMarketDataSnapshotResponseReader reader = readerFactory.createMarketDataSnapshotReader(response);
                        if (reader.Count > 0)
                        {
                            if (DateTime.Compare(dtFirst, reader.getDate(0)) != 0)
                            {
                                dtFirst = reader.getDate(0);
                            }
                            else
                            {
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    ConstructCandles(Session, response, timeframe, out var tmpCandles, out var tmpTickData);


                    if (tmpCandles.Count > 0)
                    {
                        Log.Debug($"FCXM got {tmpCandles.Count} candles for {instrument} {timeframe} (Total {candles.Count})");
                    }

                    if (tmpTickData.Count > 0)
                    {
                        Log.Debug($"FCXM got {tmpTickData.Count} ticks (Total {ticks.Count} - {(ticks.Count > 0 ? ticks[ticks.Count - 1].Datetime.ToString() : string.Empty)} - {(ticks.Count > 0 ? ticks[0].Datetime.ToString() : string.Empty)})");
                    }

                    candles.AddRange(tmpCandles);
                    downloadedCandles += tmpCandles.Count;

                    if (DateTime.Now >= progressUpdateTime && progressUpdate != null)
                    {
                        progressUpdate($"Downloaded {downloadedCandles} candles");
                        progressUpdateTime = DateTime.Now.AddSeconds(5);
                    }

                    ticks.AddRange(tmpTickData);
                }
                else
                {
                    break;
                }

                iteration++;
            } while (!reversed && dtFirst > dtFrom);

            Log.Debug($"Got {candles.Count} prices for {instrument} {interval} for date {dtFrom}-{dtTo}");
        }

        /// <summary>
        /// Print history data from response
        /// </summary>
        /// <param name="session"></param>
        /// <param name="response"></param>
        public static void ConstructCandles(O2GSession session, O2GResponse response, Timeframe timeframe, out List<Candle> candles, out List<TickData> bidTicks)
        {
            candles = new List<Candle>();
            bidTicks = new List<TickData>();
            O2GResponseReaderFactory factory = session.getResponseReaderFactory();

            if (factory != null)
            {
                O2GMarketDataSnapshotResponseReader reader = factory.createMarketDataSnapshotReader(response);
                for (int ii = reader.Count - 1; ii >= 0; ii--)
                {
                    if (reader.isBar)
                    {
                        var candle = new Candle();
                        candle.OpenTimeTicks = DateTime.SpecifyKind(reader.getDate(ii), DateTimeKind.Utc).Ticks;
                        candle.CloseTimeTicks = new DateTime(candle.OpenTimeTicks).AddSeconds((int)timeframe).Ticks;
                        candle.IsComplete = candle.CloseTimeTicks <= DateTime.UtcNow.Ticks ? (byte)1 : (byte)0;

                        candle.CloseBid = (float)reader.getBidClose(ii);
                        candle.OpenBid = (float)reader.getBidOpen(ii);
                        candle.HighBid = (float)reader.getBidHigh(ii);
                        candle.LowBid = (float)reader.getBidLow(ii);
                        candle.CloseAsk = (float)reader.getAskClose(ii);
                        candle.OpenAsk = (float)reader.getAskOpen(ii);
                        candle.HighAsk = (float)reader.getAskHigh(ii);
                        candle.LowAsk = (float)reader.getAskLow(ii);

                        candles.Add(candle);

                    }
                    else
                    {
                        var tick = new TickData();
                        tick.Datetime = reader.getDate(ii);
                        tick.Open = (float)reader.getBidOpen(ii);
                        tick.Close = (float)reader.getBidClose(ii);
                        tick.High = (float)reader.getBidHigh(ii);
                        tick.Low = (float)reader.getBidLow(ii);
                        bidTicks.Add(tick);
                    }
                }
            }
        }

        #region IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }

                if (Session != null)
                {
                    if (Session.getChartSessionStatus() == O2GChartSessionStatusCode.Connected)
                    {
                        Disconnect();
                    }
                }

                disposedValue = true;
            }
        }

        public void Disconnect()
        {
            Log.Info("FXCM disconnecting");

            if (Session != null)
            {
                _sessionStatusListener.Reset();
                Session.logout();
                _sessionStatusListener.WaitEvents();
                Session.Dispose();
                Session = null;
            }

            Log.Info("FXCM disconnected");
        }

        ~FxcmBroker()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}