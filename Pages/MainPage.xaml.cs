using FrekingCompareAnalysis.Models;
using FrekingCompareAnalysis.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using MessageBox = System.Windows.MessageBox;

namespace FrekingCompareAnalysis.Pages
{
    /// <summary>
    /// Interaction logic for MainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        private Dictionary<string, List<WellDataRecord>> _loadedData = new();
        private readonly WellDataExtractService _wellDataExtractService = new();

        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnLoadClicked(object sender, RoutedEventArgs e) {
            _loadedData.Clear();
            var openFileDialog = new Microsoft.Win32.OpenFileDialog {
                Filter = "Text files (*.txt)|*.txt",
                Multiselect = false
            };
            if (openFileDialog.ShowDialog() == true)
            {
                var file = openFileDialog.FileName;
                var data = await _wellDataExtractService.ReadWellDataFileAsync(file);
                _loadedData[System.IO.Path.GetFileNameWithoutExtension(file)] = data;
                MessageBox.Show("Файл завантажено.");
            }
            else
            {
                using var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _loadedData = await _wellDataExtractService.ReadMultipleFilesAsync(folderDialog.SelectedPath);
                    MessageBox.Show("Файли з папки завантажено.");
                }
            }
        }

        private void OnViewClicked(object sender, RoutedEventArgs e)
        {
            if (_loadedData.Count == 0)
            {
                MessageBox.Show("Спочатку завантажте дані!");
                return;
            }
            this.NavigationService.Navigate(new DataViewPage(_loadedData));
        }

        private void OnViewGraphClicked(object sender, RoutedEventArgs e)
        {
            if (_loadedData.Count == 0)
            {
                MessageBox.Show("Спочатку завантажте дані!");
                return;
            }
            this.NavigationService.Navigate(new GraphPage(_loadedData));
        }

        private void OnCompareClicked(object sender, RoutedEventArgs e)
        {
            if (_loadedData.Count == 0)
            {
                MessageBox.Show("Спочатку завантажте дані!");
                return;
            }
            this.NavigationService.Navigate(new ComparePage(_loadedData));
        }
    }
}
