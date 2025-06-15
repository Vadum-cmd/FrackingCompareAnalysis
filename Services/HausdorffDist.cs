namespace FrekingCompareAnalysis.Services
{
    public class HausdorffDist
    {
        public double GetHausdorffDist(List<DateTime> darcy, List<DateTime> trends)
        {
            double maxDarcyToTrends = darcy.Max(d => trends.Min(t => Math.Abs((d - t).TotalSeconds)));
            double maxTrendsToDarcy = trends.Max(t => darcy.Min(d => Math.Abs((t - d).TotalSeconds)));

            return Math.Max(maxDarcyToTrends, maxTrendsToDarcy);
        }
    }
}
