using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Winton.Views
{
    public class FilterChangedEventArgs : EventArgs
    {
        public Dictionary<string, HashSet<string>> ActiveFilters { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public FilterPanel.MatchMode MatchMode { get; set; }



    }

    public partial class FilterPanel : UserControl
    {
        // Existing individual events
        public event Action<string, string> FilterApplied;
        public event Action<string, string> FilterRemoved;
        public event Action AllFiltersCleared;
        private List<string> _allProducts = new();

        public enum MatchMode { MatchAny, MatchAll }
        public MatchMode CurrentMatchMode { get; private set; } = MatchMode.MatchAny;
        public event Action<MatchMode> MatchModeChanged;

        // ✅ New event for LiveSalesFloor to subscribe to
        public event EventHandler<FilterChangedEventArgs> FiltersChanged;

        private readonly Dictionary<string, HashSet<string>> _activeFilters = new();

        public FilterPanel()
        {
            InitializeComponent();
        }

        // PUBLIC METHODS TO LOAD OPTIONS
        public void LoadVendors(List<string> vendors) => VendorComboBox.ItemsSource = vendors;
        public void LoadCategories(List<string> categories) => CategoryComboBox.ItemsSource = categories;
        public void LoadGroups(List<string> groups) => GroupComboBox.ItemsSource = groups;
        public void LoadProducts(List<string> products)
        {
            _allProducts = products;
        }


        // SELECTION HANDLERS
        private void VendorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AddFilter("Vendor", VendorComboBox.SelectedItem as string);
            VendorComboBox.SelectedItem = null;
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AddFilter("Category", CategoryComboBox.SelectedItem as string);
            CategoryComboBox.SelectedItem = null;
        }

        private void GroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AddFilter("Group", GroupComboBox.SelectedItem as string);
            GroupComboBox.SelectedItem = null;
        }

        private void ProductSuggestionsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ProductSuggestionsListBox.SelectedItem is string selectedProduct)
            {
                AddFilter("Product", selectedProduct);
                ProductSearchBox.Text = "";
                ProductSuggestionsListBox.Visibility = Visibility.Collapsed;
            }
        }

        private void ProductSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string input = ProductSearchBox.Text.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(input))
            {
                ProductSuggestionsListBox.Visibility = Visibility.Collapsed;
                ProductSuggestionsListBox.ItemsSource = null;
                return;
            }

            var filtered = _allProducts
                .Where(p => p.ToLower().StartsWith(input))
                .Take(10) // limit results
                .ToList();

            if (filtered.Any())
            {
                ProductSuggestionsListBox.ItemsSource = filtered;
                ProductSuggestionsListBox.Visibility = Visibility.Visible;
            }
            else
            {
                ProductSuggestionsListBox.Visibility = Visibility.Collapsed;
            }
        }




        private void StartDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StartDatePicker.SelectedDate.HasValue)
                AddFilter("Start Date", StartDatePicker.SelectedDate.Value.ToShortDateString());
        }

        private void EndDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EndDatePicker.SelectedDate.HasValue)
                AddFilter("End Date", EndDatePicker.SelectedDate.Value.ToShortDateString());
        }

        private void MatchModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (MatchAnyRadioButton.IsChecked == true)
                CurrentMatchMode = MatchMode.MatchAny;
            else
                CurrentMatchMode = MatchMode.MatchAll;

            MatchModeChanged?.Invoke(CurrentMatchMode);
            RaiseFiltersChanged(); // ✅ Notify subscribers of change
        }

        // CORE FILTER LOGIC
        private void AddFilter(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (!_activeFilters.ContainsKey(key))
                _activeFilters[key] = new HashSet<string>();

            if (_activeFilters[key].Contains(value))
                return;

            _activeFilters[key].Add(value);
            FilterApplied?.Invoke(key, value);
            RefreshFilterTags();
            RaiseFiltersChanged(); // ✅ Notify subscribers
        }

        private void RemoveFilter(string key, string value)
        {
            if (_activeFilters.TryGetValue(key, out var values))
            {
                values.Remove(value);
                if (values.Count == 0)
                    _activeFilters.Remove(key);

                FilterRemoved?.Invoke(key, value);
                RefreshFilterTags();
                RaiseFiltersChanged(); // ✅ Notify subscribers
            }
        }

        private void RefreshFilterTags()
        {
            ActiveFiltersPanel.Children.Clear();

            foreach (var kvp in _activeFilters)
            {
                foreach (var val in kvp.Value)
                {
                    var btn = new Button
                    {
                        Content = $"{kvp.Key}: {val} ✕",
                        Margin = new Thickness(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        Tag = (kvp.Key, val),
                        Style = (Style)FindResource("TagButtonStyle")
                    };

                    btn.Click += IndividualFilter_Remove_Click;
                    ActiveFiltersPanel.Children.Add(btn);
                }
            }
        }

        private void IndividualFilter_Remove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ValueTuple<string, string> tag)
            {
                RemoveFilter(tag.Item1, tag.Item2);

                if (tag.Item1 == "Start Date") StartDatePicker.SelectedDate = null;
                if (tag.Item1 == "End Date") EndDatePicker.SelectedDate = null;
            }
        }

        private void ClearAllFilters_Click(object sender, RoutedEventArgs e)
        {
            _activeFilters.Clear();

            VendorComboBox.SelectedItem = null;
            CategoryComboBox.SelectedItem = null;
            GroupComboBox.SelectedItem = null;
            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;

            ActiveFiltersPanel.Children.Clear();
            AllFiltersCleared?.Invoke();
            RaiseFiltersChanged(); // ✅ Notify subscribers
        }

        public void ClearFilters() => ClearAllFilters_Click(null, null);

        // ✅ Helper to raise full filter state update
        private void RaiseFiltersChanged()
        {
            FiltersChanged?.Invoke(this, new FilterChangedEventArgs
            {
                ActiveFilters = new Dictionary<string, HashSet<string>>(_activeFilters),
                StartDate = StartDatePicker.SelectedDate,
                EndDate = EndDatePicker.SelectedDate,
                MatchMode = CurrentMatchMode
            });
        }
    }
}
