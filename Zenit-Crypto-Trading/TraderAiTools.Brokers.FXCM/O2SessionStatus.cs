using System;
using System.Reflection;
using System.Threading.Tasks;
using fxcore2;
using log4net;

namespace TraderTools.Brokers.FXCM
{
    internal class O2SessionStatus : IO2GSessionStatus
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Action _connectedAction;
        private readonly Action _connectionFailed;

        public O2SessionStatus(Action connectedAction, Action connectionFailed)
        {
            _connectedAction = connectedAction;
            _connectionFailed = connectionFailed;
        }

        public void onLoginFailed(string error)
        {
            Log.Error($"Failed to login to FXCM: {error}");

            Task.Run(() =>
            {
                _connectionFailed();
            });
        }

        public void onSessionStatusChanged(O2GSessionStatusCode status)
        {
            Log.Debug($"FXCM session status changed: {status}");
            if (status == O2GSessionStatusCode.Connected)
            {
                Task.Run(() =>
                {
                    _connectedAction();
                });
            }
            else if (status == O2GSessionStatusCode.Disconnected
                     || status == O2GSessionStatusCode.Disconnecting
                     || status == O2GSessionStatusCode.SessionLost)
            {
                Task.Run(() =>
                {
                    _connectionFailed();
                });
            }
        }
    }
}