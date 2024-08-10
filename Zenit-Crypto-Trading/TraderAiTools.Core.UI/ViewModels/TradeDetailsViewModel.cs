using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Windows;
using Hallupa.Library;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;

namespace TraderTools.Core.UI.ViewModels
{
    public class TradeDetailsViewModel : INotifyPropertyChanged
    {
        #region Fields
        private readonly Action _closeWindow;
        private Trade _trade;
        [Import] private IBrokersService _brokersService;
        [Import] private IBrokersCandlesService _candlesService;
        [Import] private IMarketDetailsService _marketDetailsService;
        private IBroker _broker;
        private IBrokerAccount _brokerAccount;

        #endregion

        #region Constructors
        public TradeDetailsViewModel(Trade trade, Action closeWindow)
        {
            _closeWindow = closeWindow;
            DependencyContainer.ComposeParts(this);

            Trade = trade;
            Date = Trade.StartDateTimeLocal != null
                ? Trade.StartDateTimeLocal.Value.ToString("dd/MM/yy HH:mm")
                : DateTime.Now.ToString("dd/MM/yy HH:mm");

            RefreshDetails();

            _broker = _brokersService.GetBroker(trade.Broker);
            _brokerAccount = _brokersService.AccountsLookup[_broker];

            AddLimitCommand= new DelegateCommand(AddLimit);
            AddStopCommand = new DelegateCommand(AddStop);
            RemoveLimitCommand = new DelegateCommand(RemoveLimit);
            RemoveStopCommand = new DelegateCommand(RemoveStop);
            SetOrderDateTimePriceCommand = new DelegateCommand(SetOrderDateTimePrice);
            DoneCommand = new DelegateCommand(o => Done());
        }

        #endregion

        #region Properties

        public Trade Trade
        {
            get => _trade;
            set
            {
                _trade = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<DatePrice> LimitPrices { get; } = new ObservableCollection<DatePrice>();
        public ObservableCollection<DatePrice> StopPrices { get; } = new ObservableCollection<DatePrice>();
        public DelegateCommand AddLimitCommand { get; }
        public DelegateCommand AddStopCommand { get; }
        public DelegateCommand DoneCommand { get; }
        public DelegateCommand RemoveLimitCommand { get; }
        public DelegateCommand RemoveStopCommand { get; }
        public DelegateCommand SetOrderDateTimePriceCommand { get; }
        public int SelectedLimitIndex { get; set; }
        public int SelectedStopIndex { get; set; }
        public string Date { get; set; }
        public string Price { get; set; }
        public bool UsePips { get; set; }
        #endregion

        private void RefreshDetails()
        {
            DependencyContainer.ComposeParts(this);

            LimitPrices.Clear();
            foreach (var limitPrice in Trade.LimitPrices)
            {
                LimitPrices.Add(new DatePrice(limitPrice.Date, limitPrice.Price));
            }

            StopPrices.Clear();
            foreach (var stopPrice in Trade.StopPrices)
            {
                StopPrices.Add(new DatePrice(stopPrice.Date, stopPrice.Price));
            }
        }

        private void RemoveLimit(object obj)
        {
            if (SelectedLimitIndex == -1)
            {
                return;
            }

            Trade.RemoveLimitPrice(SelectedLimitIndex);
            RefreshDetails();
        }

        private void RemoveStop(object obj)
        {
            if (SelectedStopIndex == -1)
            {
                return;
            }

            Trade.RemoveStopPrice(SelectedStopIndex);
            RefreshDetails();
        }

        private void Done()
        {
            _closeWindow();
        }

        private void AddLimit(object obj)
        {
            var date = GetDatetime();
            var price = Trade.TradeDirection == TradeDirection.Long ? GetPrice(PipsChange.Add) : GetPrice(PipsChange.Minus);
            if (date == null || price == null)
            {
                MessageBox.Show("Invalid details", "Invalid details", MessageBoxButton.OK);
                return;
            }

            Trade.AddLimitPrice(date.Value, price.Value);
            RefreshDetails();
        }

        private void AddStop(object obj)
        {
            var date = GetDatetime();
            var price = Trade.TradeDirection == TradeDirection.Long ? GetPrice(PipsChange.Minus) : GetPrice(PipsChange.Add);
            if (date == null || price == null)
            {
                MessageBox.Show("Invalid details", "Invalid details", MessageBoxButton.OK);
                return;
            }

            Trade.AddStopPrice(date.Value, price.Value);
            RefreshDetails();
        }

        private void SetOrderDateTimePrice(object obj)
        {
            var date = GetDatetime();
            Trade.OrderDateTime = date;

            if (date != null)
            {
                Trade.AddOrderPrice(date.Value, GetPrice());
            }

            // Refresh by changing Trade
            var t = Trade;
            Trade = null;
            Trade = t;
        }

        public enum PipsChange
        {
            Add,
            Minus
        }

        private decimal? GetPrice(PipsChange pipsChange = PipsChange.Add)
        {
            if (!decimal.TryParse(Price, out var decimalPrice))
            {
                return null;
            }

            if (UsePips)
            {
                var tradeStartPrice = Trade.OrderPrice ?? Trade.EntryPrice.Value;
                var broker = _brokersService.GetBroker(Trade.Broker);
                var priceInPips = _marketDetailsService.GetPriceInPips(_broker.Name, tradeStartPrice, Trade.Market);
                priceInPips += pipsChange == PipsChange.Add ? decimalPrice : -decimalPrice;

                return _marketDetailsService.GetPriceFromPips(_broker.Name, priceInPips, Trade.Market);
            }

            return decimalPrice;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private DateTime? GetDatetime()
        {
            if (DateTime.TryParse(Date, out var date))
            {
                return date.ToUniversalTime();
            }

            return null;
        }
    }
}