using FrekingCompareAnalysis.Models;

namespace FrekingCompareAnalysis.Services
{
    public class FractureConds
    {
        public class FractureCondition
        {
            public DateTime BreakdownStart { get; set; }
            public Dictionary<string, double> Slopes { get; set; } // зберігаємо нахили

            public override string ToString()
            {
                var summary = string.Join(", ", Slopes.Select(kvp => $"{kvp.Key}: {kvp.Value:F5}"));
                return $"{BreakdownStart:HH:mm:ss} => {summary}";
            }
        }

        public Dictionary<string, double> AverageSlopesAcrossFiles { get; private set; } = new();

        public List<FractureCondition> AnalyzePreBreakdownConditions(
            List<WellDataRecord> data,
            List<DateTime> breakdowns,
            int preBreakdownWindow = 30)
        {
            var conditions = new List<FractureCondition>();
            var sortedData = data.OrderBy(d => d.Time).ToList();

            foreach (var breakdown in breakdowns)
            {
                var windowStart = breakdown - TimeSpan.FromSeconds(preBreakdownWindow);
                var windowEnd = breakdown;

                var windowData = sortedData
                    .Where(r => r.Time >= windowStart && r.Time <= windowEnd)
                    .ToList();

                if (windowData.Count < 2)
                    continue;

                var slopes = new Dictionary<string, double>();

                void AnalyzeSlope(string name, Func<WellDataRecord, double?> selector)
                {
                    var points = windowData
                        .Select(r => new { X = r.Time.Subtract(windowStart).TotalSeconds, Y = selector(r) ?? 0 })
                        .ToList();

                    int n = points.Count;
                    double sumX = points.Sum(p => p.X);
                    double sumY = points.Sum(p => p.Y);
                    double sumXY = points.Sum(p => p.X * p.Y);
                    double sumXX = points.Sum(p => p.X * p.X);

                    double denominator = n * sumXX - sumX * sumX;
                    if (Math.Abs(denominator) < 1e-8)
                    {
                        slopes[name] = double.NaN;
                        return;
                    }

                    double slope = (n * sumXY - sumX * sumY) / denominator;
                    slopes[name] = slope;
                }

                AnalyzeSlope("TrPress", r => r.TrPress);
                AnalyzeSlope("AnPress", r => r.AnPress);
                AnalyzeSlope("BhPress", r => r.BhPress);
                AnalyzeSlope("SlurRate", r => r.SlurRate);
                AnalyzeSlope("PropCon", r => r.PropCon);
                AnalyzeSlope("BhPropCon", r => r.BhPropCon);
                AnalyzeSlope("NetPress", r => r.NetPress);

                conditions.Add(new FractureCondition
                {
                    BreakdownStart = breakdown,
                    Slopes = slopes
                });
            }

            return conditions;
        }

        public Dictionary<string, double> CalculateSlopes(List<WellDataRecord> windowData)
        {
            var slopes = new Dictionary<string, double>();

            void AnalyzeSlope(string name, Func<WellDataRecord, double?> selector)
            {
                var startTime = windowData.First().Time;
                var points = windowData
                    .Select(r => new { X = r.Time.Subtract(startTime).TotalSeconds, Y = selector(r) ?? 0 })
                    .ToList();

                int n = points.Count;
                if (n < 2)
                {
                    slopes[name] = double.NaN;
                    return;
                }

                double sumX = points.Sum(p => p.X);
                double sumY = points.Sum(p => p.Y);
                double sumXY = points.Sum(p => p.X * p.Y);
                double sumXX = points.Sum(p => p.X * p.X);

                double denominator = n * sumXX - sumX * sumX;
                if (Math.Abs(denominator) < 1e-8)
                {
                    slopes[name] = double.NaN;
                    return;
                }

                double slope = (n * sumXY - sumX * sumY) / denominator;
                slopes[name] = slope;
            }

            AnalyzeSlope("TrPress", r => r.TrPress);
            AnalyzeSlope("AnPress", r => r.AnPress);
            AnalyzeSlope("BhPress", r => r.BhPress);
            AnalyzeSlope("SlurRate", r => r.SlurRate);
            AnalyzeSlope("PropCon", r => r.PropCon);
            AnalyzeSlope("BhPropCon", r => r.BhPropCon);
            AnalyzeSlope("NetPress", r => r.NetPress);

            return slopes;
        }

        public bool IsSimilarToFavorable(Dictionary<string, double> candidate, Dictionary<string, double> favorable, double tolerance = 0.1, double absoluteTolerance = 0.01, double requiredMatchRatio = 0.9)
        {
            int total = favorable.Count;
            int matches = 0;

            foreach (var kvp in favorable)
            {
                string param = kvp.Key;
                double favorableSlope = kvp.Value;

                if (!candidate.TryGetValue(param, out double testSlope))
                    continue;

                if (double.IsNaN(testSlope) || double.IsNaN(favorableSlope))
                    continue;

                double absDiff = Math.Abs(testSlope - favorableSlope);

                // Якщо обидва значення близькі до нуля — вважаємо подібними
                if (Math.Abs(favorableSlope) < 1e-4 && Math.Abs(testSlope) < 1e-4)
                {
                    matches++;
                    continue;
                }

                // Відносне відхилення
                double ratio = Math.Abs((testSlope - favorableSlope) / (Math.Abs(favorableSlope) + 1e-6));

                if (ratio <= tolerance || absDiff <= absoluteTolerance)
                    matches++;
            }

            // Вважаємо схожими, якщо достатньо параметрів співпадає
            return (double)matches / total >= requiredMatchRatio;
        }


        public void AnalyzeMultipleFiles(
            List<List<WellDataRecord>> allFilesData,
            List<List<DateTime>> allBreakdowns,
            int preBreakdownWindow = 30)
        {
            var slopeSums = new Dictionary<string, double>();
            var slopeCounts = new Dictionary<string, int>();

            for (int i = 0; i < allFilesData.Count; i++)
            {
                var data = allFilesData[i];
                var breakdowns = allBreakdowns[i];

                var conditions = AnalyzePreBreakdownConditions(data, breakdowns, preBreakdownWindow);

                foreach (var cond in conditions)
                {
                    foreach (var kvp in cond.Slopes)
                    {
                        if (double.IsNaN(kvp.Value))
                            continue;

                        string param = kvp.Key;
                        double slope = kvp.Value;

                        if (!slopeSums.ContainsKey(param))
                        {
                            slopeSums[param] = 0;
                            slopeCounts[param] = 0;
                        }

                        slopeSums[param] += slope;
                        slopeCounts[param]++;
                    }
                }
            }

            var averageSlopes = new Dictionary<string, double>();
            foreach (var kvp in slopeSums)
            {
                string param = kvp.Key;
                double avg = kvp.Value / slopeCounts[param];
                averageSlopes[param] = avg;
            }

            AverageSlopesAcrossFiles = averageSlopes;
        }
    }
}
