using System.Windows;
using System.Windows.Media;
using SciChart.Charting.Visuals.Annotations;

namespace TraderTools.Core.UI.Controls
{
    /// <summary>
    /// Interaction logic for BuyMarkerAnnotation.xaml
    /// </summary>
    public partial class BuyMarkerAnnotation : CustomAnnotation
    {
        public BuyMarkerAnnotation()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty StrokeBrushProperty = DependencyProperty.Register(
            "StrokeBrush", typeof(Brush), typeof(BuyMarkerAnnotation), new PropertyMetadata(Brushes.Green));

        public Brush StrokeBrush
        {
            get { return (Brush) GetValue(StrokeBrushProperty); }
            set { SetValue(StrokeBrushProperty, value); }
        }
    }
}