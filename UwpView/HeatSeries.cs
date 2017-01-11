﻿//The MIT License(MIT)

//Copyright(c) 2016 Alberto Rodriguez

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using LiveCharts.Definitions.Points;
using LiveCharts.Definitions.Series;
using LiveCharts.Dtos;
using LiveCharts.Helpers;
using LiveCharts.SeriesAlgorithms;
using LiveCharts.Uwp.Charts.Base;
using LiveCharts.Uwp.Points;
using LiveCharts.Uwp.Components;

namespace LiveCharts.Uwp
{
    /// <summary>
    /// Use a HeatSeries in a cartesian chart to draw heat maps.
    /// </summary>
    public class HeatSeries : Series, IHeatSeriesView
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of HeatSeries class
        /// </summary>
        public HeatSeries()
        {
            Model = new HeatAlgorithm(this);
            InitializeDefuaults();
        }

        /// <summary>
        /// Initializes a new instance of HeatSries class, using a given mapper
        /// </summary>
        /// <param name="configuration"></param>
        public HeatSeries(object configuration)
        {
            Model = new HeatAlgorithm(this);
            Configuration = configuration;
            InitializeDefuaults();
        }

        #endregion

        #region Private Properties

        private HeatColorRange ColorRangeControl { get; set; }
        #endregion

        #region Properties

        /// <summary>
        /// The draws heat range property
        /// </summary>
        public static readonly DependencyProperty DrawsHeatRangeProperty = DependencyProperty.Register(
            "DrawsHeatRange", typeof(bool), typeof(HeatSeries),
            new PropertyMetadata(true, CallChartUpdater()));
        /// <summary>
        /// Gets or sets whether the series should draw the heat range control, it is the vertical frame to the right that displays the heat gradient.
        /// </summary>
        public bool DrawsHeatRange
        {
            get { return (bool)GetValue(DrawsHeatRangeProperty); }
            set { SetValue(DrawsHeatRangeProperty, value); }
        }

        /// <summary>
        /// The gradient stop collection property
        /// </summary>
        public static readonly DependencyProperty GradientStopCollectionProperty = DependencyProperty.Register(
            "GradientStopCollection", typeof(GradientStopCollection), typeof(HeatSeries), new PropertyMetadata(default(GradientStopCollection)));
        /// <summary>
        /// Gets or sets the gradient stop collection, use every gradient offset and color properties to define your gradient.
        /// </summary>
        public GradientStopCollection GradientStopCollection
        {
            get { return (GradientStopCollection)GetValue(GradientStopCollectionProperty); }
            set { SetValue(GradientStopCollectionProperty, value); }
        }

        /// <summary>
        /// Gets the gradient stops, this property is normally used internally to communicate with the core of the library.
        /// </summary>
        public IList<CoreGradientStop> Stops
        {
            get
            {
                return GradientStopCollection.Select(x => new CoreGradientStop
                {
                    Offset = x.Offset,
                    Color = new CoreColor(x.Color.A, x.Color.R, x.Color.G, x.Color.B)
                }).ToArray();
            }
        }

        #endregion

        #region Overridden Methods

        /// <summary>
        /// Gets the view of a given point
        /// </summary>
        /// <param name="point"></param>
        /// <param name="label"></param>
        /// <returns></returns>
        public override IChartPointView GetPointView(ChartPoint point, string label)
        {
            var pbv = (HeatPoint) point.View;

            if (pbv == null)
            {
                pbv = new HeatPoint
                {
                    IsNew = true,
                    Rectangle = new Rectangle()
                };

                Model.Chart.View.AddToDrawMargin(pbv.Rectangle);
            }
            else
            {
                pbv.IsNew = false;
                point.SeriesView.Model.Chart.View
                    .EnsureElementBelongsToCurrentDrawMargin(pbv.Rectangle);
                point.SeriesView.Model.Chart.View
                    .EnsureElementBelongsToCurrentDrawMargin(pbv.HoverShape);
                point.SeriesView.Model.Chart.View
                    .EnsureElementBelongsToCurrentDrawMargin(pbv.DataLabel);
            }

            pbv.Rectangle.Stroke = Stroke;
            pbv.Rectangle.StrokeThickness = StrokeThickness;
            pbv.Rectangle.Visibility = Visibility;
            pbv.Rectangle.StrokeDashArray = StrokeDashArray;

            Canvas.SetZIndex(pbv.Rectangle, Canvas.GetZIndex(pbv.Rectangle));

            if (Model.Chart.RequiresHoverShape && pbv.HoverShape == null)
            {
                pbv.HoverShape = new Rectangle
                {
                    Fill = new SolidColorBrush(Colors.Transparent),
                    StrokeThickness = 0
                };

                Canvas.SetZIndex(pbv.HoverShape, short.MaxValue);

                var uwpfChart = (Chart)Model.Chart.View;
                uwpfChart.AttachHoverableEventTo(pbv.HoverShape);

                Model.Chart.View.AddToDrawMargin(pbv.HoverShape);
            }

            if (pbv.HoverShape != null) pbv.HoverShape.Visibility = Visibility;

            if (DataLabels && pbv.DataLabel == null)
            {
                pbv.DataLabel = BindATextBlock(0);
                Canvas.SetZIndex(pbv.DataLabel, short.MaxValue - 1);

                Model.Chart.View.AddToDrawMargin(pbv.DataLabel);
            }

            if (pbv.DataLabel != null) pbv.DataLabel.Text = label;

            return pbv;
        }

        /// <summary>
        /// Erases series
        /// </summary>
        /// <param name="removeFromView"></param>
        public override void Erase(bool removeFromView = true)
        {
            Values.GetPoints(this).ForEach(p =>
            {
                p.View?.RemoveFromView(Model.Chart);
            });
            if (removeFromView) Model.Chart.View.RemoveFromView(this);
        }

        /// <summary>
        /// Defines special elements to draw according to the series type
        /// </summary>
        public override void DrawSpecializedElements()
        {
            if (DrawsHeatRange)
            {
                if (ColorRangeControl == null)
                {
                    ColorRangeControl = new HeatColorRange();
                }

                ColorRangeControl.SetBinding(TextBlock.FontFamilyProperty,
                    new Binding {Path = new PropertyPath("FontFamily"), Source = this});
                ColorRangeControl.SetBinding(TextBlock.FontSizeProperty,
                    new Binding {Path = new PropertyPath("FontSize"), Source = this});
                ColorRangeControl.SetBinding(TextBlock.FontStretchProperty,
                    new Binding {Path = new PropertyPath("FontStretch"), Source = this});
                ColorRangeControl.SetBinding(TextBlock.FontStyleProperty,
                    new Binding {Path = new PropertyPath("FontStyle"), Source = this});
                ColorRangeControl.SetBinding(TextBlock.FontWeightProperty,
                    new Binding {Path = new PropertyPath("FontWeight"), Source = this});
                ColorRangeControl.SetBinding(TextBlock.ForegroundProperty,
                    new Binding {Path = new PropertyPath("Foreground"), Source = this});
                ColorRangeControl.SetBinding(VisibilityProperty,
                    new Binding {Path = new PropertyPath("Visibility"), Source = this});

                if (ColorRangeControl.Parent == null)
                {
                    Model.Chart.View.AddToView(ColorRangeControl);
                }
                var max = ColorRangeControl.SetMax(ActualValues.GetTracker(this).WLimit.Max.ToString(CultureInfo.InvariantCulture));
                var min = ColorRangeControl.SetMin(ActualValues.GetTracker(this).WLimit.Min.ToString(CultureInfo.InvariantCulture));

                var m = max > min ? max : min;

                ColorRangeControl.Width = m;

                Model.Chart.ControlSize = new CoreSize(Model.Chart.ControlSize.Width - m - 4,
                    Model.Chart.ControlSize.Height);
            }
            else
            {
                Model.Chart.View.RemoveFromView(ColorRangeControl);
            }
        }

        /// <summary>
        /// Places specializes items
        /// </summary>
        public override void PlaceSpecializedElements()
        {
            if (DrawsHeatRange)
            {
                ColorRangeControl.UpdateFill(GradientStopCollection);

                ColorRangeControl.Height = Model.Chart.DrawMargin.Height;

                Canvas.SetTop(ColorRangeControl, Model.Chart.DrawMargin.Top);
                Canvas.SetLeft(ColorRangeControl, Model.Chart.DrawMargin.Left + Model.Chart.DrawMargin.Width + 4);
            }
        }

        #endregion

        #region Private Methods

        private void InitializeDefuaults()
        {
            //this.SetIfNotSet(StrokeThicknessProperty, 0d);
            //this.SetIfNotSet(ForegroundProperty, new SolidColorBrush(Colors.White));
            //this.SetIfNotSet(StrokeProperty, new SolidColorBrush(Colors.White));
            this.SetIfNotSet(GradientStopCollectionProperty, new GradientStopCollection());

            Func<ChartPoint, string> defaultLabel = x => x.Weight.ToString(CultureInfo.InvariantCulture);
            this.SetIfNotSet(LabelPointProperty, defaultLabel);

            DefaultFillOpacity = 0.4;
        }

        /// <summary>
        /// Initializes the series colors if they are not set
        /// </summary>
        public override void InitializeColors()
        {
            var uwpfChart = (Chart)Model.Chart.View;
            var nextColor = uwpfChart.GetNextDefaultColor();

            if (Stroke == null)
                SetValue(StrokeProperty, new SolidColorBrush(nextColor));
            if (Fill == null)
                SetValue(FillProperty, new SolidColorBrush(nextColor));

            var defaultColdColor = new Color
            {
                A = (byte)(nextColor.A * (DefaultFillOpacity > 1
                    ? 1
                    : (DefaultFillOpacity < 0 ? 0 : DefaultFillOpacity))),
                R = nextColor.R,
                G = nextColor.G,
                B = nextColor.B
            };

            if (!GradientStopCollection.Any())
            {
                GradientStopCollection.Add(new GradientStop
                {
                    Color = defaultColdColor,
                    Offset = 0
                });
                GradientStopCollection.Add(new GradientStop
                {
                    Color = nextColor,
                    Offset = 1
                });
            }
        }

        #endregion
    }
}