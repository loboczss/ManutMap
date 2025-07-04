using System.Windows;

namespace ManutMap
{
    public partial class ShareDialog : Window
    {
        public enum ShareOption { None, Html, Csv }
        public ShareOption Selected { get; private set; }

        public ShareDialog()
        {
            InitializeComponent();
        }

        private void BtnHtml_Click(object sender, RoutedEventArgs e)
        {
            Selected = ShareOption.Html;
            DialogResult = true;
        }

        private void BtnCsv_Click(object sender, RoutedEventArgs e)
        {
            Selected = ShareOption.Csv;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Selected = ShareOption.None;
            DialogResult = false;
        }
    }
}
