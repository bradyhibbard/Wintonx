using NPOI.HPSF;
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
