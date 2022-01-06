using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Umwelt.Faculty.Csv.Algorithm2.Models;

namespace Umwelt.Faculty.Csv.Algorithm2
{
    class Faculty
    {
        private readonly string _inputPath;
        private readonly string _outputPath;
        private readonly List<string> _sortColumnNames;
        private readonly List<string> _targetColumnNames;
        private readonly List<string> _resultColumnNames;
        private List<string> _headerNames;
        private readonly string DATE_HEADER_NAME = "date";
        private readonly List<string> _suffixes;

        public Faculty(IConfiguration configuration)
        {
            (_inputPath, _outputPath) = Initializer.InFileOutFile(configuration);
            var incar = configuration.GetSection("INCAR");

            _sortColumnNames = incar["SortColumns"].Split(',').ToList();
            _targetColumnNames = incar["TargetColumns"].Split(',').ToList();
            _suffixes = new List<string>
            {
                "Sum",
                "Ave",
                "σ"
            };

            _resultColumnNames = new List<string>();

            foreach (var t in _targetColumnNames)
            {
                foreach (var s in _suffixes)
                {
                    _resultColumnNames.Add($"{t}-{s}");
                }
            }

            _headerNames = _sortColumnNames.Concat(_resultColumnNames).ToList();

        }

        public async Task ExecuteAsync()
        {
            using var reader = Csv.OpenRead(_inputPath);
            using var writer = Csv.Create(_outputPath);
            reader.Read();
            reader.ReadHeader();
            var records = new List<Record>();
            while (reader.Read())
            {
                var record = reader.GetRecord(DATE_HEADER_NAME, _sortColumnNames, new[] { "count" });
                if (record is null) continue;
                records.Add(record);
            }

            //集計
            var groups = records.GroupBy(t => t.Keys, StringArrayEqualityComparer.Default).ToList();
            var fieldsLength = records.First().Fields.Length;

            var outputRecords = (from g in groups
                                 orderby g.Key[0], g.Key[1]
                                 let ave1 = CalculateAve(g, 3)
                                 let ave2 = CalculateAve(g, 4)
                                 let std1 = CalculateStd(g, ave1, 3)
                                 let std2 = CalculateStd(g, ave2, 4)
                                 select new
                                 {
                                     col1 = g.Key[0],
                                     col2 = g.Key[1],
                                     countSum = CaclulateSum(g, 3),
                                     countAve = ave1,
                                     countStd = std1,
                                     priceSum = CaclulateSum(g, 4),
                                     priceAve = ave2,
                                     priceStd = std2,
                                 }).ToList();

            //出力
            writer.WriteFields(_headerNames);
            writer.NextRecord();

            foreach (var record in outputRecords)
            {
                writer.WriteRecord(record);
                writer.NextRecord();
            }
        }

        private double CalculateStd(IEnumerable<Record> g, decimal ave, int index)
        {
            var count = g.Count();
            var sum = g.Sum(r => Math.Abs(decimal.Parse(r.Fields[index].ToString()) - ave));
            return Math.Sqrt((double)sum / count);
        }

        private decimal CalculateAve(IGrouping<string[], Record> group, int index)
        {
            return Math.Round(group.Average(r => decimal.Parse(r.Fields[index].ToString())), 2);
        }

        private decimal CaclulateSum(IGrouping<string[], Record> group, int index)
        {
            return group.Sum(r => decimal.Parse(r.Fields[index].ToString()));
        }
    }
}
