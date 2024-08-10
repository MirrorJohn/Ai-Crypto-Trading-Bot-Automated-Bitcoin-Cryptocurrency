using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Hallupa.Library.UI.TokenEditor
{
    /// <summary>
    /// Interaction logic for TokenContainerControl.xaml
    /// </summary>
    public partial class TokenContainerControl
    {
        public TokenContainerControl()
        {
            InitializeComponent();

            DeleteTokenCommand = new DelegateCommand(DeleteToken);

            Loaded += (sender, args) => StopEdit(this);
        }

        private void DeleteToken(object obj)
        {
            var item = (string)obj;
            SelectedItemsCSV = string.Join(",", SelectedItems.Cast<string>().Where(c => c != item));
        }

        public static readonly DependencyProperty DeleteTokenCommandProperty = DependencyProperty.Register(
            "DeleteTokenCommand", typeof(ICommand), typeof(TokenContainerControl), new PropertyMetadata(default(ICommand)));

        public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.Register(
            "SelectedItems", typeof(IEnumerable), typeof(TokenContainerControl), new FrameworkPropertyMetadata(default(IEnumerable), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public IEnumerable SelectedItems
        {
            get { return (IEnumerable)GetValue(SelectedItemsProperty); }
            set { SetValue(SelectedItemsProperty, value); }
        }

        public static readonly DependencyProperty SelectedItemsCSVProperty = DependencyProperty.Register(
            "SelectedItemsCSV", typeof(string), typeof(TokenContainerControl),
            new PropertyMetadata(default(string), SelectedItemsCSVChanged));

        private static void SelectedItemsCSVChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (TokenContainerControl)d;
            control.SelectedItems = control.SelectedItemsCSV != null
                ? control.SelectedItemsCSV.Split(',').Where(x => !string.IsNullOrEmpty(x)).ToList()
                : new List<string>();
        }

        public string SelectedItemsCSV
        {
            get { return (string)GetValue(SelectedItemsCSVProperty); }
            set { SetValue(SelectedItemsCSVProperty, value); }
        }

        public ICommand DeleteTokenCommand
        {
            get { return (ICommand)GetValue(DeleteTokenCommandProperty); }
            set { SetValue(DeleteTokenCommandProperty, value); }
        }

        public static readonly DependencyProperty TextBoxTextProperty = DependencyProperty.Register(
            "TextBoxText", typeof(string), typeof(TokenContainerControl),
            new PropertyMetadata(default(string)));


        public string TextBoxText
        {
            get { return (string)GetValue(TextBoxTextProperty); }
            set { SetValue(TextBoxTextProperty, value); }
        }

        private void TextBoxOnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddItem(this);
                TextBoxText = string.Empty;
                var tb = VisualHelper.FindChild<TextBox>(this, "MainTextBox");
                tb.Text = string.Empty;
                StopEdit(this);
                e.Handled = true;
            }
        }

        private static void AddItem(TokenContainerControl userControl)
        {
            var tb = VisualHelper.FindChild<TextBox>(userControl, "MainTextBox");
            var item = tb.Text;
            if (string.IsNullOrEmpty(item))
            {
                return;
            }

            // Add token
            var currentItems = userControl.SelectedItemsCSV != null ? userControl.SelectedItemsCSV.Split(',') : new string[] { };
            if (!currentItems.Contains(item))
            {
                userControl.SelectedItemsCSV = string.Join(",", currentItems.Where(x => !string.IsNullOrEmpty(x)).Union(new List<string> { item }));
            }

            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke((Action)(() =>
            {
                userControl.TextBoxText = string.Empty;
            }));
        }

        private void TextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            AddItem(this);
            TextBoxText = string.Empty;
            StopEdit(this);
        }

        private static void StartEdit(TokenContainerControl control)
        {
            var tb = control.MainTextBox;
            var grid = (Grid)tb.Parent;

            grid.ColumnDefinitions[0].Width = new GridLength(3, GridUnitType.Star);
            grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);

            tb.Focus();
        }

        private static void StopEdit(TokenContainerControl control)
        {
            var tb = VisualHelper.FindChild<TextBox>(control, "MainTextBox");
            var grid = (Grid)tb.Parent;
            grid.ColumnDefinitions[0].Width = new GridLength(0);
            grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
        }

        private void TokenContainerControl_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var control = (TokenContainerControl)sender;
            StartEdit(control);
        }
    }
}
