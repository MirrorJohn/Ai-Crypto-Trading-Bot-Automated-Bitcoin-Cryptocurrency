using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Hallupa.Library;
using TraderTools.Basics;
using TraderTools.Core.UI.Views;

namespace TraderTools.Core.UI.ViewModels
{
    public class TradeListViewModel : DependencyObject, INotifyPropertyChanged
    {
        public TradeListViewModel()
        {
            TimeFrameItems = new List<Timeframe>
            {
                Timeframe.D1,
                Timeframe.H4,
                Timeframe.H2,
                Timeframe.H1,
                Timeframe.M30,
                Timeframe.M15,
                Timeframe.M5,
                Timeframe.M1
            };

            TradesView = (CollectionView)CollectionViewSource.GetDefaultView(Trades);
            TradesView.Filter = TradesViewFilter;
        }

        public CollectionView TradesView { get; private set; }

        public List<Timeframe> TimeFrameItems { get; set; }

        public TradeListDisplayOptionsFlag TradeListDisplayOptions
        {
            get => _tradeListDisplayOptions;
            set
            {
                _tradeListDisplayOptions = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TradeListDisplayOptions)));
            }
        }

        public ObservableCollectionEx<Trade> Trades { get; } = new ObservableCollectionEx<Trade>();

        private bool TradesViewFilter(object obj)
        {
            var t = (Trade)obj;
            return ((ShowOpenTrades && t.EntryPrice != null && t.CloseDateTime == null)
                   || (ShowOrders && t.OrderPrice != null && t.EntryPrice == null && t.CloseDateTime == null)
                   || (ShowClosedTrades && t.CloseDateTime != null)) && !t.Ignore;
        }

        public static readonly DependencyProperty TradeSelectionModeProperty = DependencyProperty.Register(
            "TradeSelectionMode", typeof(DataGridSelectionMode), typeof(TradeListViewModel), new PropertyMetadata(DataGridSelectionMode.Extended));

        public DataGridSelectionMode TradeSelectionMode
        {
            get { return (DataGridSelectionMode) GetValue(TradeSelectionModeProperty); }
            set { SetValue(TradeSelectionModeProperty, value); }
        }
        public Trade SelectedTrade
        {
            get => _selectedTrade;
            set
            {
                _selectedTrade = value;
                OnPropertyChanged();
            }
        }

        public static readonly DependencyProperty ShowOpenTradesProperty = DependencyProperty.Register("ShowOpenTrades", typeof(bool), typeof(TradeListViewModel), new PropertyMetadata(true, ShowTradesChanged));
        public static readonly DependencyProperty ShowClosedTradesProperty = DependencyProperty.Register("ShowClosedTrades", typeof(bool), typeof(TradeListViewModel), new PropertyMetadata(true, ShowTradesChanged));
        public static readonly DependencyProperty ShowOrdersProperty = DependencyProperty.Register("ShowOrders", typeof(bool), typeof(TradeListViewModel), new PropertyMetadata(true, ShowTradesChanged));

        private static void ShowTradesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tvm = (TradeListViewModel)d;
            tvm.TradesView.Refresh();
        }

        public bool ShowOpenTrades
        {
            get { return (bool)GetValue(ShowOpenTradesProperty); }
            set { SetValue(ShowOpenTradesProperty, value); }
        }

        public bool ShowClosedTrades
        {
            get { return (bool)GetValue(ShowClosedTradesProperty); }
            set { SetValue(ShowClosedTradesProperty, value); }
        }

        public bool ShowOrders
        {
            get { return (bool)GetValue(ShowOrdersProperty); }
            set { SetValue(ShowOrdersProperty, value); }
        }

        private Window _parent;
        private Trade _selectedTrade;
        private TradeListDisplayOptionsFlag _tradeListDisplayOptions = TradeListDisplayOptionsFlag.PoundsPerPip | TradeListDisplayOptionsFlag.Stop
            | TradeListDisplayOptionsFlag.Limit
            | TradeListDisplayOptionsFlag.OrderPrice
            | TradeListDisplayOptionsFlag.OrderDate
            | TradeListDisplayOptionsFlag.Comments
            | TradeListDisplayOptionsFlag.ResultR
            | TradeListDisplayOptionsFlag.Broker
            | TradeListDisplayOptionsFlag.Timeframe
            | TradeListDisplayOptionsFlag.Strategies
            | TradeListDisplayOptionsFlag.Risk
            | TradeListDisplayOptionsFlag.Status
            | TradeListDisplayOptionsFlag.ClosePrice
            | TradeListDisplayOptionsFlag.Dates
            | TradeListDisplayOptionsFlag.Profit;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}