using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ManutMap.Controls
{
    public partial class LineChartControl : UserControl, INotifyPropertyChanged
    {
        public LineChartControl()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += (_, _) => { UpdateChartWidth(); DrawChart(); };
            SizeChanged += (_, _) => { UpdateChartWidth(); DrawChart(); };
        }

        private IEnumerable? _items;
        public IEnumerable? Items
        {
            get => _items;
            set
            {
                _items = value;
                OnPropertyChanged(nameof(Items));
                UpdateMax();
                UpdateLabels();
                UpdateChartWidth();
                DrawChart();
            }
        }

        private double _maxValue;
        public double MaxValue
        {
            get => _maxValue;
            private set
            {
                _maxValue = value;
                OnPropertyChanged(nameof(MaxValue));
                this.Tag = _maxValue;
            }
        }

        private List<string> _labels = new();
        public List<string> Labels
        {
            get => _labels;
            private set
            {
                _labels = value;
                OnPropertyChanged(nameof(Labels));
            }
        }

        private double _chartWidth;
        public double ChartWidth
        {
            get => _chartWidth;
            set
            {
                _chartWidth = value;
                OnPropertyChanged(nameof(ChartWidth));
            }
        }

        private void UpdateLabels()
        {
            var list = new List<string>();
            if (_items != null)
            {
                foreach (var item in _items)
                {
                    if (item == null) continue;
                    var type = item.GetType();
                    var prop = type.GetProperty("Label") ?? type.GetProperty("Route");
                    list.Add(prop?.GetValue(item)?.ToString() ?? string.Empty);
                }
            }
            Labels = list;
        }

        private void UpdateMax()
        {
            if (_items == null) { MaxValue = 0; return; }
            double max = 0;
            foreach (var item in _items)
            {
                if (item == null) continue;
                var type = item.GetType();
                var prop = type.GetProperty("Value") ?? type.GetProperty("Count");
                if (prop != null && double.TryParse(prop.GetValue(item)?.ToString(), out double v) && v > max)
                    max = v;
            }
            MaxValue = max;
        }

        private void UpdateChartWidth()
        {
            int count = _items?.Cast<object>().Count() ?? 0;
            double minWidth = ActualWidth > 0 ? ActualWidth : 200;
            double width = count > 0 ? System.Math.Max(minWidth, count * 40) : minWidth;
            ChartWidth = width;
        }

        private void DrawChart()
        {
            ChartCanvas.Children.Clear();
            if (_items == null || MaxValue <= 0) return;

            var list = _items.Cast<object>().ToList();
            int n = list.Count;
            if (n == 0) return;

            double width = ChartCanvas.ActualWidth;
            double height = ChartCanvas.ActualHeight;
            if (width == 0 || height == 0) return;

            // draw grid lines for better readability
            int grids = 4;
            for (int i = 0; i <= grids; i++)
            {
                double y = height - i * height / grids;
                var line = new Line
                {
                    X1 = 0,
                    X2 = width,
                    Y1 = y,
                    Y2 = y,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 0.5,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };
                ChartCanvas.Children.Add(line);

                var label = new TextBlock
                {
                    Text = ((int)(MaxValue * i / grids)).ToString(),
                    FontSize = 10,
                    Foreground = Brushes.Gray
                };
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, y - 10);
                ChartCanvas.Children.Add(label);
            }

            // vertical grid lines when not too many points
            if (n <= 12)
            {
                for (int i = 0; i < n; i++)
                {
                    double x = n == 1 ? width / 2 : i * width / (n - 1);
                    var vline = new Line
                    {
                        X1 = x,
                        X2 = x,
                        Y1 = 0,
                        Y2 = height,
                        Stroke = Brushes.LightGray,
                        StrokeThickness = 0.5,
                        StrokeDashArray = new DoubleCollection { 2, 2 }
                    };
                    ChartCanvas.Children.Add(vline);
                }
            }

            // axis lines
            var axisBrush = Brushes.Gray;
            ChartCanvas.Children.Add(new Line { X1 = 0, Y1 = height, X2 = width, Y2 = height, Stroke = axisBrush, StrokeThickness = 1 });
            ChartCanvas.Children.Add(new Line { X1 = 0, Y1 = 0, X2 = 0, Y2 = height, Stroke = axisBrush, StrokeThickness = 1 });

            Color brandColor = Color.FromRgb(0x00, 0x78, 0xD4);
            var lineBrush = new SolidColorBrush(brandColor);
            var fillBrush = new SolidColorBrush(Color.FromArgb(60, brandColor.R, brandColor.G, brandColor.B));

            var points = new List<System.Windows.Point>();

            bool showLabels = n <= 20;
            for (int i = 0; i < n; i++)
            {
                var item = list[i];
                var prop = item.GetType().GetProperty("Value") ?? item.GetType().GetProperty("Count");
                double val = 0;
                if (prop != null)
                    double.TryParse(prop.GetValue(item)?.ToString(), out val);

                double x = n == 1 ? width / 2 : i * width / (n - 1);
                double y = height - (val / MaxValue * height);
                points.Add(new System.Windows.Point(x, y));

                var ell = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = lineBrush,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(ell, x - 4);
                Canvas.SetTop(ell, y - 4);
                ChartCanvas.Children.Add(ell);

                if (showLabels)
                {
                    var valueLabel = new TextBlock
                    {
                        Text = val.ToString(),
                        FontSize = 11,
                        Foreground = Brushes.Black
                    };
                    Canvas.SetLeft(valueLabel, x - 10);
                    Canvas.SetTop(valueLabel, y - 20);
                    ChartCanvas.Children.Add(valueLabel);
                }
            }

            var areaGeometry = new StreamGeometry();
            using (var ctx = areaGeometry.Open())
            {
                ctx.BeginFigure(new System.Windows.Point(0, height), true, true);
                foreach (var p in points)
                    ctx.LineTo(p, true, false);
                ctx.LineTo(new System.Windows.Point(width, height), true, false);
            }
            areaGeometry.Freeze();
            var areaPath = new Path { Data = areaGeometry, Fill = fillBrush };
            ChartCanvas.Children.Add(areaPath);

            var poly = new Polyline
            {
                Stroke = lineBrush,
                StrokeThickness = 2,
                Points = new PointCollection(points)
            };
            ChartCanvas.Children.Add(poly);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
