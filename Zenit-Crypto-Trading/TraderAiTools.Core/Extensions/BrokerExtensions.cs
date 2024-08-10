using System;
using System.Threading;
using TraderTools.Basics;

namespace TraderTools.Core.Extensions
{
    public static class BrokerExtensions
    {
        public static void WaitForStatus(this IBroker broker, ConnectStatus status)
        {
            var maxWait = DateTime.UtcNow.AddSeconds(30);
            while (broker.Status != status && DateTime.UtcNow <= maxWait)
            {
                Thread.Sleep(100);
            }

            if (broker.Status != status)
            {
                throw new ApplicationException($"Broker status not {status}");
            }
        }
    }
}