using FrekingCompareAnalysis.Models;

namespace FrekingCompareAnalysis.Services
{
    public class FracturePredictionModule
    {
        private readonly BreakdownDetector _detector;
        private readonly FractureConds _fractureConds;

        public Dictionary<string, List<DateTime>> DetectedBreakdowns { get; private set; } = new();
        public Dictionary<string, double> FavorableConditions { get; private set; } = new();

        public FracturePredictionModule(BreakdownDetector.BreakdownDetectionSettings settings = null)
        {
            _detector = new BreakdownDetector(settings);
            _fractureConds = new FractureConds();
        }

        public void DetectBreakdownsForFiles(Dictionary<string, List<WellDataRecord>> files)
        {
            DetectedBreakdowns.Clear();

            foreach (var kvp in files)
            {
                string fileName = kvp.Key;
                var data = kvp.Value;

                var breakdowns = _detector.DetectFractureMoments(data);
                DetectedBreakdowns[fileName] = breakdowns;
            }
        }

        public void AnalyzeFavorableConditions(Dictionary<string, List<WellDataRecord>> files)
        {
            var allData = new List<List<WellDataRecord>>();
            var allBreakdowns = new List<List<DateTime>>();

            foreach (var kvp in files)
            {
                string fileName = kvp.Key;

                if (!DetectedBreakdowns.ContainsKey(fileName))
                    continue;

                allData.Add(kvp.Value);
                allBreakdowns.Add(DetectedBreakdowns[fileName]);
            }

            _fractureConds.AnalyzeMultipleFiles(allData, allBreakdowns);
            FavorableConditions = _fractureConds.AverageSlopesAcrossFiles;
        }

        // (breakdownsDarcy, breakdownsTrend)
        public (List<DateTime>, List<DateTime>) ApplyConditionsToNewFile(List<WellDataRecord> newFileData)
        {
            if (FavorableConditions == null || FavorableConditions.Count == 0)
                throw new InvalidOperationException("Сприятливі умови ще не були проаналізовані.");

            var breakdownsDarcy = _detector.DetectFractureMoments(newFileData);

            var breakdownsTrend = new List<DateTime>();

            int windowSeconds = 60;  // розмір вікна до моменту
            int offsetSeconds = 30;  // зсув після моменту

            for (int i = 0; i < newFileData.Count; i++)
            {
                var currentTime = newFileData[i].Time;

                DateTime windowStart = currentTime.AddSeconds(-windowSeconds);
                DateTime windowEnd = currentTime;

                var windowData = newFileData
                    .Where(r => r.Time >= windowStart && r.Time <= windowEnd)
                    .ToList();

                if (windowData.Count < 2)
                    continue;

                var condition = _fractureConds.CalculateSlopes(windowData);

                if (_fractureConds.IsSimilarToFavorable(condition, FavorableConditions))
                {
                    var recordAfterOffset = newFileData.FirstOrDefault(r => r.Time >= currentTime.AddSeconds(offsetSeconds));
                    if (recordAfterOffset != null)
                    {
                        breakdownsTrend.Add(recordAfterOffset.Time);
                    }
                }
            }

            return (breakdownsDarcy, breakdownsTrend);
        }
    }
}
