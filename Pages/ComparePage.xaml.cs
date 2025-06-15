using FrekingCompareAnalysis.Models;
using FrekingCompareAnalysis.Services;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace FrekingCompareAnalysis.Pages
{
    public partial class ComparePage : Page
    {
        private readonly Dictionary<string, List<WellDataRecord>> _loadedData;
        private FracturePredictionModule _fracturePredictionModule;
        private HausdorffDist _hausdorffDist;
        private Dictionary<string, double> _favorableConditions = new();
        private List<DateTime> _breakdownsDarcy = new();
        private List<DateTime> _breakdownsTrend = new();
        private double _dist;

        // Maximum number of points after resampling
        private const int MaxPoints = 200;

        public SeriesCollection SeriesCollection { get; set; }
        public Func<double, string> TimeFormatter { get; set; }

        public ComparePage(Dictionary<string, List<WellDataRecord>> loadedData)
        {
            InitializeComponent();
            _loadedData = loadedData;
            _fracturePredictionModule = new FracturePredictionModule();
            _hausdorffDist = new HausdorffDist();

            InitializeChart();
            PopulateFileComboBox();
            DataContext = this;
        }

        private void InitializeChart()
        {
            SeriesCollection = new SeriesCollection();

            // Configure the time formatter
            TimeFormatter = value => new DateTime((long)value).ToString("HH:mm:ss");

            // Configure the mapper for WellDataRecord
            var mapper = Mappers.Xy<WellDataPoint>()
                .X(model => model.Time.Ticks)
                .Y(model => model.Value);

            Charting.For<WellDataPoint>(mapper);

            // Configure the mapper for breakdown points
            var breakdownMapper = Mappers.Xy<BreakdownPoint>()
                .X(model => model.Time.Ticks)
                .Y(model => model.Value);

            Charting.For<BreakdownPoint>(breakdownMapper);

            MainChart.Series = SeriesCollection;
        }

        private void PopulateFileComboBox()
        {
            CmbFileSelection.Items.Clear();
            CmbFileSelection.Items.Add("-- Виберіть файл --");

            foreach (var fileName in _loadedData.Keys)
            {
                CmbFileSelection.Items.Add(fileName);
            }

            CmbFileSelection.SelectedIndex = 0;
        }

        private void BtnAnalyzeFavorableConditions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show file selection dialog
                var selectedFiles = ShowFileSelectionDialog();

                if (selectedFiles == null || selectedFiles.Count == 0)
                    return;

                // First detect breakdowns for selected files
                _fracturePredictionModule.DetectBreakdownsForFiles(selectedFiles);

                // Then analyze favorable conditions
                _fracturePredictionModule.AnalyzeFavorableConditions(selectedFiles);

                // Update the favorable conditions
                _favorableConditions = _fracturePredictionModule.FavorableConditions;

                // Update status
                TxtFavorableConditionsStatus.Text = $"Ідеальні умови отримано з {selectedFiles.Count} файлів. " +
                                                   $"Знайдено {_favorableConditions.Count} параметрів.";

                MessageBox.Show($"Аналіз ідеальних умов виконано!\n" +
                              $"Проаналізовано {selectedFiles.Count} файлів.\n" +
                              $"Знайдено {_favorableConditions.Count} параметрів.",
                              "Аналіз завершено", MessageBoxButton.OK, MessageBoxImage.Information);

                // Refresh the current chart if a file is selected
                if (CmbFileSelection.SelectedIndex > 0)
                {
                    UpdateChart();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час аналізу трендів: {ex.Message}",
                              "Помилка аналізу", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Dictionary<string, List<WellDataRecord>> ShowFileSelectionDialog()
        {
            var dialog = new FileSelectionDialog(_loadedData.Keys.ToList());

            if (dialog.ShowDialog() == true)
            {
                var selectedFiles = new Dictionary<string, List<WellDataRecord>>();

                foreach (var fileName in dialog.SelectedFiles)
                {
                    if (_loadedData.ContainsKey(fileName))
                    {
                        selectedFiles[fileName] = _loadedData[fileName];
                    }
                }

                return selectedFiles;
            }

            return null;
        }

        private void CmbFileSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbFileSelection.SelectedIndex <= 0)
            {
                ClearChart();
                TxtChartTitle.Text = "Виберіть файл щоб побачити графік";
                TxtBreakdownInfo.Text = "Виберіть файл щоб отримати інформацію про гідророзриви";
                return;
            }

            UpdateChart();
        }

        private List<WellDataRecord> ResampleData(List<WellDataRecord> originalData)
        {
            if (originalData == null || originalData.Count <= MaxPoints)
                return originalData;

            var resampledData = new List<WellDataRecord>();
            double step = (double)(originalData.Count - 1) / (MaxPoints - 1);

            for (int i = 0; i < MaxPoints; i++)
            {
                int idx = (int)Math.Round(i * step);
                if (idx >= originalData.Count)
                    idx = originalData.Count - 1;

                resampledData.Add(originalData[idx]);
            }

            return resampledData;
        }

        private List<DateTime> ResampleBreakdowns(List<DateTime> breakdowns, List<WellDataRecord> originalData, List<WellDataRecord> resampledData)
        {
            if (breakdowns == null || !breakdowns.Any() || originalData.Count <= MaxPoints)
                return breakdowns;

            var resampledBreakdowns = new List<DateTime>();
            var originalStartTime = originalData.First().Time;
            var originalEndTime = originalData.Last().Time;
            var resampledStartTime = resampledData.First().Time;
            var resampledEndTime = resampledData.Last().Time;

            foreach (var breakdown in breakdowns)
            {
                // Skip breakdowns outside the time range
                if (breakdown < originalStartTime || breakdown > originalEndTime)
                    continue;

                // Find the closest time in resampled data
                var closestTime = resampledData
                    .OrderBy(r => Math.Abs((r.Time - breakdown).TotalSeconds))
                    .First().Time;

                if (!resampledBreakdowns.Contains(closestTime))
                {
                    resampledBreakdowns.Add(closestTime);
                }
            }

            return resampledBreakdowns.OrderBy(t => t).ToList();
        }

        private void UpdateChart()
        {
            try
            {
                string selectedFileName = CmbFileSelection.SelectedItem.ToString();

                if (!_loadedData.ContainsKey(selectedFileName))
                    return;

                var originalFileData = _loadedData[selectedFileName];

                // Apply resampling to reduce data points
                var fileData = ResampleData(originalFileData);

                TxtChartTitle.Text = $"Аналіз для: {selectedFileName}";

                // Apply conditions to get breakdowns
                List<DateTime> originalBreakdownsDarcy;
                List<DateTime> originalBreakdownsTrend;

                if (_favorableConditions.Any())
                {
                    var (breakdownsDarcy, breakdownsTrend) = _fracturePredictionModule.ApplyConditionsToNewFile(originalFileData);
                    originalBreakdownsDarcy = breakdownsDarcy;
                    originalBreakdownsTrend = breakdownsTrend;
                }
                else
                {
                    // If no favorable conditions, just use basic breakdown detection
                    var tempFiles = new Dictionary<string, List<WellDataRecord>> { { selectedFileName, originalFileData } };
                    _fracturePredictionModule.DetectBreakdownsForFiles(tempFiles);
                    originalBreakdownsDarcy = _fracturePredictionModule.DetectedBreakdowns.ContainsKey(selectedFileName)
                                     ? _fracturePredictionModule.DetectedBreakdowns[selectedFileName]
                                     : new List<DateTime>();
                    originalBreakdownsTrend = new List<DateTime>();
                }

                // Resample breakdowns to match resampled data
                _breakdownsDarcy = ResampleBreakdowns(originalBreakdownsDarcy, originalFileData, fileData);
                _breakdownsTrend = ResampleBreakdowns(originalBreakdownsTrend, originalFileData, fileData);

                _dist = _hausdorffDist.GetHausdorffDist(originalBreakdownsDarcy, originalBreakdownsTrend);
                AccuracyInfo.Text = $"Відстань Хаусдорфа: {_dist:F2} сек";

                // Update breakdown info
                var dataInfo = originalFileData.Count > MaxPoints
                    ? $"Необроблені дані: {originalFileData.Count:N0}, Зменшена вибірка: {fileData.Count:N0}\n"
                    : $"Точки: {fileData.Count:N0}\n";

                TxtBreakdownInfo.Text = dataInfo +
                                       $"Гідророзриви за Дарсі: {_breakdownsDarcy.Count}\n" +
                                       $"Гідророзриви за трендом: {_breakdownsTrend.Count}";

                // Clear existing series
                SeriesCollection.Clear();

                // Add TrPress line series
                var trPressPoints = fileData.Select(r => new WellDataPoint
                {
                    Time = r.Time,
                    Value = r.TrPress
                }).ToList();

                SeriesCollection.Add(new LineSeries
                {
                    Title = "TrPress",
                    Values = new ChartValues<WellDataPoint>(trPressPoints),
                    Stroke = System.Windows.Media.Brushes.Blue,
                    Fill = System.Windows.Media.Brushes.Transparent,
                    PointGeometry = null,
                    LineSmoothness = 0
                });

                // Add SlurRate line series
                var slurRatePoints = fileData.Select(r => new WellDataPoint
                {
                    Time = r.Time,
                    Value = r.SlurRate
                }).ToList();

                SeriesCollection.Add(new LineSeries
                {
                    Title = "SlurRate",
                    Values = new ChartValues<WellDataPoint>(slurRatePoints),
                    Stroke = System.Windows.Media.Brushes.Green,
                    Fill = System.Windows.Media.Brushes.Transparent,
                    PointGeometry = null,
                    LineSmoothness = 0
                });

                // Add Darcy breakdown points
                if (_breakdownsDarcy.Any())
                {
                    var darcyBreakdownPoints = _breakdownsDarcy.Select(time =>
                    {
                        var record = fileData.FirstOrDefault(r => Math.Abs((r.Time - time).TotalSeconds) < 30); // Increased tolerance for resampled data
                        return new BreakdownPoint
                        {
                            Time = time,
                            Value = record?.TrPress ?? 0
                        };
                    }).Where(bp => bp.Value > 0).ToList(); // Filter out invalid points

                    if (darcyBreakdownPoints.Any())
                    {
                        SeriesCollection.Add(new ScatterSeries
                        {
                            Title = "Гідророзрив за Дарсі",
                            Values = new ChartValues<BreakdownPoint>(darcyBreakdownPoints),
                            Fill = System.Windows.Media.Brushes.Red,
                        });
                    }
                }

                // Add Trend breakdown points
                if (_breakdownsTrend.Any())
                {
                    var trendBreakdownPoints = _breakdownsTrend.Select(time =>
                    {
                        var record = fileData.FirstOrDefault(r => Math.Abs((r.Time - time).TotalSeconds) < 30); // Increased tolerance for resampled data
                        return new BreakdownPoint
                        {
                            Time = time,
                            Value = record?.TrPress ?? 0
                        };
                    }).Where(bp => bp.Value > 0).ToList(); // Filter out invalid points

                    if (trendBreakdownPoints.Any())
                    {
                        SeriesCollection.Add(new ScatterSeries
                        {
                            Title = "Гідророзрив за трендом",
                            Values = new ChartValues<BreakdownPoint>(trendBreakdownPoints),
                            Fill = System.Windows.Media.Brushes.Orange,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка оновлення графіка: {ex.Message}",
                              "Помилка графіка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearChart()
        {
            SeriesCollection.Clear();
            _breakdownsDarcy.Clear();
            _breakdownsTrend.Clear();
        }
    }

    // Helper classes for chart data points
    public class WellDataPoint
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    public class BreakdownPoint
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }
}
