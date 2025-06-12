using FrekingCompareAnalysis.Models;
using FrekingCompareAnalysis.Services;
using LiveCharts;
using LiveCharts.Wpf;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace FrekingCompareAnalysis.Pages
{
    public partial class GraphPage : Page
    {
        private readonly Dictionary<string, List<WellDataRecord>> _loadedData;
        private CancellationTokenSource _updateCts;
        private List<Color> _seriesColors = new();
        private BreakdownDetector _breakdownDetector; // Add breakdown detector

        // Максимальна кількість точок після ресемплінгу
        private const int MaxPoints = 200;

        public GraphPage(Dictionary<string, List<WellDataRecord>> loadedData)
        {
            InitializeComponent();
            _loadedData = loadedData;
            _updateCts = new CancellationTokenSource();
            _breakdownDetector = new BreakdownDetector(); // Initialize detector
            InitializeColors();
            LoadFiles();
            ParameterListBox.ItemsSource = AllParameters.All;

            this.Loaded += OnPageLoaded;
        }

        private void InitializeColors()
        {
            _seriesColors = new List<Color>
            {
                Color.FromRgb(31, 119, 180),   // Blue
                Color.FromRgb(255, 127, 14),   // Orange
                Color.FromRgb(44, 160, 44),    // Green
                Color.FromRgb(214, 39, 40),    // Red
                Color.FromRgb(148, 103, 189),  // Purple
                Color.FromRgb(140, 86, 75),    // Brown
                Color.FromRgb(227, 119, 194),  // Pink
                Color.FromRgb(127, 127, 127),  // Gray
                Color.FromRgb(188, 189, 34),   // Olive
                Color.FromRgb(23, 190, 207),   // Cyan
            };
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Set default selections
                await SetDefaultSelections();

                // Initial chart update
                if (FileComboBox.SelectedItem is string selectedFile && _loadedData.ContainsKey(selectedFile))
                {
                    await UpdateDataInfo(selectedFile);
                    await ScheduleUpdateChart(_loadedData[selectedFile], GetSelectedParameters());
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error loading page: {ex.Message}");
            }
        }

        private async Task SetDefaultSelections()
        {
            await Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    foreach (var item in ParameterListBox.Items.Cast<string>())
                    {
                        var container = ParameterListBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                        if (container != null && (item == "TrPress" || item == "SlurRate"))
                            container.IsSelected = true;
                    }
                });
            });
        }

        private void LoadFiles()
        {
            foreach (var kvp in _loadedData)
            {
                FileComboBox.Items.Add(kvp.Key);
            }

            if (FileComboBox.Items.Count > 0)
                FileComboBox.SelectedIndex = 0;
        }

        private async Task UpdateDataInfo(string fileName)
        {
            if (!_loadedData.ContainsKey(fileName)) return;

            var data = _loadedData[fileName];
            var info = $"Records: {data.Count:N0}\n";

            if (data.Count > 0)
            {
                info += $"Time Range: {data.First().Time:HH:mm:ss} - {data.Last().Time:HH:mm:ss}\n";
                var duration = data.Last().Time - data.First().Time;
                info += $"Duration: {duration.TotalMinutes:F1} minutes\n";

                // Add fracture detection info
                var fractureMoments = _breakdownDetector.DetectFractureMoments(data);
                info += $"Fractures detected: {fractureMoments.Count}";
            }

            DataInfoText.Text = info;
        }

        private async void FileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (FileComboBox.SelectedItem is string selectedFile && _loadedData.ContainsKey(selectedFile))
                {
                    await UpdateDataInfo(selectedFile);
                    ChartTitleText.Text = $"Аналіз даних свердловини - {Path.GetFileNameWithoutExtension(selectedFile)}";
                    await ScheduleUpdateChart(_loadedData[selectedFile], GetSelectedParameters());
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error changing file selection: {ex.Message}");
            }
        }

        private async void ParameterCheckChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (FileComboBox.SelectedItem is string selectedFile && _loadedData.ContainsKey(selectedFile))
                {
                    await ScheduleUpdateChart(_loadedData[selectedFile], GetSelectedParameters());
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error updating parameters: {ex.Message}");
            }
        }

        private void SelectAllBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ParameterListBox.SelectAll();
            }
            catch (Exception ex)
            {
                ShowError($"Error selecting all parameters: {ex.Message}");
            }
        }

        private void ClearAllBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ParameterListBox.UnselectAll();
            }
            catch (Exception ex)
            {
                ShowError($"Error clearing parameter selection: {ex.Message}");
            }
        }

        private async void ChartOptionChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (FileComboBox.SelectedItem is string selectedFile && _loadedData.ContainsKey(selectedFile))
                {
                    await ScheduleUpdateChart(_loadedData[selectedFile], GetSelectedParameters());
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error updating chart options: {ex.Message}");
            }
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "PNG Image|*.png|JPEG Image|*.jpg",
                    Title = "Save Chart Image",
                    FileName = $"WellDataChart_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    ExportChart(saveDialog.FileName);
                    MessageBox.Show("Chart exported successfully!", "Export Complete",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error exporting chart: {ex.Message}");
            }
        }

        private void ExportChart(string filePath)
        {
            var renderBitmap = new RenderTargetBitmap(
                (int)TimeChart.ActualWidth, (int)TimeChart.ActualHeight,
                96, 96, PixelFormats.Pbgra32);

            renderBitmap.Render(TimeChart);

            var encoder = Path.GetExtension(filePath).ToLower() == ".png"
                ? (BitmapEncoder)new PngBitmapEncoder()
                : new JpegBitmapEncoder();

            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

            using var stream = new FileStream(filePath, FileMode.Create);
            encoder.Save(stream);
        }

        private List<string> GetSelectedParameters()
        {
            return ParameterListBox.SelectedItems.Cast<string>().ToList();
        }

        private async Task UpdateChartAsync(List<WellDataRecord> data, List<string> selectedParameters)
        {
            if (data == null || !selectedParameters.Any())
            {
                Dispatcher.Invoke(() =>
                {
                    TimeChart.Series.Clear();
                    NoDataMessage.Visibility = Visibility.Visible;
                    ChartSubtitleText.Text = "Select parameters to display data";
                });
                return;
            }

            ShowLoading(true);

            try
            {
                var chartData = await PrepareChartData(data, selectedParameters);

                // Ресемплінг даних
                var (resampledTimes, resampledValues) = ResampleData(chartData.times, chartData.values);

                // Detect fracture moments
                var fractureMoments = await Task.Run(() => _breakdownDetector.DetectFractureMoments(data));

                Dispatcher.Invoke(() => UpdateChartUI((resampledTimes, resampledValues), selectedParameters, data, fractureMoments));
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async Task<(string[] times, Dictionary<string, double[]> values)> PrepareChartData(
            List<WellDataRecord> data, List<string> selectedParameters)
        {
            return await Task.Run(() =>
            {
                var times = data.Select(r => r.Time.ToString("HH:mm:ss")).ToArray();
                var valuesDict = new Dictionary<string, double[]>();

                foreach (var param in selectedParameters)
                {
                    var values = data.Select(r => GetValueByParameter(r, param)).ToArray();
                    valuesDict[param] = values;
                }

                return (times, valuesDict);
            });
        }

        /// Ресемплінг (зменшення кількості точок) для рівномірного відбору даних, якщо їх більше за MaxPoints.
        private (string[] times, Dictionary<string, double[]> values) ResampleData(
            string[] times, Dictionary<string, double[]> values)
        {
            int count = times.Length;
            if (count <= MaxPoints)
                return (times, values);

            double step = (double)(count - 1) / (MaxPoints - 1);
            var resampledTimes = new string[MaxPoints];
            var resampledValues = new Dictionary<string, double[]>();

            for (int i = 0; i < MaxPoints; i++)
            {
                int idx = (int)Math.Round(i * step);
                if (idx >= count) idx = count - 1;
                resampledTimes[i] = times[idx];
            }

            foreach (var kvp in values)
            {
                var originalValues = kvp.Value;
                var resampledArray = new double[MaxPoints];
                for (int i = 0; i < MaxPoints; i++)
                {
                    int idx = (int)Math.Round(i * step);
                    if (idx >= count) idx = count - 1;
                    resampledArray[i] = originalValues[idx];
                }
                resampledValues[kvp.Key] = resampledArray;
            }

            return (resampledTimes, resampledValues);
        }

        private void UpdateChartUI((string[] times, Dictionary<string, double[]> values) chartData,
                          List<string> selectedParameters, List<WellDataRecord> originalData, List<DateTime> fractureMoments)
        {
            TimeChart.Series.Clear();
            NoDataMessage.Visibility = Visibility.Collapsed;

            // Add parameter series
            for (int i = 0; i < selectedParameters.Count; i++)
            {
                var param = selectedParameters[i];
                var color = _seriesColors[i % _seriesColors.Count];

                var lineSeries = new LineSeries
                {
                    Title = param,
                    Values = new ChartValues<double>(chartData.values[param]),
                    PointGeometry = ShowPointsCheckBox.IsChecked == true ? DefaultGeometries.Circle : null,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent,
                    Stroke = new SolidColorBrush(color),
                    ScalesYAt = (param == "SlurRate") ? 1 : 0 // 0 - ліва вісь, 1 - права вісь
                };

                TimeChart.Series.Add(lineSeries);
            }

            if (ShowFracturesCheckBox.IsChecked == true)
                AddFractureLines(originalData, fractureMoments);

            // Update subtitle
            var subtitle = $"Parameters displayed: {string.Join(", ", selectedParameters)}";
            if (fractureMoments.Count > 0 && ShowFracturesCheckBox.IsChecked == true)
            {
                subtitle += $" | Fractures detected: {fractureMoments.Count}";
            }
            ChartSubtitleText.Text = subtitle;
        }

        private void AddFractureLines(List<WellDataRecord> originalData, List<DateTime> fractureMoments)
        {
            if (fractureMoments == null || !fractureMoments.Any() || originalData == null || !originalData.Any())
                return;

            var startTime = originalData.First().Time;
            var endTime = originalData.Last().Time;

            // Find min and max values for Y-axis to draw full-height lines
            double minY = double.MaxValue;
            double maxY = double.MinValue;

            foreach (var series in TimeChart.Series.OfType<LineSeries>())
            {
                foreach (double value in series.Values.Cast<double>())
                {
                    if (!double.IsNaN(value))
                    {
                        minY = Math.Min(minY, value);
                        maxY = Math.Max(maxY, value);
                    }
                }
            }

            // Add some padding
            var range = maxY - minY;
            minY -= range * 0.1;
            maxY += range * 0.1;

            // Create vertical lines for each fracture moment
            foreach (var fractureMoment in fractureMoments)
            {
                if (fractureMoment >= startTime && fractureMoment <= endTime)
                {
                    // Find the index of this fracture moment in the time series
                    var timeIndex = FindTimeIndex(originalData, fractureMoment);

                    if (timeIndex >= 0)
                    {
                        // Convert to resampled index if data was resampled
                        var actualIndex = ConvertToResampledIndex(timeIndex, originalData.Count);

                        // Create vertical line as a line series with two points
                        var fractureLine = new LineSeries
                        {
                            Title = $"Fracture {fractureMoments.IndexOf(fractureMoment) + 1}",
                            Stroke = new SolidColorBrush(Colors.Red),
                            StrokeThickness = 2,
                            StrokeDashArray = new System.Windows.Media.DoubleCollection { 5, 5 }, // Dashed line
                            Fill = Brushes.Transparent,
                            PointGeometry = null,
                            LineSmoothness = 0,
                            ScalesYAt = 0 // Use left Y-axis
                        };

                        // Create the vertical line by adding two points at the same X position
                        var lineValues = new ChartValues<double>();

                        // Add points for all X positions, but only show the line at fracture position
                        for (int i = 0; i < (originalData.Count <= MaxPoints ? originalData.Count : MaxPoints); i++)
                        {
                            if (i == actualIndex)
                            {
                                // Add two points at fracture time to create vertical line
                                lineValues.Add(minY);
                                if (i + 1 < (originalData.Count <= MaxPoints ? originalData.Count : MaxPoints))
                                {
                                    lineValues.Add(maxY);
                                    i++; // Skip next iteration since we added two points
                                }
                            }
                            else
                            {
                                lineValues.Add(double.NaN); // No line at other positions
                            }
                        }

                        fractureLine.Values = lineValues;
                        TimeChart.Series.Add(fractureLine);
                    }
                }
            }
        }

        private int FindTimeIndex(List<WellDataRecord> data, DateTime targetTime)
        {
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].Time >= targetTime)
                {
                    return i;
                }
            }
            return data.Count - 1; // Return last index if not found
        }

        private int ConvertToResampledIndex(int originalIndex, int originalCount)
        {
            if (originalCount <= MaxPoints)
                return originalIndex;

            double step = (double)(originalCount - 1) / (MaxPoints - 1);
            return (int)Math.Round(originalIndex / step);
        }

        private double GetValueByParameter(WellDataRecord record, string param)
        {
            return param switch
            {
                "TrPress" => record.TrPress,
                "AnPress" => record.AnPress,
                "BhPress" => record.BhPress,
                "SlurRate" => record.SlurRate,
                "CfldRate" => record.CfldRate,
                "PropCon" => record.PropCon,
                "BhPropCon" => record.BhPropCon,
                "NetPress" => record.NetPress,
                "TmtB600_3050" => record.TmtB600_3050 ?? 0,
                "TmtProp" => record.TmtProp,
                "TmtCfld" => record.TmtCfld,
                "TmtSlur" => record.TmtSlur,
                "B525Conc" => record.B525Conc,
                "B534Conc" => record.B534Conc,
                "J604Conc" => record.J604Conc,
                "U028Conc" => record.U028Conc,
                "J627Conc" => record.J627Conc,
                "PcmGuarConc" => record.PcmGuarConc,
                "J475Conc" => record.J475Conc,
                "J218Conc" => record.J218Conc,
                _ => double.NaN
            };
        }

        private async Task ScheduleUpdateChart(List<WellDataRecord> data, List<string> selectedParameters)
        {
            _updateCts.Cancel();
            _updateCts = new CancellationTokenSource();

            try
            {
                await Task.Delay(300, _updateCts.Token);
                await UpdateChartAsync(data, selectedParameters);
            }
            catch (TaskCanceledException)
            {
                // ignore, new update scheduled
            }
        }

        private void ShowLoading(bool isLoading)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingIndicator.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
                TimeChart.Visibility = isLoading ? Visibility.Hidden : Visibility.Visible;
                NoDataMessage.Visibility = Visibility.Collapsed;
            });
        }

        private void ShowError(string message)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
    }
}