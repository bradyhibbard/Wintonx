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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (Parent is Grid parentGrid)
                parentGrid.Visibility = Visibility.Collapsed;
        }

    }
}
