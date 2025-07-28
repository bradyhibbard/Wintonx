using System.Windows;
using Winton.Models;
using Winton.Services;

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
            _ = LoadProductItemNumbersAsync();
        }

        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            string itemNumber = cmbItemNumber.Text;
            string sectionId = txtSectionID.Text;

            await ProductPlacementServices.EnsureSectionExistsAsync(sectionId);

            if (await ProductPlacementServices.ProductExistsAsync(itemNumber))
            {
                var currentPlacement = await ProductPlacementServices.GetCurrentProductPlacementAsync(itemNumber);

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
            await ProductPlacementServices.ArchiveProductPlacementAsync(currentPlacement.ProductID, currentPlacement.SectionID, "Moved to another section", currentPlacement.QuantitySold, currentPlacement.Revenue);

            // Remove from current section
            await ProductPlacementServices.RemoveProductFromSectionAsync(currentPlacement.ProductID, currentPlacement.SectionID, DateTime.Now);

            // Add to new section
            await ProductPlacementServices.PlaceProductAsync(itemNumber, newSectionID);
        }

        private async Task PlaceProduct(string itemNumber, string sectionID)
        {
            await ProductPlacementServices.PlaceProductAsync(itemNumber, sectionID);
            MessageBox.Show($"Product {itemNumber} has been placed in section {sectionID}.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async Task LoadProductItemNumbersAsync()
        {
            var itemNumbers = await ProductService.GetProductItemNumbersAsync(); // Await the async call
            cmbItemNumber.ItemsSource = itemNumbers;
        }

    }
}
