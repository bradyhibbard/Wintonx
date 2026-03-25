using System.Collections.ObjectModel;
using System.ComponentModel;
using Winton.Models;

namespace Winton.Views
{
    public class ProductListViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Product> Products { get; set; }

        public ProductListViewModel()
        {
            Products = new ObservableCollection<Product>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }
}
