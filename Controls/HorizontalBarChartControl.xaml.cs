using System.Collections;
using System.ComponentModel;
using System.Windows.Controls;

namespace ManutMap.Controls
{
    public partial class HorizontalBarChartControl : UserControl, INotifyPropertyChanged
    {
        public HorizontalBarChartControl()
        {
            InitializeComponent();
            DataContext = this;
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

        private void UpdateMax()
        {
            if (_items == null) { MaxValue = 0; return; }
            double max = 0;
            foreach (var item in _items)
            {
                if (item == null) continue;
                var type = item.GetType();
                var prop = type.GetProperty("Value") ?? type.GetProperty("Count");
                if (prop != null)
                {
                    var valObj = prop.GetValue(item);
                    if (double.TryParse(valObj?.ToString(), out double v) && v > max)
                        max = v;
                }
            }
            MaxValue = max;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
