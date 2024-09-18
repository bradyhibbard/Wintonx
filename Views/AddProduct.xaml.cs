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
    /// Interaction logic for AddProduct.xaml
    /// </summary>
    public partial class AddProduct : Window
    {
        public AddProduct(string sectionID)
        {
            InitializeComponent();
            txtSectionID.Text = sectionID;
            LoadProductItemNumbers();
        }

        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            string itemNumber = cmbItemNumber.Text;
            string sectionId = txtSectionID.Text;

            await DatabaseHelper.EnsureSectionExistsAsync(sectionId);

            if (await DatabaseHelper.ProductExistsAsync(itemNumber))
            {
                var currentPlacement = await DatabaseHelper.GetCurrentProductPlacementAsync(itemNumber);

                if (currentPlacement != null)
                {
                    if (currentPlacement.SectionID == sectionId)
                    {
                        MessageBox.Show($"Product {itemNumber} is already placed in section {sectionId}.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        var result = MessageBox.Show($"Product {itemNumber} is already placed in section {currentPlacement.SectionID}. Do you want to move it to section {sectionId}?", "Move Product", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            await MoveProduct(itemNumber, sectionId, currentPlacement);
                            MessageBox.Show($"Product {itemNumber} has been moved to section {sectionId}.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            this.DialogResult = true; // Set DialogResult to true when a product is moved
                        }
                        else
                        {
                            MessageBox.Show("Operation cancelled.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                else
                {
                    await PlaceProduct(itemNumber, sectionId);
                    this.DialogResult = true; // Set DialogResult to true when a product is placed
                }
            }
            else
            {
                MessageBox.Show($"Product with item number {itemNumber} does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }


        private async Task MoveProduct(string itemNumber, string newSectionID, ProductPlacement currentPlacement)
        {
            // Archive the current placement
            await DatabaseHelper.ArchiveProductPlacement(currentPlacement.ProductID, currentPlacement.SectionID, "Moved to another section", currentPlacement.QuantitySold, currentPlacement.Revenue);

            // Remove from current section
            await DatabaseHelper.RemoveProductFromSection(currentPlacement.ProductID, currentPlacement.SectionID, DateTime.Now);

            // Add to new section
            await DatabaseHelper.PlaceProductAsync(itemNumber, newSectionID);
        }

        private async Task PlaceProduct(string itemNumber, string sectionID)
        {
            await DatabaseHelper.PlaceProductAsync(itemNumber, sectionID);
            MessageBox.Show($"Product {itemNumber} has been placed in section {sectionID}.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void LoadProductItemNumbers()
        {
            var itemNumbers = DatabaseHelper.GetProductItemNumbers(); // This method should query your database
            cmbItemNumber.ItemsSource = itemNumbers;
        }
    }
}
