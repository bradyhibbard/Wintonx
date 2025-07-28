using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Winton.Models;
using Winton.Services;

namespace Winton.Views
{
    public partial class SectionArchiveWindow : Window
    {
        private string _sectionId;

        public SectionArchiveWindow(string sectionId)
        {
            InitializeComponent();
            _sectionId = sectionId;
            LoadArchiveData();
        }

        private async void LoadArchiveData()
        {
            try
            {
                var archivedProducts = await ProductPlacementServices.GetArchivedProductsBySectionAsync(_sectionId);

                ArchiveListBox.Items.Clear();

                foreach (var product in archivedProducts)
                {
                    // Add the actual object, not the string representation
                    ArchiveListBox.Items.Add(product);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading archive data: {ex.Message}");
            }
        }


        private void ArchiveListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = ArchiveListBox.SelectedItem;

            if (selectedItem != null)
            {
                Console.WriteLine($"Selected Item Type: {selectedItem.GetType()}");
            }

            if (selectedItem is ProductPlacement placement)
            {
                Console.WriteLine($"Selected Item: {placement.ItemNumber}");
                DateAddedPicker.SelectedDate = placement.DatePlaced;
                DateRemovedPicker.SelectedDate = placement.DateRemoved ?? (DateTime?)null;
            }
            else
            {
                DateAddedPicker.SelectedDate = null;
                DateRemovedPicker.SelectedDate = null;
            }
        }

        private async Task RefreshArchiveData(int placementId)
        {
            try
            {
                var archivedProducts = await ProductPlacementServices.GetArchivedProductsBySectionAsync(_sectionId);

                ArchiveListBox.Items.Clear();

                ProductPlacement updatedPlacement = null;

                foreach (var product in archivedProducts)
                {
                    ArchiveListBox.Items.Add(product);

                    // Identify the updated item to reselect it
                    if (product.PlacementID == placementId)
                    {
                        updatedPlacement = product;
                    }
                }

                // Re-select the updated item to keep focus on it
                if (updatedPlacement != null)
                {
                    ArchiveListBox.SelectedItem = updatedPlacement;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing archive data: {ex.Message}");
            }
        }



        private async void SaveDatesButton_Click(object sender, RoutedEventArgs e)
        {
            if (ArchiveListBox.SelectedItem is ProductPlacement placement)
            {
                DateTime? newDateAdded = DateAddedPicker.SelectedDate;
                DateTime? newDateRemoved = DateRemovedPicker.SelectedDate;

                if (newDateAdded.HasValue && newDateRemoved.HasValue && newDateRemoved < newDateAdded)
                {
                    MessageBox.Show("Date Removed cannot be earlier than Date Added.");
                    return;
                }

                try
                {
                    // Update the dates in the database
                    await ProductPlacementServices.UpdateArchiveDatesAsync(
                        placement.PlacementID,
                        newDateAdded,
                        newDateRemoved
                    );

                    MessageBox.Show("Dates updated successfully.");

                    // Refresh the UI to reflect the new dates
                    await RefreshArchiveData(placement.PlacementID);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating dates: {ex.Message}");
                }
            }
        }






        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
