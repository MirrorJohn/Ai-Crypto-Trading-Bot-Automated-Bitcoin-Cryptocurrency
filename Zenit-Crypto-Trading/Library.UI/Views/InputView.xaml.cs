using System.Windows;

namespace Hallupa.Library.UI.Views
{
    /// <summary>
    /// Interaction logic for InputView.xaml
    /// </summary>
    public partial class InputView : Window
    {
        private bool _okClicked = false;

        public InputView()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty InputTextProperty = DependencyProperty.Register(
            "InputText", typeof(string), typeof(InputView), new PropertyMetadata(default(string)));

        public string InputText
        {
            get { return (string)GetValue(InputTextProperty); }
            set { SetValue(InputTextProperty, value); }
        }

        public static (bool OKClicked, string Text) Show(string title = null, int width = 500)
        {
            var view = new InputView { Owner = Application.Current.MainWindow, Width = width };

            if (title != null)
            {
                view.Title = title;
            }

            view.ShowDialog();

            return (view._okClicked, view.InputText);
        }

        private void OKClicked(object sender, RoutedEventArgs e)
        {
            _okClicked = true;
            Close();
        }

        private void CancelClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}