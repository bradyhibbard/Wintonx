using System.Windows;
using System.Windows.Controls;
using Winton.ViewModels;

namespace Winton.Views
{
    public partial class SectionDetail : UserControl
    {
        public SectionDetailViewModel ViewModel { get; }

        public SectionDetail(SectionDetailViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
        }

        public event EventHandler Closed;

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Closed?.Invoke(this, EventArgs.Empty);

            if (Parent is Panel panel)
            {
                panel.Children.Clear();
                panel.Visibility = Visibility.Collapsed;
                return;
            }

            Visibility = Visibility.Collapsed;
        }

    }
}
