using FrekingCompareAnalysis.Models;
using System.Windows.Controls;

namespace FrekingCompareAnalysis.Pages
{
    /// <summary>
    /// Interaction logic for DataViewPage.xaml
    /// </summary>
    public partial class DataViewPage : Page
    {
        private readonly Dictionary<string, List<WellDataRecord>> _loadedData;

        public DataViewPage(Dictionary<string, List<WellDataRecord>> loadedData)
        {
            InitializeComponent();
            _loadedData = loadedData;
            FileComboBox.ItemsSource = _loadedData.Keys;
        }

        private void FileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FileComboBox.SelectedItem is string selectedFile && _loadedData.ContainsKey(selectedFile))
            {
                WellDataGrid.ItemsSource = _loadedData[selectedFile];
            }
            else
            {
                WellDataGrid.ItemsSource = null;
            }
        }
    }
}
