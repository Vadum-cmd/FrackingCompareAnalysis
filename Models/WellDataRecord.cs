namespace FrekingCompareAnalysis.Models
{
    public class WellDataRecord
    {
        public DateTime Time { get; set; }
        public double TrPress { get; set; }
        public double AnPress { get; set; }
        public double BhPress { get; set; }
        public double SlurRate { get; set; }
        public double CfldRate { get; set; }
        public double PropCon { get; set; }
        public double BhPropCon { get; set; }
        public double NetPress { get; set; }
        public double? TmtB600_3050 { get; set; }
        public double TmtProp { get; set; }
        public double TmtCfld { get; set; }
        public double TmtSlur { get; set; }
        public double B525Conc { get; set; }
        public double B534Conc { get; set; }
        public double J604Conc { get; set; }
        public double U028Conc { get; set; }
        public double J627Conc { get; set; }
        public double PcmGuarConc { get; set; }
        public double J475Conc { get; set; }
        public double J218Conc { get; set; }
    }
}
