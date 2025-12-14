using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace HolocronToolset.NET.Tests.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_sort.py
    // Original: Tests for sorting logic with extracted values
    public class SortTests
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_sort.py:6-10
        // Original: class ERFResource:
        private class ERFResource
        {
            public string Name { get; set; }
            public string ResType { get; set; }
            public int Size { get; set; }

            public ERFResource(string name, string resType, int size)
            {
                Name = name;
                ResType = resType;
                Size = size;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_sort.py:13-38
        // Original: class ERFSortFilterProxyModel:
        private class ERFSortFilterProxyModel
        {
            private List<ERFResource> _data = new List<ERFResource>();

            public void AddData(string resref, string restype, int size)
            {
                var resource = new ERFResource(resref, restype, size);
                _data.Add(resource);
            }

            public void Sort(int column, string order = "ascending")
            {
                bool reverse = (order == "descending");

                // Extracting sort key based on the column index provided
                Func<ERFResource, IComparable> keyFunc;
                if (column == 0)
                {
                    keyFunc = x => x.Name;
                }
                else if (column == 1)
                {
                    keyFunc = x => x.ResType;
                }
                else if (column == 2)
                {
                    keyFunc = x => x.Size;
                }
                else
                {
                    throw new ArgumentException("Invalid column index");
                }

                // Apply sort; C#'s OrderBy is stable which maintains the relative order of records that compare equal
                if (reverse)
                {
                    _data = _data.OrderByDescending(keyFunc).ToList();
                }
                else
                {
                    _data = _data.OrderBy(keyFunc).ToList();
                }
            }

            public List<Tuple<string, string, int>> GetData()
            {
                return _data.Select(item => Tuple.Create(item.Name, item.ResType, item.Size)).ToList();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_sort.py:40-87
        // Original: Data and tests setup
        [Fact]
        public void TestSortingLogic()
        {
            // Data and tests setup
            var data = new List<Tuple<string, string, int>>
            {
                Tuple.Create("workbnch_tut", "DLG", (int)(135.13 * 1024)),
                Tuple.Create("3cfd", "DLG", (int)(97.3 * 1024)),
                Tuple.Create("intro", "DLG", (int)(49.58 * 1024)),
                Tuple.Create("seccon", "DLG", (int)(45.78 * 1024)),
                Tuple.Create("combat", "DLG", (int)(44.2 * 1024)),
                Tuple.Create("001ebo", "GIT", (int)(43.24 * 1024)),
                Tuple.Create("extra", "DLG", (int)(36.92 * 1024)),
                Tuple.Create("hyper", "DLG", (int)(25.38 * 1024)),
                Tuple.Create("001ebo", "PTH", (int)(19.32 * 1024)),
                Tuple.Create("001ebo", "ARE", (int)(4.75 * 1024))
            };

            // Initialize and populate the sorter
            var sorter = new ERFSortFilterProxyModel();
            foreach (var (resref, restype, size) in data)
            {
                sorter.AddData(resref, restype, size);
            }

            // Perform the primary sort by size in descending order
            sorter.Sort(2, "descending");
            // Then apply a secondary sort by name in ascending order
            sorter.Sort(0, "ascending");

            // Test the sorting
            var sortedData = sorter.GetData();

            var test1 = Tuple.Create("001ebo", "GIT", (int)(43.24 * 1024));
            var test2 = Tuple.Create("001ebo", "PTH", (int)(19.32 * 1024));
            var test3 = Tuple.Create("001ebo", "ARE", (int)(4.75 * 1024));
            var test4 = Tuple.Create("3cfd", "DLG", (int)(97.3 * 1024));

            sortedData[0].Should().Be(test1, $"sortedData[0] == {test1}");
            sortedData[1].Should().Be(test2, $"sortedData[1] == {test2}");
            sortedData[2].Should().Be(test3, $"sortedData[2] == {test3}");
            sortedData[3].Should().Be(test4, $"sortedData[3] == {test4}");

            sorter.Sort(0, "descending");
            sortedData = sorter.GetData();

            var test5 = Tuple.Create("workbnch_tut", "DLG", (int)(135.13 * 1024));
            var test6 = Tuple.Create("seccon", "DLG", (int)(45.78 * 1024));
            var test7 = Tuple.Create("intro", "DLG", (int)(49.58 * 1024));
            var test8 = Tuple.Create("hyper", "DLG", (int)(25.38 * 1024));

            sortedData[0].Should().Be(test5, $"sortedData[0] == {test5}");
            sortedData[1].Should().Be(test6, $"sortedData[1] == {test6}");
            sortedData[2].Should().Be(test7, $"sortedData[2] == {test7}");
            sortedData[3].Should().Be(test8, $"sortedData[3] == {test8}");
        }
    }
}
