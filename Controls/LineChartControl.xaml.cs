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
            Loaded += (_, _) => DrawChart();
            SizeChanged += (_, _) => DrawChart();
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

            var poly = new Polyline
            {
                Stroke = Brushes.SteelBlue,
                StrokeThickness = 2
            };

            for (int i = 0; i < n; i++)
            {
                var item = list[i];
                var prop = item.GetType().GetProperty("Value") ?? item.GetType().GetProperty("Count");
                double val = 0;
                if (prop != null)
                    double.TryParse(prop.GetValue(item)?.ToString(), out val);

                double x = n == 1 ? width / 2 : i * width / (n - 1);
                double y = height - (val / MaxValue * height);
                poly.Points.Add(new System.Windows.Point(x, y));

                var ell = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = Brushes.SteelBlue,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(ell, x - 4);
                Canvas.SetTop(ell, y - 4);
                ChartCanvas.Children.Add(ell);
            }

            ChartCanvas.Children.Insert(0, poly);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
