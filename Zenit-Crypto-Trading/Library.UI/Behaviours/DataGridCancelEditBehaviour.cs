using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace Hallupa.Library.UI.Behaviours
{
    public class DataGridCancelEditBehaviour : Behavior<DataGrid>
    {
        public static readonly DependencyProperty CancelEditCommandProperty = DependencyProperty.Register(
            "CancelEditCommand", typeof(ICommand), typeof(DataGridCancelEditBehaviour), new PropertyMetadata(default(ICommand), CancelEditCommandChanged));

        public DataGridCancelEditBehaviour()
        {
        }

        protected override void OnAttached()
        {
            CancelEditCommand = new DelegateCommand(CancelEdit);
        }

        private  void CancelEdit(object obj)
        {
            AssociatedObject.CancelEdit();
        }

        private static void CancelEditCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
        }

        public ICommand CancelEditCommand
        {
            get { return (ICommand) GetValue(CancelEditCommandProperty); }
            set { SetValue(CancelEditCommandProperty, value); }
        }
    }
}