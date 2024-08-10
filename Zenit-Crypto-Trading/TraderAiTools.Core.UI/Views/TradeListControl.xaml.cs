using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TraderTools.Basics;

namespace TraderTools.Core.UI.Views
{
    [Flags]
    public enum TradeListDisplayOptionsFlag : int
    {
        None = 0,
        PoundsPerPip = 1,
        Quantity = 2,
        Stop = 4,
        Limit = 8,
        OrderDate = 16,
        Broker = 32,
        Comments = 64,
        ResultR = 128,
        OrderPrice = 256,
        ClosePrice = 512,
        Timeframe = 1024,
        EntryValue = 2048,
        Strategies = 4096,
        Risk = 8192,
        Status = 16384,
        Rollover = 32768,
        Dates = 65536,
        Profit = 131072
    }

    /// <summary>
    /// Interaction logic for TradeListControl.xaml
    /// </summary>
    public partial class TradeListControl : UserControl
    {
        public TradeListControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty AllColumnsReadOnlyProperty = DependencyProperty.Register(
            "AllColumnsReadOnly", typeof(bool), typeof(TradeListControl), new PropertyMetadata(default(bool), AllColumnsReadOnlyPropertyChanged));


        private static void AllColumnsReadOnlyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (TradeListControl)d;
            foreach (var column in c.MainDataGrid.Columns)
            {
                column.IsReadOnly = true;
            }
        }

        public bool AllColumnsReadOnly
        {
            get { return (bool)GetValue(AllColumnsReadOnlyProperty); }
            set { SetValue(AllColumnsReadOnlyProperty, value); }
        }

        public static readonly DependencyProperty DisableMouseWheelScrollProperty = DependencyProperty.Register(
            "DisableMouseWheelScroll", typeof(bool), typeof(TradeListControl), new PropertyMetadata(false));

        public bool DisableMouseWheelScroll
        {
            get { return (bool)GetValue(DisableMouseWheelScrollProperty); }
            set { SetValue(DisableMouseWheelScrollProperty, value); }
        }

        private void MainDataGrid_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DisableMouseWheelScroll)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                var parent = ((Control)sender).Parent as UIElement;
                parent.RaiseEvent(eventArg);
            }
        }

        public static readonly DependencyProperty TradeDoubleClickCommandProperty = DependencyProperty.Register(
            "TradeDoubleClickCommand", typeof(ICommand), typeof(TradeListControl), new PropertyMetadata(default(ICommand)));

        public ICommand TradeDoubleClickCommand
        {
            get { return (ICommand) GetValue(TradeDoubleClickCommandProperty); }
            set { SetValue(TradeDoubleClickCommandProperty, value); }
        }

        private void DataGridRowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var row = (DataGridRow)sender;
            var trade = (Trade)row.DataContext;

            e.Handled = true;
            TradeDoubleClickCommand?.Execute(trade);
        }
    }
}