using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace ManutMap
{
    public partial class OsDetailWindow : Window
    {
        public OsDetailWindow(JObject data)
        {
            InitializeComponent();

            if (data.TryGetValue("DESCADICIONALEXEC", out var desc))
            {
                DescExecText.Text = $"DESCADICIONALEXEC: {desc}";
            }
            else
            {
                DescExecText.Text = "DESCADICIONALEXEC: -";
            }

            var items = data.Properties()
                .Select(p => new KeyValuePair<string, string>(p.Name, p.Value.ToString()))
                .ToList();
            DetailsGrid.ItemsSource = items;
        }
    }
}
