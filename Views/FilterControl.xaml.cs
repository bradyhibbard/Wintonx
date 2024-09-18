using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Winton.Views
{
    public partial class FilterControl : UserControl
    {
        public event EventHandler<List<string>> SaveClicked;
        public event EventHandler CancelClicked;
        public event EventHandler ClearClicked;
        public event EventHandler RevenueFilterChanged;

        private readonly List<string> _tags = new List<string>();

        public FilterControl()
        {
            InitializeComponent();
            SetDefaultDateRange();
        }

        private void SetDefaultDateRange()
        {
            var now = DateTime.Now;
            StartDatePicker.SelectedDate = new DateTime(now.Year, now.Month, 1);
            EndDatePicker.SelectedDate = now;
        }

        private void RevenueCheckbox_Checked(object sender, RoutedEventArgs e) => ApplyRevenueFilter();

        private void RevenueCheckbox_Unchecked(object sender, RoutedEventArgs e) => ApplyRevenueFilter();

        private void ApplyRevenueFilter()
        {
            if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue)
            {
                var filterData = new RevenueFilterData
                {
                    IsRevenueChecked = RevenueCheckbox.IsChecked ?? false,
                    StartDate = StartDatePicker.SelectedDate.Value,
                    EndDate = EndDatePicker.SelectedDate.Value
                };
                RevenueFilterChanged?.Invoke(this, filterData);
            }
        }

        private void AddTag_Click(object sender, RoutedEventArgs e)
        {
            var tag = TagInput.Text.Trim();
            if (!string.IsNullOrEmpty(tag) && !_tags.Contains(tag))
            {
                _tags.Add(tag);
                TagList.Items.Add(new TextBlock { Text = tag, Margin = new Thickness(5), Foreground = Brushes.Black });
                TagInput.Clear();
            }
        }
        private void SaveFilters_Click(object sender, RoutedEventArgs e)
        {
            var selectedFilters = new List<string>();

            foreach (var expander in FilterStackPanel.Children.OfType<Expander>())
            {
                foreach (var child in (expander.Content as StackPanel).Children)
                {
                    if (child is CheckBox checkbox && checkbox.IsChecked == true)
                    {
                        selectedFilters.Add(checkbox.Content.ToString());
                    }
                }
            }

            // Add date range to selected filters
            if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue)
            {
                selectedFilters.Add($"StartDate:{StartDatePicker.SelectedDate.Value}");
                selectedFilters.Add($"EndDate:{EndDatePicker.SelectedDate.Value}");
            }

            // Raise the SaveClicked event to notify SalesFloorControl
            SaveClicked?.Invoke(this, selectedFilters.Concat(_tags).ToList());

            // Hide the filter panel after saving
            Visibility = Visibility.Collapsed;
        }

        private void CancelFilters_Click(object sender, RoutedEventArgs e)
        {
            ClearAllFilters();
            CancelClicked?.Invoke(this, EventArgs.Empty);

            // Hide the filter panel after canceling
            Visibility = Visibility.Collapsed;
        }


        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            ClearAllFilters();
            ClearClicked?.Invoke(this, EventArgs.Empty);
        }

        private void ClearAllFilters()
        {
            foreach (var expander in FilterStackPanel.Children.OfType<Expander>())
            {
                foreach (var checkbox in (expander.Content as StackPanel)?.Children.OfType<CheckBox>() ?? Enumerable.Empty<CheckBox>())
                {
                    checkbox.IsChecked = false;
                }
            }

            _tags.Clear();
            TagList.Items.Clear();
            SetDefaultDateRange();
        }

        private void Category_Checked(object sender, RoutedEventArgs e) => UpdateCategoryGroups((CheckBox)sender, true);

        private void Category_Unchecked(object sender, RoutedEventArgs e) => UpdateCategoryGroups((CheckBox)sender, false);

        private void UpdateCategoryGroups(CheckBox checkbox, bool isChecked)
        {
            var category = checkbox.Content.ToString();
            var groupStackPanel = FindName($"{category}Groups") as StackPanel;
            if (groupStackPanel == null) return;

            if (isChecked)
            {
                PopulateGroupStackPanel(category, groupStackPanel);
            }
            else
            {
                groupStackPanel.Children.Clear();
            }
        }

        private void PopulateGroupStackPanel(string category, StackPanel groupStackPanel)
        {
            var groups = GetGroupsByCategory(category);
            groupStackPanel.Children.Clear();

            var selectAllCheckBox = new CheckBox
            {
                Content = "Select All",
                Margin = new Thickness(5),
                Foreground = Brushes.Black
            };
            selectAllCheckBox.Checked += (s, args) => SelectAllGroups(groupStackPanel, true);
            selectAllCheckBox.Unchecked += (s, args) => SelectAllGroups(groupStackPanel, false);
            groupStackPanel.Children.Add(selectAllCheckBox);

            foreach (var group in groups)
            {
                var groupCheckbox = new CheckBox
                {
                    Content = group.Key,
                    Margin = new Thickness(5),
                    Foreground = Brushes.Black,
                    Tag = group.Value
                };
                groupStackPanel.Children.Add(groupCheckbox);
            }
        }

        private void SelectAllGroups(StackPanel groupStackPanel, bool isChecked)
        {
            foreach (var child in groupStackPanel.Children.OfType<CheckBox>().Skip(1)) // Skip the Select All checkbox
            {
                child.IsChecked = isChecked;
            }
        }

        private Dictionary<string, string> GetGroupsByCategory(string category)
        {
            var categoryGroups = new Dictionary<string, Dictionary<string, string>>()
            {
                {
                    "Upholstery", new Dictionary<string, string>
                    {
                        { "Stationary Sectional Leather", "STSELE" },
                        { "Stationary Sectional Fabric", "STSEFA" },
                        { "Stationary Fabric", "STAFAB" },
                        { "Stationary Leather", "STALEA" },
                        { "Sleepers", "SLEEPR" },
                        { "Motion Sectional Leather", "MOSELE" },
                        { "Motion Sectional Fabric", "MOSEFA" },
                        { "Motion Leather", "MOLEA" },
                        { "Motion Fabric", "MOFAB" },
                        { "Recliners Fabric", "RECFAB" },
                        { "Recliners Leather", "RECLEA" },
                        { "Ottomans", "OTTO" },
                        { "Accent Chairs", "ACTCHR" }
                    }
                },
                {
                    "Patio", new Dictionary<string, string>
                    {
                        { "Umbrellas", "PTUMB" },
                        { "Patio Fireplace", "PTFIR" },
                        { "Patio Occasional", "PTOCCA" },
                        { "Patio Lounge", "PTLNG" },
                        { "Patio Dining", "PTDIN" }
                    }
                },
                {
                    "Office", new Dictionary<string, string>
                    {
                        { "Office Chairs", "OFCCHR" },
                        { "File Cabinets", "FILE" },
                        { "Desks", "DESK" },
                        { "Credenzas/Hutches", "CRDNZA" },
                        { "Bookcases", "BKCS" },
                        { "Curio Cabinets", "CURIO" }
                    }
                },
                {
                    "Occasional Tables", new Dictionary<string, string>
                    {
                        { "Sofa Tables", "SFATBL" },
                        { "Shelving/Room Dividers", "SHLVING" },
                        { "End Tables", "ENDTBL" },
                        { "Chairs Side Tables", "CHRSID" },
                        { "3 Piece Occasional Set", "3PAC" },
                        { "Cocktail Tables", "COCTBL" }
                    }
                },
                {
                    "Mattresses", new Dictionary<string, string>
                    {
                        { "Twin Mattress", "TWN" },
                        { "TXL Mattress", "SK" },
                        { "Full Mattress", "FULL" },
                        { "Queen Mattress", "QUEEN" },
                        { "King Mattress", "KING" },
                        { "Cal King Mattress", "CAL" },
                        { "Sheets", "SHEETS" },
                        { "Protectors", "PRTCTR" },
                        { "Pillows", "PILOWS" },
                        { "Bed Frames", "FRAMES" },
                        { "Adjustable Bases", "ADJBSE" },
                        { "Boxsprings", "BOX" }
                    }
                },
                {
                    "Entertainment", new Dictionary<string, string>
                    {
                        { "Fire Places", "FRPLC" },
                        { "Speakers", "SPEK" },
                        { "TV Consoles", "TVCON" }
                    }
                },
                {
                    "Bedroom", new Dictionary<string, string>
                    {
                        { "Bunk Beds", "BNKBD" },
                        { "Cal King Bed Frames", "CALB" },
                        { "Chests", "CHEST" },
                        { "Dressers", "DRSR" },
                        { "Full Beds", "FB" },
                        { "Twin Beds", "TB" },
                        { "Queen Beds", "QB" },
                        { "King Beds", "KB" },
                        { "Mirrors", "MIROR" },
                        { "Nightstands", "NGTST" },
                        { "Bedroom Bench", "BNCH" }
                    }
                },
                {
                    "Dining", new Dictionary<string, string>
                    {
                        { "Arm Chairs", "ARMCHR" },
                        { "Sid Chairs", "SIDCHR" },
                        { "Barstools", "BARSTL" },
                        { "Benches", "DINBCH" },
                        { "Buffet/Sideboards", "BUFFET" },
                        { "Dining Tables", "TABLES" }
                    }
                },
                {
                    "Accessories", new Dictionary<string, string>
                    {
                        { "Accessories", "ACCES" }
                    }
                }
            };

            return categoryGroups.ContainsKey(category) ? categoryGroups[category] : new Dictionary<string, string>();
        }
    }
    public class RevenueFilterData : EventArgs
    {
        public bool IsRevenueChecked { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}

