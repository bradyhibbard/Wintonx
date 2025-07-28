using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using Winton.Models;
using Winton.Services;

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
            _ = LoadProductsAsync(); // Fire-and-forget async initialization
        }

        /// <summary>
        /// Asynchronously loads products from the database and populates the ListView.
        /// </summary>
        private async Task LoadProductsAsync()
        {
            try
            {
                List<Product> products = await ProductService.GetProductsAsync();
                ProductListView.ItemsSource = products;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading products: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Gets the selected product's item number.
        /// </summary>
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

        /// <summary>
        /// Handles the Import Product List button click event.
        /// Opens a file dialog to select an Excel file and imports product data.
        /// </summary>
        private async void ImportProductList_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                DefaultExt = ".xlsx",
                Filter = "Excel Files|*.xls;*.xlsx;*.xlsm|CSV Files|*.csv"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    await ImportServices.ImportProductListAsync(openFileDialog.FileName);
                    MessageBox.Show("Import successful.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadProductsAsync(); // Refresh the product list
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error importing product list: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Handles the Delete Product List button click event.
        /// Deletes all products from the database and refreshes the list.
        /// </summary>
        private async void DeleteProductList_Click(object sender, RoutedEventArgs e)
        {
            var confirmation = MessageBox.Show(
                "Are you sure you want to delete all products?",
                "Confirm Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (confirmation == MessageBoxResult.Yes)
            {
                try
                {
                    bool success = await ImportServices.DeleteAllProductsAsync();

                    if (success)
                    {
                        MessageBox.Show("All products have been successfully deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadProductsAsync(); // Refresh the product list
                    }
                    else
                    {
                        MessageBox.Show("An error occurred while deleting products.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting products: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
