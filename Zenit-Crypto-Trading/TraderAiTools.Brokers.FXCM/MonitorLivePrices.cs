using System;
using System.Reflection;
using fxcore2;
using log4net;

namespace TraderTools.Brokers.FXCM
{
    public class MonitorLivePrices : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly FxcmBroker _fxcm;
        private readonly Action<(string Instrument, double Bid, double Ask, DateTime Time)> _processUpdateAction;
        private ResponseListener _responseListener;
        private O2GResponseReaderFactory _readerFactory;
        private bool _disposed;
        private const int LogIntervalSeconds = 300;
        private int _pricesReceivedInInterval;
        private DateTime _nextLogTime; 

        public MonitorLivePrices(
            FxcmBroker fxcm,
            Action<(string Instrument, double Bid, double Ask, DateTime Time)> processUpdateAction)
        {
            _fxcm = fxcm;
            _processUpdateAction = processUpdateAction;
            _nextLogTime = DateTime.UtcNow.AddSeconds(LogIntervalSeconds);

            _responseListener = new ResponseListener(_fxcm.Session, ProcessResponse);
            _fxcm.Session.subscribeResponse(_responseListener);
            GetLatestOffer();
        }

        private void ProcessResponse(O2GResponse resp)
        {
            if (_disposed) return;

            if (resp.Type == O2GResponseType.TablesUpdates || resp.Type == O2GResponseType.GetOffers)
            {
                if (_readerFactory == null) _readerFactory = _fxcm.Session.getResponseReaderFactory();
                if (_readerFactory == null)
                {
                    throw new Exception("Cannot create response reader factory");
                }

                var responseReader = _readerFactory.createOffersTableReader(resp);
                for (int i = 0; i < responseReader.Count; i++)
                {
                    O2GOfferRow offerRow = responseReader.getRow(i);
                    if (offerRow.isTimeValid && offerRow.isBidValid && offerRow.isAskValid)
                    {
                        _pricesReceivedInInterval++;
                        _processUpdateAction((
                            offerRow.Instrument,
                            offerRow.Bid,
                            offerRow.Ask,
                            offerRow.Time));
                    }
                }
            }

            if (DateTime.UtcNow >= _nextLogTime)
            {
                Log.Info($"{_pricesReceivedInInterval} prices received in last {LogIntervalSeconds}s ");
                _pricesReceivedInInterval = 0;
                _nextLogTime = DateTime.UtcNow.AddSeconds(LogIntervalSeconds);
            }
        }

        /// <summary>
        /// Get the latest offer to which the user is subscribed
        /// </summary>
        private void GetLatestOffer()
        {
            // Get the list of the offers to which the user is subscribed
            var loginRules = _fxcm.Session.getLoginRules();
            var response = loginRules.getSystemPropertiesResponse();

            if (loginRules.isTableLoadedByDefault(O2GTableType.Offers))
            {
                // If it is already loaded - just handle them
                response = loginRules.getTableRefreshResponse(O2GTableType.Offers);
                ProcessResponse(response);
            }
            else
            {
                // Otherwise create the request to get offers from the server
                var factory = _fxcm.Session.getRequestFactory();
                var offerRequest = factory.createRefreshTableRequest(O2GTableType.Offers);
                _fxcm.Session.sendRequest(offerRequest);
            }
        }

        public void Dispose()
        {
            _fxcm.Session.unsubscribeResponse(_responseListener);
            _disposed = true;
        }
    }
}