using System;
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hallupa.Library;
using Hallupa.Library.UI;
using SciChart.Charting.ChartModifiers;
using SciChart.Charting.Numerics.CoordinateCalculators;
using SciChart.Charting.Visuals;
using SciChart.Charting.Visuals.Annotations;
using SciChart.Charting.Visuals.Events;
using SciChart.Charting.Visuals.TradeChart;
using SciChart.Core.Utility.Mouse;
using TraderTools.Core.UI.Services;

namespace TraderTools.Core.UI.ChartModifiers
{
    /// <summary>
    /// Always use indexes for X properties on LineAnnotations. If use DateTime, it can cause issues when the line
    /// goes off the surface when scrolling and the X co-ordinate doesn't exactly match-up with an X-axis data point.
    /// </summary>
    public class AddLinesModifier : ChartModifierBase
    {
        public const double StrokeThickness = 4;
        public static readonly Brush Stroke = Brushes.Black;
        public const double Opacity = 0.6;

        private LineAnnotation _currentLine;
        private LineAnnotation _currentLinkedLine;
        private ISciChartSurface _linkedSurface;
        private IChartModifierSurface _linkedModifierSurface;

        [Import] private ChartingService _chartingService;

        public string LinkedChartGroupName { get; set; }


        public ISciChartSurface LinkedChartSurface
        {
            get
            {
                if (_linkedSurface != null) return _linkedSurface;
                if (string.IsNullOrEmpty(LinkedChartGroupName)) return null;

                var top = VisualHelper.GetTopParent((Grid)ParentSurface.RootGrid);
                var group = VisualHelper.FindChild<SciChartGroup>(top, LinkedChartGroupName);
                var surface = VisualHelper.GetChildOfType<SciChartSurface>(group);
                _linkedSurface = surface;
                return _linkedSurface;
            }
        }

        public IChartModifierSurface LinkedModifierChartSurface
        {
            get
            {
                if (_linkedModifierSurface != null) return _linkedModifierSurface;
                if (string.IsNullOrEmpty(LinkedChartGroupName)) return null;

                var top = VisualHelper.GetTopParent((Grid)ParentSurface.RootGrid);
                var group = VisualHelper.FindChild<SciChartGroup>(top, LinkedChartGroupName);
                var surface = VisualHelper.GetChildOfType<ChartModifierSurface>(group);
                _linkedModifierSurface = surface;
                return _linkedModifierSurface;
            }
        }

        public AddLinesModifier()
        {
            DependencyContainer.ComposeParts(this);
        }

        public override void OnAttached()
        {
            base.OnAttached();

            Loaded += OnLoaded;
        }

        /// <summary>
        /// Loaded can be called multiple times.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ParentSurface.Annotations == null) return;

            if (LinkedChartSurface != null) LinkedChartSurface.Annotations.CollectionChanged -= AnnotationsOnCollectionChanged;
            ParentSurface.Annotations.CollectionChanged -= AnnotationsOnCollectionChanged;

            if (LinkedChartSurface != null) LinkedChartSurface.Annotations.CollectionChanged += AnnotationsOnCollectionChanged;
            ParentSurface.Annotations.CollectionChanged += AnnotationsOnCollectionChanged;

            if (LinkedChartSurface != null && LinkedChartSurface.Annotations.Count > 0)
            {
                AnnotationsOnCollectionChanged(sender, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, LinkedChartSurface.Annotations.ToList()));
            }

            if (ParentSurface.Annotations.Count > 0)
            {
                AnnotationsOnCollectionChanged(sender, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, ParentSurface.Annotations.ToList()));
            }
        }

        private void AnnotationsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var l in e.NewItems.OfType<LineAnnotation>().Where(x => x.Tag != null && x.Tag is string && ((string) x.Tag).StartsWith("Added")))
                {
                    l.DragDelta -= CurrentLineOnDragDelta;
                    l.DragDelta += CurrentLineOnDragDelta;

                    // Don't raise RaiseChartLinesChanged here as the controls are not ready. The delta changed event will raise RaiseChartLinesChanged
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var l in e.OldItems.OfType<LineAnnotation>().Where(x => x.Tag != null && x.Tag is string && ((string)x.Tag).StartsWith("Added")))
                {
                    l.DragDelta -= CurrentLineOnDragDelta;
                }

                _chartingService.RaiseChartLinesChanged();
            }
        }

        public override void OnModifierMouseDown(ModifierMouseArgs e)
        {
            if (_currentLine == null)
            {
                if (_chartingService.ChartMode == ChartMode.AddLine)
                {
                    var xy = GetXY(e.MousePoint, ParentSurface, ModifierSurface);
                    var id = Guid.NewGuid();
                    _currentLine = CreateLine(e, ParentSurface, xy.X, xy.Y, id);

                    if (LinkedChartSurface != null)
                    {
                        _currentLinkedLine = CreateLine(e, LinkedChartSurface, xy.X, xy.Y, id);
                    }

                    e.Handled = true;
                }
                else
                {
                    e.Handled = false;
                }
            }
            else
            {
                _currentLine.IsEditable = true;
                _currentLine = null;
                if (_currentLinkedLine != null)
                {
                    _currentLinkedLine.IsEditable = true;
                    _currentLinkedLine = null;
                }

                _chartingService.ChartMode = null;
                e.Handled = true;

                _chartingService.RaiseChartLinesChanged();
            }
        }

        private LineAnnotation CreateLine(ModifierMouseArgs e, ISciChartSurface surface, IComparable x, IComparable y, Guid id)
        {
            var currentLine = new LineAnnotation
            {
                Tag = "Added_" + id,
                StrokeThickness = StrokeThickness,
                Opacity = Opacity,
                Stroke = Stroke,
                X1 = x is DateTime t1 ? ((ICategoryCoordinateCalculator)surface.XAxis.GetCurrentCoordinateCalculator()).TransformDataToIndex(t1) : x,
                Y1 = y,
                X2 = x is DateTime t2 ? ((ICategoryCoordinateCalculator)surface.XAxis.GetCurrentCoordinateCalculator()).TransformDataToIndex(t2) : x,
                Y2 = y
            };

            surface.Annotations.Add(currentLine);

            return currentLine;
        }

        private void CurrentLineOnDragDelta(object sender, AnnotationDragDeltaEventArgs e)
        {
            if (LinkedChartSurface == null) return;

            if (sender is LineAnnotation line && line.Tag != null && ((string)line.Tag).StartsWith("Added"))
            {
                if (line == _currentLine || line == _currentLinkedLine) return;

                var otherLine = ParentSurface.Annotations.OfType<LineAnnotation>().FirstOrDefault(x => x.Tag != null && x.Tag.Equals(line.Tag));

                if (otherLine == line)
                {
                    otherLine = LinkedChartSurface.Annotations.OfType<LineAnnotation>().FirstOrDefault(x => x.Tag != null && x.Tag.Equals(line.Tag));
                }

                if (otherLine != null)
                {
                    var categoryCoordCalc = (ICategoryCoordinateCalculator)line.ParentSurface.XAxis.GetCurrentCoordinateCalculator();
                    var otherCategoryCoordCalc = (ICategoryCoordinateCalculator)otherLine.ParentSurface.XAxis.GetCurrentCoordinateCalculator();

                    if (line.X1 is DateTime time1)
                    {
                        otherLine.X1 = otherCategoryCoordCalc.TransformDataToIndex(time1);
                    }
                    else
                    {
                        otherLine.X1 = otherCategoryCoordCalc.TransformDataToIndex(categoryCoordCalc.TransformIndexToData((int)line.X1));
                    }

                    if (line.X2 is DateTime time2)
                    {
                        otherLine.X2 = otherCategoryCoordCalc.TransformDataToIndex(time2);
                    }
                    else
                    {
                        otherLine.X2 = otherCategoryCoordCalc.TransformDataToIndex(categoryCoordCalc.TransformIndexToData((int)line.X2));
                    }

                    otherLine.Y1 = line.Y1;
                    otherLine.Y2 = line.Y2;
                }

                _chartingService.RaiseChartLinesChanged();
            }
        }

        public override void OnModifierMouseMove(ModifierMouseArgs e)
        {
            if (_currentLine != null)
            {
                var xy = GetXY(e.MousePoint, ParentSurface, ModifierSurface);
                _currentLine.X2 = xy.X is DateTime t1 ? ((ICategoryCoordinateCalculator)_currentLine.ParentSurface.XAxis.GetCurrentCoordinateCalculator()).TransformDataToIndex(t1) : xy.X;
                _currentLine.Y2 = xy.Y;

                if (_currentLinkedLine != null)
                {
                    _currentLinkedLine.X2 = xy.X is DateTime t2 ? ((ICategoryCoordinateCalculator)_currentLinkedLine.ParentSurface.XAxis.GetCurrentCoordinateCalculator()).TransformDataToIndex(t2) : xy.X;
                    _currentLinkedLine.Y2 = xy.Y;
                }
            }
        }

        private (IComparable X, IComparable Y) GetXY(Point initialMousePoint, ISciChartSurface surface, IChartModifierSurface modifierSurface)
        {
            var mousePoint = GetPointRelativeTo(initialMousePoint, modifierSurface);

            var x = mousePoint.X;
            var y = mousePoint.Y;
            var chartX = surface.XAxis.GetDataValue(x);
            var chartY = surface.YAxis.GetDataValue(y);
            return (chartX, chartY);
        }
    }
}