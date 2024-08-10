using System;
using System.Windows;
using ICSharpCode.AvalonEdit;
using Microsoft.Xaml.Behaviors;

namespace Hallupa.Library.UI.Behaviours
{
    public sealed class AvalonEditBehaviour : Behavior<TextEditor>
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(AvalonEditBehaviour),
                new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, PropertyChangedCallback));

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject != null)
                AssociatedObject.TextChanged += AssociatedObjectOnTextChanged;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            if (AssociatedObject != null)
                AssociatedObject.TextChanged -= AssociatedObjectOnTextChanged;
        }

        private void AssociatedObjectOnTextChanged(object sender, EventArgs eventArgs)
        {
            var textEditor = sender as TextEditor;
            if (textEditor != null)
            {
                if (textEditor.Document != null)
                    Text = textEditor.Document.Text;
            }
        }

        private static void PropertyChangedCallback(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            var behavior = dependencyObject as AvalonEditBehaviour;
            if (behavior.AssociatedObject != null)
            {
                var editor = behavior.AssociatedObject as TextEditor;
                if (editor.Document != null)
                {
                    var caretOffset = editor.CaretOffset;
                    editor.Document.Text = dependencyPropertyChangedEventArgs.NewValue.ToString();

                    if (caretOffset < editor.Document.Text.Length)
                    {
                        editor.CaretOffset = caretOffset;
                    }
                    else
                    {
                        editor.CaretOffset = 0;
                    }
                }
            }
        }
    }
}
