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
using System.Windows.Shapes;
using Winton.Data;
using Winton.Models;

namespace Winton.Views
{
    /// <summary>
    /// Interaction logic for SectionDetails.xaml
    /// </summary>
    public partial class SectionDetails : Window
    {

        private string _sectionID;
        public SectionDetails(string sectionID)
        {
            InitializeComponent();
            _sectionID = sectionID;
            this.DataContext = this;
            LoadProducts(_sectionID);
        }

        public async void LoadProducts(string sectionID)
        {
            try
            {
                var products = await Task.Run(() => DatabaseHelper.GetProductsBySection(sectionID));
                Dispatcher.Invoke(() =>
                {
                    lstProducts.ItemsSource = products;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load products: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            var addProductWindow = new AddProduct(_sectionID);
            if (addProductWindow.ShowDialog() == true) // Check if the product was added successfully
            {
                LoadProducts(_sectionID); // Refresh the list
            }
        }

        private async void RemoveProduct_Click(object sender, RoutedEventArgs e)
        {
            if (lstProducts.SelectedItem != null)
            {
                var selectedProduct = lstProducts.SelectedItem as ProductPlacement;

                if (selectedProduct != null)
                {
                    // Example static values, replace with actual logic to obtain these values
                    int quantitySold = selectedProduct.QuantitySold; // Assuming this is stored in your ProductPlacement model
                    decimal revenue = selectedProduct.Revenue;   // Assuming this is stored in your ProductPlacement model
                    int ProductID = selectedProduct.ProductID;
                    string removalNotes = "Product sold out"; // This could be extended to include more dynamic notes or reasons

                    // Call the updated archiving function with the additional parameters
                    await DatabaseHelper.ArchiveProductPlacement(ProductID, selectedProduct.SectionID, removalNotes, quantitySold, revenue);

                    MessageBox.Show("Product has been removed and archived successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadProducts(_sectionID);  // Refresh the list to reflect the removal
                }
                else
                {
                    MessageBox.Show("Selected item is not a ProductPlacement.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a product to remove.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }


        private void ShowReport_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Feature Coming Soon");
        }


        private void lstProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Handle selection changed logic if needed
        }
    }
}
