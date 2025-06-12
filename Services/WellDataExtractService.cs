using FrekingCompareAnalysis.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace FrekingCompareAnalysis.Services
{
    public class WellDataExtractService
    {
        public async Task<Dictionary<string, List<WellDataRecord>>> ReadMultipleFilesAsync(string directoryPath, string filePattern = "*.txt")
        {
            var allData = new Dictionary<string, List<WellDataRecord>>();
            var files = Directory.GetFiles(directoryPath, filePattern);

            Regex regex = new Regex(@"_STG (\d+)");

            files = files.OrderBy(file =>
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var match = regex.Match(fileName);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                {
                    return num;
                }
                return int.MaxValue;
            }).ToArray();

            foreach (var file in files)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var data = await ReadWellDataFileAsync(file);
                    allData[fileName] = data;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to read {file}: {ex.Message}");
                }
            }

            return allData;
        }

        public async Task<List<WellDataRecord>> ReadWellDataFileAsync(string filePath)
        {
            var file = new List<WellDataRecord>();

            try
            {
                using (var reader = new StreamReader(filePath))
                {
                    await reader.ReadLineAsync(); // Назви колонок
                    await reader.ReadLineAsync(); // Величини, в яких вимірюється
                    await reader.ReadLineAsync(); // Роздільна лінія

                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var record = ParseDataLine(line);
                        if (record != null)
                            file.Add(record);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error reading file {filePath}: {ex.Message}", ex);
            }

            return file;
        }

        private WellDataRecord ParseDataLine(string line)
        {
            try
            {
                var parts = line.Split('\t');

                // Мінімум 19 колонка, максимум 22 (з TMT B600-3050)
                if (parts.Length < 19)
                    return null;

                var record = new WellDataRecord();

                if (!DateTime.TryParseExact(parts[0], "MM:dd:yyyy:HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime time))
                {
                    return null;
                }

                record.Time = time;
                record.TrPress = ParseDouble(parts[1]);
                record.AnPress = ParseDouble(parts[2]);
                record.BhPress = ParseDouble(parts[3]);
                record.SlurRate = ParseDouble(parts[4]);
                record.CfldRate = ParseDouble(parts[5]);
                record.PropCon = ParseDouble(parts[6]);
                record.BhPropCon = ParseDouble(parts[7]);
                record.NetPress = ParseDouble(parts[8]);

                int offset = 0;

                if (parts.Length == 21)
                {
                    record.TmtB600_3050 = ParseDouble(parts[9]);
                    offset = 1;
                }
                else {
                    record.TmtB600_3050 = 0.0;
                }

                record.TmtProp = ParseDouble(parts[9 + offset]);
                record.TmtCfld = ParseDouble(parts[10 + offset]);
                record.TmtSlur = ParseDouble(parts[11 + offset]);
                record.B525Conc = ParseDouble(parts[12 + offset]);
                record.B534Conc = ParseDouble(parts[13 + offset]);
                record.J604Conc = ParseDouble(parts[14 + offset]);
                record.U028Conc = ParseDouble(parts[15 + offset]);
                record.J627Conc = ParseDouble(parts[16 + offset]);
                record.PcmGuarConc = ParseDouble(parts[17 + offset]);
                record.J475Conc = ParseDouble(parts[18 + offset]);
                if (parts.Length > 19)
                {
                    record.J218Conc = ParseDouble(parts[19 + offset]);
                }
                else
                {
                    record.J218Conc = 0.0;
                }

                return record;
            }
            catch (Exception)
            {
                return null;
            }
        }


        private double ParseDouble(string value)
        {
            if (double.TryParse(value, out double result))
                return result;
            return 0.0;
        }
    }
}
