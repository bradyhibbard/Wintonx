using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Winton.Data;
using Winton.Models;

namespace Winton.Views
{
    /// <summary>
    /// Interaction logic for ProductListControl.xaml
    /// </summary>
    public partial class ProductListControl : UserControl
    {
        public ProductListControl()
        {
            InitializeComponent();
            LoadProducts();
        }

        private void LoadProducts()
        {
            List<Product> products = DatabaseHelper.GetProducts(); // Implement this method to fetch products
            ProductListView.ItemsSource = products;
        }

        public string SelectedProductItemNumber
        {
            get
            {
                if (ProductListView.SelectedItem is Product selectedProduct)
                {
                    return selectedProduct.ItemNumber;
                }
                return null;
            }
        }

        private void ImportProductList_Click(object sender, RoutedEventArgs e)
        {
            // You would typically use a file dialog to get the file path
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".xlsx";
            dlg.Filter = "Excel Files|*.xls;*.xlsx;*.xlsm|CSV Files|*.csv";

            if (dlg.ShowDialog() == true)
            {
                string filename = dlg.FileName;
                DatabaseHelper.ImportProductList(filename);
                MessageBox.Show("Import successful.");
            }
        }

        private void DeleteProductList_Click(object sender, RoutedEventArgs e)
        {
            DatabaseHelper.DeleteAllProducts();
        }
    }
}
