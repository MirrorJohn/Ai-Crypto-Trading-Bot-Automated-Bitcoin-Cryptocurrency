using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Hallupa.Library.UI.TokenEditor
{
    /// <summary>
    /// Interaction logic for TokenControl.xaml
    /// </summary>
    public partial class TokenControl : UserControl
    {
        public TokenControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty DeleteCommandProperty = DependencyProperty.Register(
            "DeleteCommand", typeof(ICommand), typeof(TokenControl), new PropertyMetadata(default(ICommand)));

        public ICommand DeleteCommand
        {
            get { return (ICommand) GetValue(DeleteCommandProperty); }
            set { SetValue(DeleteCommandProperty, value); }
        }
    }
}