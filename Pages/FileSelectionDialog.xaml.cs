using System.Windows;
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;

namespace FrekingCompareAnalysis.Pages
{
    public partial class FileSelectionDialog : Window
    {
        private readonly List<string> _availableFiles;
        private readonly List<CheckBox> _fileCheckBoxes;

        public List<string> SelectedFiles { get; private set; }

        public FileSelectionDialog(List<string> availableFiles)
        {
            InitializeComponent();
            _availableFiles = availableFiles ?? new List<string>();
            _fileCheckBoxes = new List<CheckBox>();
            SelectedFiles = new List<string>();

            InitializeFileCheckBoxes();
            UpdateSelectionCount();
        }

        private void InitializeFileCheckBoxes()
        {
            FileCheckBoxPanel.Children.Clear();
            _fileCheckBoxes.Clear();

            foreach (var fileName in _availableFiles)
            {
                var checkBox = new CheckBox
                {
                    Content = fileName,
                    Margin = new Thickness(5, 3, 5, 3),
                    Tag = fileName
                };

                checkBox.Checked += CheckBox_CheckedChanged;
                checkBox.Unchecked += CheckBox_CheckedChanged;

                _fileCheckBoxes.Add(checkBox);
                FileCheckBoxPanel.Children.Add(checkBox);
            }
        }

        private void CheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateSelectionCount();
        }

        private void UpdateSelectionCount()
        {
            int selectedCount = _fileCheckBoxes.Count(cb => cb.IsChecked == true);
            TxtSelectionCount.Text = $"{selectedCount} файл{(selectedCount != 1 ? "ів" : "")} вибрано";

            BtnOK.IsEnabled = selectedCount > 0;
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var checkBox in _fileCheckBoxes)
            {
                checkBox.IsChecked = true;
            }
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var checkBox in _fileCheckBoxes)
            {
                checkBox.IsChecked = false;
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            SelectedFiles = _fileCheckBoxes
                .Where(cb => cb.IsChecked == true)
                .Select(cb => cb.Tag.ToString())
                .ToList();

            if (SelectedFiles.Count == 0)
            {
                MessageBox.Show("Будь ласка, виберіть хоч один файл.",
                              "Файли не вибрано", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}