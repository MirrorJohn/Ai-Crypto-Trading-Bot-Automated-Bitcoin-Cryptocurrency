using System.Windows;

namespace Hallupa.Library.UI.ViewModels
{
    public class ProgressViewModel : DependencyObject
    {
        public static readonly DependencyProperty MainTextProperty = DependencyProperty.Register(
            "MainText", typeof(string), typeof(ProgressViewModel), new PropertyMetadata(default(string)));

        public string MainText
        {
            get { return (string) GetValue(MainTextProperty); }
            set { SetValue(MainTextProperty, value); }
        }

        public static readonly DependencyProperty SecondaryTextProperty = DependencyProperty.Register(
            "SecondaryText", typeof(string), typeof(ProgressViewModel), new PropertyMetadata(default(string)));

        public string SecondaryText
        {
            get { return (string) GetValue(SecondaryTextProperty); }
            set { SetValue(SecondaryTextProperty, value); }
        }
    }
}