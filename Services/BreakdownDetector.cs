using FrekingCompareAnalysis.Models;

namespace FrekingCompareAnalysis.Services
{
    public class BreakdownDetector
    {
        public class BreakdownDetectionSettings
        {
            public double MinBreakdownDurationSeconds { get; set; } = 30;
            public double MinKIncreaseRatio { get; set; } = 0.01; // >1%
            public double DataSkipProportion { get; set; } = 0.2; // 20%
            public double MinRateThreshold { get; set; } = 0.1; // bbl/min
            public double FiltrationRadius { get; set; } = 0.5; // m
            public double ReservoirPressure { get; set; } = 3000; // psi
            public double FluidViscosity { get; set; } = 2.5; // cP
        }

        private readonly BreakdownDetectionSettings _settings;

        public BreakdownDetector(BreakdownDetectionSettings settings = null)
        {
            _settings = settings ?? new BreakdownDetectionSettings();
        }

        public List<DateTime> DetectFractureMoments(List<WellDataRecord> data)
        {
            var result = new List<DateTime>();

            if (data == null || data.Count < 2)
                return result;

            int startIndex = (int)(data.Count * _settings.DataSkipProportion);
            int endIndex = data.Count - (int)(data.Count * _settings.DataSkipProportion / 2);

            Queue<double> kWindow = new Queue<double>();
            const int kWindowSize = 5;
            DateTime? lastBreakdownTime = null;

            for (int i = startIndex + 1; i < endIndex; i++)
            {
                var currentTime = data[i].Time;

                if (lastBreakdownTime.HasValue &&
                    (currentTime - lastBreakdownTime.Value).TotalSeconds < _settings.MinBreakdownDurationSeconds)
                {
                    continue;
                }

                if (data[i].SlurRate < _settings.MinRateThreshold)
                    continue;

                double Q = data[i].SlurRate / 8.3864 / 60.0; // bbl/min → m³/s
                double Pc = _settings.ReservoirPressure;
                double Pk = data[i].BhPress;
                double Rc = _settings.FiltrationRadius;
                double mu = _settings.FluidViscosity * 1e-3; // cP → Pa·s

                double denominator = (Pk - Pc) * Rc;

                if (Math.Abs(denominator) < 1e-6 || Q <= 0)
                    continue;

                double k = (mu / (4 * Math.PI)) * (Q / denominator);

                // Додаємо до вікна згладжування
                kWindow.Enqueue(k);
                if (kWindow.Count > kWindowSize)
                    kWindow.Dequeue();

                if (kWindow.Count == kWindowSize)
                {
                    double avgK = kWindow.Average();
                    double deltaK = k - avgK;

                    // Чутливі до невеликих зростань, але не до шумових піків
                    if (deltaK > avgK * _settings.MinKIncreaseRatio && deltaK < avgK * 5)
                    {
                        result.Add(currentTime);
                        lastBreakdownTime = currentTime;

                        // Очищаємо вікно після розриву, щоб уникнути повторного спрацювання на один і той самий пік
                        kWindow.Clear();
                    }
                }
            }

            return result;
        }

    }
}