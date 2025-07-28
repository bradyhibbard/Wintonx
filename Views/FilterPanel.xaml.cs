using System.Collections.Generic;
using System.Windows.Controls;

namespace Winton.Views
{
    public partial class FilterPanel : UserControl
    {

        public event Action<string> VendorSelected;
        public event Action<string, string> CategorySelected;
        public event Action<string, string, string> GroupSelected;

        public FilterPanel()
        {
            InitializeComponent();
        }


        private void VendorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VendorComboBox.SelectedItem is string selectedVendor)
            {
                VendorSelected?.Invoke(selectedVendor);
            }
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VendorComboBox.SelectedItem is string vendor &&
                CategoryComboBox.SelectedItem is string category)
            {
                CategorySelected?.Invoke(vendor, category);
            }
        }

        private void GroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VendorComboBox.SelectedItem is string vendor &&
                CategoryComboBox.SelectedItem is string category &&
                GroupComboBox.SelectedItem is string group)
            {
                GroupSelected?.Invoke(vendor, category, group);
            }
        }

        public void LoadVendors(List<string> vendors)
        {
            VendorComboBox.ItemsSource = vendors;
        }

        public void LoadCategories(List<string> categories)
        {
            CategoryComboBox.ItemsSource = categories;
        }

        public void LoadGroups(List<string> groups)
        {
            GroupComboBox.ItemsSource = groups;
        }

        public void ClearFilters()
        {
            VendorComboBox.SelectedItem = null;
            CategoryComboBox.SelectedItem = null;
            GroupComboBox.SelectedItem = null;
            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;
        }
    }
}
