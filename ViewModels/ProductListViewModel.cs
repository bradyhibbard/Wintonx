using Winton.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Winton.Views
{
    public class ProductListViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Product> Products { get; set; }

        public ProductListViewModel()
        {
            Products = new ObservableCollection<Product>();
            LoadProducts(); // Load initial products from the database or source
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private async void LoadProducts()
        {

        }

    }
}
