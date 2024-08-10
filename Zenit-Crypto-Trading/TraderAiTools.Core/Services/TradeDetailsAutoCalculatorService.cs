using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.Broker;

namespace TraderTools.Core.Services
{
    [Export(typeof(ITradeDetailsAutoCalculatorService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class TradeDetailsAutoCalculatorService : ITradeDetailsAutoCalculatorService
    {
        private List<Trade> _calculatingTrades = new List<Trade>();

        [Import] private IBrokersCandlesService _candlesService;
        [Import] private IBrokersService _brokersService;
        [Import] private IMarketDetailsService _marketsService;
        public void RemoveTrade(Trade trade)
        {
            trade.PropertyChanged -= TradeOnPropertyChanged;
        }

        private void TradeOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var trade = (Trade)sender;

            lock (_calculatingTrades)
            {
                if (_calculatingTrades.Contains(trade))
                {
                    return;
                }

                _calculatingTrades.Add(trade);
            }

            try
            {
                RecalculateTrade(trade);
            }
            finally
            {
                lock (_calculatingTrades)
                {
                    _calculatingTrades.Remove(trade);
                }
            }
        }

        public void RecalculateTrade(Trade trade)
        {
            // TODO Remove this altogether
            var startTime = trade.OrderDateTime ?? trade.EntryDateTime;
            var broker = _brokersService.Brokers.FirstOrDefault(x => x.Name == trade.Broker);
            var options = trade.CalculateOptions;

            IBrokerAccount brokerAccount = null;
            if (broker != null)
            {
                _brokersService.AccountsLookup.TryGetValue(broker, out brokerAccount);
            }

            if (startTime == null)
            {
                trade.StopInPips = null;
                trade.InitialStopInPips = null;
                trade.InitialStop = null;
                trade.LimitInPips = null;
                trade.InitialLimitInPips = null;
                trade.InitialLimit = null;
                trade.RiskPercentOfBalance = null;
                trade.RiskAmount = null;
                trade.RMultiple = null;
                return;
            }

            UpdateOrderPrice(trade);

            //UpdateStop(trade);

            //UpdateLimit(trade);

            // Update price per pip
            if (!options.HasFlag(CalculateOptions.ExcludePipsCalculations))
            {
                UpdateTradePricePerPip(trade, broker);
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

                if (brokerAccount != null)
                {
                    /* TODO var balance = brokerAccount.GetBalance(trade.StartDateTime);
                    if (balance != 0.0M)
                    {
                        trade.RiskPercentOfBalance = (trade.RiskAmount * 100M) / brokerAccount.GetBalance(startTime);
                    }
                    else
                    {
                        trade.RiskPercentOfBalance = null;
                    }*/
                }
            }
        }

        private void UpdateOrderPrice(Trade trade)
        {
            // Update order prices
            if (trade.OrderPrices.Count > 0)
            {
                trade.OrderPrice = trade.OrderPrices.Last().Price;
            }
            else
            {
                trade.OrderPrice = null;
            }
        }

        private void UpdateTradePricePerPip(Trade trade, IBroker broker)
        {
            // Update price/pip
            if (trade.EntryQuantity != null && trade.EntryDateTime != null)
            {
                if (broker != null)
                {
                    trade.PricePerPip = _candlesService.GetGBPPerPip(_marketsService, broker, trade.Market,
                        trade.EntryQuantity.Value, trade.EntryDateTime.Value, true);
                }
                else
                {
                    trade.PricePerPip = null;
                }
            }
        }
    }
}