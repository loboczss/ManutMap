using System.Windows;
using System.Windows.Input; // Necess�rio para MouseButtonEventArgs

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
            this.Close(); // Fecha a janela ap�s a sele��o
        }

        private void BtnCsv_Click(object sender, RoutedEventArgs e)
        {
            Selected = ShareOption.Csv;
            DialogResult = true;
            this.Close(); // Fecha a janela ap�s a sele��o
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Selected = ShareOption.None;
            DialogResult = false;
            this.Close();
        }

        // --- M�TODO ADICIONADO ---
        // Permite que a janela sem borda seja arrastada com o mouse
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
    }
}