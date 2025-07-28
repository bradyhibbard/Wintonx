using System.Windows;

namespace Winton.Views
{
    /// <summary>
    /// Interaction logic for FileNameWindow.xaml
    /// </summary>
    public partial class FileNameWindow : Window
    {
        public string FileName { get; set; }
        public DateTime ReportDate { get; set; }
        public FileNameWindow(string initialFileName, DateTime initialDate)
        {
            InitializeComponent();
            txtFileName.Text = initialFileName;
            datePicker.SelectedDate = initialDate;
        }


        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            FileName = txtFileName.Text;
            ReportDate = datePicker.SelectedDate ?? DateTime.Now;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
