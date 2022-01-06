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
        private readonly int _targetCount;
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
            _targetCount = _targetColumnNames.Count;
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
            var start = fieldsLength - _targetCount;
            var outputRecords = (from g in groups
                                 orderby g.Key[0], g.Key[1]
                                 let averages = Enumerable.Range(start, _targetCount).Select(t => CalculateAve(g, t)).ToArray()
                                 let standards = Enumerable.Range(start, _targetCount).Select(t => CalculateStd(g, averages[t - start], t)).ToArray()
                                 let sums = Enumerable.Range(start, _targetCount).Select(t => CalculateSum(g, t)).ToArray()
                                 select new
                                 {
                                     cols = g.Key,
                                     col1 = g.Key[0],
                                     col2 = g.Key[1],
                                     averages = averages,
                                     standards = standards,
                                     sums = sums
                                 }).ToList();

            //出力
            writer.WriteFields(_headerNames);
            writer.NextRecord();

            foreach (var record in outputRecords)
            {
                for (int i = 0; i < _targetCount; i++)
                {
                    writer.WriteField(record.cols[i]);
                }
                for (int i = 0; i < _targetCount; i++)
                {
                    writer.WriteField(record.sums[i]);
                    writer.WriteField(record.averages[i]);
                    writer.WriteField(record.standards[i]);
                }
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

        private decimal CalculateSum(IGrouping<string[], Record> group, int index)
        {
            return group.Sum(r => decimal.Parse(r.Fields[index].ToString()));
        }
    }
}
