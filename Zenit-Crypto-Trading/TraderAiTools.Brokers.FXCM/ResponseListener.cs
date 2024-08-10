using System;
using System.Threading;
using fxcore2;

namespace TraderTools.Brokers.FXCM
{
    internal class ResponseListener : IO2GResponseListener
    {
        private O2GSession mSession;
        private readonly Action<O2GResponse> _tableUpdateAction;
        private string mRequestID;
        private O2GResponse mResponse;
        private EventWaitHandle mSyncResponseEvent;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="session"></param>
        public ResponseListener(O2GSession session, Action<O2GResponse> tableUpdateAction = null)
        {
            mRequestID = string.Empty;
            mResponse = null;
            mSyncResponseEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
            mSession = session;
            _tableUpdateAction = tableUpdateAction;
        }

        public string Error { get; set; } = null;

        public void SetRequestID(string sRequestID)
        {
            mResponse = null;
            mRequestID = sRequestID;
            mSyncResponseEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
            Error = null;
        }

        public bool WaitEvents()
        {
            return mSyncResponseEvent.WaitOne(60000);
        }

        public O2GResponse GetResponse()
        {
            return mResponse;
        }

        #region IO2GResponseListener Members

        public void onRequestCompleted(string sRequestId, O2GResponse response)
        {
            if (mRequestID.Equals(response.RequestID))
            {
                mResponse = response;
                mSyncResponseEvent.Set();
            }
        }

        public void onRequestFailed(string sRequestID, string sError)
        {
            if (mRequestID.Equals(sRequestID))
            {
                mResponse = null;

                Error = string.IsNullOrEmpty(sError) ? null : sError;

                mSyncResponseEvent.Set();
            }
        }

        public void onTablesUpdates(O2GResponse data)
        {
            _tableUpdateAction?.Invoke(data);
        }

        #endregion

    }

}
