using System;
using System.Reflection;
using System.Threading;
using fxcore2;
using log4net;
using TraderTools.Basics;

namespace TraderTools.Brokers.FXCM
{
    public static class FxcmBrokerCreateOrderExtension
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static bool CreateOrder(this FxcmBroker fxcm, string market, double rate, double? rateStop, double? rateLimit,
            DateTime? expiryDateUtc, decimal amount, TradeDirection direction, out string orderId)
        {
            var requestFactory = fxcm.Session.getRequestFactory();
            orderId = string.Empty;

            // Get price
            var offer = GetOffer(fxcm.Session, market);
            if (offer == null)
            {
                Log.Error($"Unable to get offer price  - {requestFactory.getLastError()}");
                return false;
            }

            // Get account
            var account = GetAccount(fxcm.Session);
            var loginRules = fxcm.Session.getLoginRules();
            var tradingSettingsProvider = loginRules.getTradingSettingsProvider();

            var iCondDistEntryLimit = tradingSettingsProvider.getCondDistEntryLimit(market);
            var iCondDistEntryStop = tradingSettingsProvider.getCondDistEntryStop(market);

            // Get order type
            var buySell = direction == TradeDirection.Long ? "B" : "S";
            var sOrderType = GetEntryOrderType(offer.Bid, offer.Ask, rate, buySell, offer.PointSize, iCondDistEntryLimit, iCondDistEntryStop);


            var valuemap = requestFactory.createValueMap();
            valuemap.setString(O2GRequestParamsEnum.Command, Constants.Commands.CreateOrder);
            valuemap.setString(O2GRequestParamsEnum.OrderType, sOrderType);
            valuemap.setString(O2GRequestParamsEnum.AccountID, account.AccountID);
            valuemap.setString(O2GRequestParamsEnum.OfferID, offer.OfferID);
            valuemap.setString(O2GRequestParamsEnum.BuySell, buySell);
            valuemap.setInt(O2GRequestParamsEnum.Amount, (int)amount);
            valuemap.setDouble(O2GRequestParamsEnum.Rate, rate);
            valuemap.setString(O2GRequestParamsEnum.CustomID, "EntryOrder");

            if (rateStop != null)
            {
                valuemap.setDouble(O2GRequestParamsEnum.RateStop, rateStop.Value);
            }

            if (rateLimit != null)
            {
                valuemap.setDouble(O2GRequestParamsEnum.RateLimit, rateLimit.Value);
            }

            // Set expiry
            if (expiryDateUtc != null)
            {
                var expiryStr = expiryDateUtc.Value.ToString("yyyyMMdd-HH:mm:ss");
                valuemap.setString(O2GRequestParamsEnum.TimeInForce, Constants.TIF.GTD);
                valuemap.setString(O2GRequestParamsEnum.ExpireDateTime, expiryStr); // UTCTimestamp format: "yyyyMMdd-HH:mm:ss.SSS" (milliseconds are optional)
            }

            // Create order
            var request = requestFactory.createOrderRequest(valuemap);
            if (request == null)
            {
                Log.Error($"Unable to create FXCM order - {requestFactory.getLastError()}");
                return false;
            }

            var ret = ProcessOrderRequest(fxcm, request, out orderId);

            return ret;
        }

        private static bool ProcessRequest(FxcmBroker fxcm, O2GRequest request)
        {
            var responseListener = new ResponseListener(fxcm.Session);
            fxcm.Session.subscribeResponse(responseListener);

            try
            {
                responseListener.SetRequestID(request.RequestID);
                fxcm.Session.sendRequest(request);
                if (responseListener.WaitEvents())
                {
                    if (!string.IsNullOrEmpty(responseListener.Error))
                    {
                        Log.Error($"Unable to process request - {responseListener.Error}");
                        return false;
                    }

                    return true;
                }
                else
                {
                    Log.Error($"Unable to process request");
                    return false;
                }
            }
            finally
            {
                fxcm.Session.unsubscribeResponse(responseListener);
            }
        }

        private static bool ProcessOrderRequest(FxcmBroker fxcm, O2GRequest request, out string orderId)
        {
            var ret = string.Empty;
            var waitOrder = new ManualResetEvent(false);

            var responseListener = new ResponseListener(
                fxcm.Session,
                data =>
                {
                    O2GResponseReaderFactory factory = fxcm.Session.getResponseReaderFactory();
                    if (factory != null)
                    {
                        O2GTablesUpdatesReader reader = factory.createTablesUpdatesReader(data);
                        for (int ii = 0; ii < reader.Count; ii++)
                        {
                            if (reader.getUpdateTable(ii) == O2GTableType.Orders)
                            {
                                O2GOrderRow orderRow = reader.getOrderRow(ii);
                                if (reader.getUpdateType(ii) == O2GTableUpdateType.Insert)
                                {
                                    ret = orderRow.OrderID;
                                    waitOrder.Set();
                                    break;
                                }
                            }
                        }
                    }

                });
            fxcm.Session.subscribeResponse(responseListener);

            try
            {
                responseListener.SetRequestID(request.RequestID);
                fxcm.Session.sendRequest(request);
                if (responseListener.WaitEvents())
                {
                    if (!string.IsNullOrEmpty(responseListener.Error))
                    {
                        Log.Error($"Unable to process request - {responseListener.Error}");
                        orderId = ret;
                        return false;
                    }

                    // Get order ID
                    waitOrder.WaitOne();

                    orderId = ret;
                    return true;
                }
                else
                {
                    Log.Error($"Unable to process request");
                    orderId = ret;
                    return false;
                }
            }
            finally
            {
                fxcm.Session.unsubscribeResponse(responseListener);
            }
        }

        private static O2GOfferRow GetOffer(O2GSession session, string market)
        {
            O2GOfferRow offer = null;
            bool bHasOffer = false;
            O2GResponseReaderFactory readerFactory = session.getResponseReaderFactory();
            if (readerFactory == null)
            {
                throw new Exception("Cannot create response reader factory");
            }
            var loginRules = session.getLoginRules();
            var response = loginRules.getTableRefreshResponse(O2GTableType.Offers);
            var offersResponseReader = readerFactory.createOffersTableReader(response);
            for (int i = 0; i < offersResponseReader.Count; i++)
            {
                offer = offersResponseReader.getRow(i);
                if (offer.Instrument.Equals(market))
                {
                    if (offer.SubscriptionStatus.Equals("T"))
                    {
                        bHasOffer = true;
                        break;
                    }
                }
            }
            if (!bHasOffer)
            {
                return null;
            }
            else
            {
                return offer;
            }
        }

        private static string GetEntryOrderType(double dBid, double dAsk, double dRate, string sBuySell, double dPointSize, int iCondDistLimit, int iCondDistStop)
        {
            double dAbsoluteDifference = 0.0D;
            if (sBuySell.Equals(Constants.Buy))
            {
                dAbsoluteDifference = dRate - dAsk;
            }
            else
            {
                dAbsoluteDifference = dBid - dRate;
            }
            int iDifferenceInPips = (int)Math.Round(dAbsoluteDifference / dPointSize);

            if (iDifferenceInPips >= 0)
            {
                if (iDifferenceInPips <= iCondDistStop)
                {
                    throw new Exception("Price is too close to market.");
                }
                return Constants.Orders.StopEntry;
            }
            else
            {
                if (-iDifferenceInPips <= iCondDistLimit)
                {
                    throw new Exception("Price is too close to market.");
                }
                return Constants.Orders.LimitEntry;
            }
        }

        public static O2GAccountRow GetAccount(this O2GSession session)
        {
            O2GResponseReaderFactory readerFactory = session.getResponseReaderFactory();
            if (readerFactory == null)
            {
                throw new Exception("Cannot create response reader factory");
            }
            var loginRules = session.getLoginRules();
            var response = loginRules.getTableRefreshResponse(O2GTableType.Accounts);
            var accountsResponseReader = readerFactory.createAccountsTableReader(response);

            for (int i = 0; i < accountsResponseReader.Count; i++)
            {
                var account = accountsResponseReader.getRow(i);
                string sAccountKind = account.AccountKind;
                if (sAccountKind.Equals("32") || sAccountKind.Equals("36"))
                {
                    if (account.MarginCallFlag.Equals("N"))
                    {
                        return account;
                    }
                }
            }

            return null;
        }

        public static bool ChangeStop(this FxcmBroker fxcm, string stopOrderId, double rateStop)
        {
            if (fxcm.Status != ConnectStatus.Connected)
            {
                Log.Error("FXCM not connected");

                return false;
            }

            O2GRequestFactory requestFactory = fxcm.Session.getRequestFactory();
            if (requestFactory == null)
            {
                throw new Exception("Cannot create request factory");
            }
            var account = GetAccount(fxcm.Session);
            O2GValueMap valuemap = requestFactory.createValueMap();
            valuemap.setString(O2GRequestParamsEnum.Command, Constants.Commands.EditOrder);
            valuemap.setString(O2GRequestParamsEnum.OrderType, Constants.Orders.Stop);
            valuemap.setString(O2GRequestParamsEnum.AccountID, account.AccountID);
            valuemap.setString(O2GRequestParamsEnum.OrderID, stopOrderId);
            valuemap.setDouble(O2GRequestParamsEnum.Rate, rateStop);


            var request = requestFactory.createOrderRequest(valuemap);

            if (request == null)
            {
                Log.Error($"Unable to process request - {requestFactory.getLastError()}");
                return false;
            }

            var ret = ProcessRequest(fxcm, request);

            return ret;
        }

        public static bool CreateMarketOrder(this FxcmBroker fxcm, string instrument, int lotsAmount, TradeDirection direction, out string orderId, double? rateStop = null, double? rateLimit = null)
        {
            orderId = string.Empty;

            if (fxcm.Status != ConnectStatus.Connected)
            {
                Log.Error("FXCM not connected");

                return false;
            }

            O2GOfferRow offer = GetOffer(fxcm.Session, instrument);
            if (offer == null)
            {
                throw new Exception(string.Format("The instrument '{0}' is not valid", instrument));
            }

            O2GRequest request = null;
            O2GRequestFactory requestFactory = fxcm.Session.getRequestFactory();
            if (requestFactory == null)
            {
                throw new Exception("Cannot create request factory");
            }
            var account = GetAccount(fxcm.Session);
            O2GValueMap valuemap = requestFactory.createValueMap();
            valuemap.setString(O2GRequestParamsEnum.Command, Constants.Commands.CreateOrder);
            valuemap.setString(O2GRequestParamsEnum.OrderType, Constants.Orders.TrueMarketOpen);
            valuemap.setString(O2GRequestParamsEnum.AccountID, account.AccountID);
            valuemap.setString(O2GRequestParamsEnum.OfferID, offer.OfferID);
            valuemap.setString(O2GRequestParamsEnum.BuySell, direction == TradeDirection.Long ? "B" : "S");

            if (rateStop != null)
            {
                valuemap.setDouble(O2GRequestParamsEnum.RateStop, rateStop.Value);
            }

            if (rateLimit != null)
            {
                valuemap.setDouble(O2GRequestParamsEnum.RateLimit, rateLimit.Value);
            }

            // Get account
            var loginRules = fxcm.Session.getLoginRules();
            var tradingSettingsProvider = loginRules.getTradingSettingsProvider();
            int iBaseUnitSize = tradingSettingsProvider.getBaseUnitSize(instrument, account);
            int iAmount = iBaseUnitSize * lotsAmount;

            valuemap.setInt(O2GRequestParamsEnum.Amount, iAmount);
            valuemap.setString(O2GRequestParamsEnum.CustomID, "TrueMarketOrder");
            request = requestFactory.createOrderRequest(valuemap);

            if (request == null)
            {
                Log.Error($"Unable to process request - {requestFactory.getLastError()}");
                return false;
            }

            var ret = ProcessOrderRequest(fxcm, request, out orderId);

            return ret;
        }
    }
}
