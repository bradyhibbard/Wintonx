using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }
}
