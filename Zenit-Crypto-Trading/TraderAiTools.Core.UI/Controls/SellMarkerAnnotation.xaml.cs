using System.Windows;
using System.Windows.Media;
using SciChart.Charting.Visuals.Annotations;

namespace TraderTools.Core.UI.Controls
{
    /// <summary>
    /// Interaction logic for SellMarkerAnnotation.xaml
    /// </summary>
    public partial class SellMarkerAnnotation : CustomAnnotation
    {
        public SellMarkerAnnotation()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty StrokeBrushProperty = DependencyProperty.Register(
            "StrokeBrush", typeof(Brush), typeof(SellMarkerAnnotation), new PropertyMetadata(Brushes.Red));

        public Brush StrokeBrush
        {
            get { return (Brush)GetValue(StrokeBrushProperty); }
            set { SetValue(StrokeBrushProperty, value); }
        }
    }
}