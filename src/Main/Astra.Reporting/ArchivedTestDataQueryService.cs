using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Astra.Core.Archiving;

namespace Astra.Reporting
{
    /// <summary>
    /// 扫描磁盘上的测试归档（<c>测试数据\yyyy-MM-dd\SN\序号\</c>），按原始/音频/算法/报告/运行日志分类；
    /// 运行日志还包含默认目录 <c>%LocalAppData%\Astra\RunLogs</c> 下的 <c>*.log</c>（无归档目录时的落盘位置）。
    /// </summary>
    public sealed class ArchivedTestDataQueryService
    {
        private static readonly CompareInfo ZhCompare = CultureInfo.GetCultureInfo("zh-CN").CompareInfo;

        /// <summary>
        /// 同步查询；调用方可在线程池执行以免阻塞 UI。
        /// </summary>
        public ArchivedTestDataQueryResult Query(string archiveRoot, ArchivedTestDataQueryCriteria criteria)
        {
            criteria ??= new ArchivedTestDataQueryCriteria();
            if (string.IsNullOrWhiteSpace(archiveRoot))
            {
                return new ArchivedTestDataQueryResult
                {
                    ArchiveRoot = string.Empty,
                    ErrorMessage = "归档根目录为空。"
                };
            }

            string root;
            try
            {
                root = Path.GetFullPath(archiveRoot.Trim());
            }
            catch (Exception ex)
            {
                return new ArchivedTestDataQueryResult
                {
                    ArchiveRoot = archiveRoot,
                    ErrorMessage = "归档根目录无效: " + ex.Message
                };
            }

            var testDataRoot = Path.Combine(root, ReportArchiveLayout.TestDataFolderName);
            if (!Directory.Exists(testDataRoot))
            {
                return new ArchivedTestDataQueryResult
                {
                    ArchiveRoot = root,
                    Items = Array.Empty<ArchivedDataFileRow>(),
                    ErrorMessage = null
                };
            }

            var catMask = criteria.Categories == ArchivedDataCategoryFlags.None
                ? ArchivedDataCategoryFlags.All
                : criteria.Categories;

            var fromDate = criteria.FromLocalDateInclusive?.Date;
            var toDate = criteria.ToLocalDateInclusive?.Date;

            var snNeedle = criteria.SnContains?.Trim();
            var snFilterActive = !string.IsNullOrEmpty(snNeedle);

            var list = new List<ArchivedDataFileRow>(256);

            foreach (var dateDir in EnumerateDirectoriesSafe(testDataRoot))
            {
                var dateName = Path.GetFileName(dateDir);
                if (!TryParseDateFolder(dateName, out var folderDate))
                    continue;

                if (fromDate is { } f && folderDate < f)
                    continue;
                if (toDate is { } t && folderDate > t)
                    continue;

                foreach (var snDir in EnumerateDirectoriesSafe(dateDir))
                {
                    var snFolder = Path.GetFileName(snDir);
                    if (snFilterActive &&
                        ZhCompare.IndexOf(snFolder, snNeedle!, CompareOptions.IgnoreCase) < 0)
                        continue;

                    foreach (var runDir in EnumerateDirectoriesSafe(snDir))
                    {
                        var runFolder = Path.GetFileName(runDir);
                        if (!int.TryParse(runFolder, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                            continue;

                        foreach (var file in EnumerateFilesSafe(runDir))
                        {
                            var name = Path.GetFileName(file);
                            if (!TryClassifyFile(name, out var category))
                                continue;
                            if ((catMask & category) == 0)
                                continue;

                            long len = 0;
                            DateTime lw = default;
                            try
                            {
                                var fi = new FileInfo(file);
                                len = fi.Length;
                                lw = fi.LastWriteTimeUtc;
                            }
                            catch
                            {
                                /* 忽略单文件元数据错误 */
                            }

                            list.Add(new ArchivedDataFileRow
                            {
                                FullPath = file,
                                Category = category,
                                CategoryDisplayName = GetDisplayName(category),
                                DateFolder = dateName,
                                SnFolder = snFolder,
                                RunFolder = runFolder,
                                FileName = name,
                                LengthBytes = len,
                                LastWriteTimeUtc = lw
                            });
                        }
                    }
                }
            }

            AppendStandaloneRunLogs(list, criteria, catMask);

            list.Sort(CompareRows);
            return new ArchivedTestDataQueryResult
            {
                ArchiveRoot = root,
                Items = list,
                ErrorMessage = null
            };
        }

        /// <summary>与主程序运行日志工厂默认目录一致：<c>%LocalAppData%\Astra\RunLogs</c>。</summary>
        private static string GetDefaultRunLogsDirectory() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Astra", "RunLogs");

        private void AppendStandaloneRunLogs(
            List<ArchivedDataFileRow> list,
            ArchivedTestDataQueryCriteria criteria,
            ArchivedDataCategoryFlags catMask)
        {
            if ((catMask & ArchivedDataCategoryFlags.RunLog) == 0)
                return;

            var runLogsDir = GetDefaultRunLogsDirectory();
            if (!Directory.Exists(runLogsDir))
                return;

            var fromDate = criteria.FromLocalDateInclusive?.Date;
            var toDate = criteria.ToLocalDateInclusive?.Date;
            var snNeedle = criteria.SnContains?.Trim();
            var snFilterActive = !string.IsNullOrEmpty(snNeedle);

            foreach (var file in EnumerateFilesSafe(runLogsDir))
            {
                if (!file.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                    continue;

                long len;
                DateTime lw;
                try
                {
                    var fi = new FileInfo(file);
                    len = fi.Length;
                    lw = fi.LastWriteTimeUtc;
                }
                catch
                {
                    continue;
                }

                var localDate = lw.ToLocalTime().Date;
                if (fromDate is { } f && localDate < f)
                    continue;
                if (toDate is { } t && localDate > t)
                    continue;

                var name = Path.GetFileName(file);
                if (snFilterActive &&
                    ZhCompare.IndexOf(name, snNeedle!, CompareOptions.IgnoreCase) < 0 &&
                    ZhCompare.IndexOf(file, snNeedle!, CompareOptions.IgnoreCase) < 0)
                    continue;

                list.Add(new ArchivedDataFileRow
                {
                    FullPath = file,
                    Category = ArchivedDataCategoryFlags.RunLog,
                    CategoryDisplayName = GetDisplayName(ArchivedDataCategoryFlags.RunLog),
                    DateFolder = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    SnFolder = "本地日志",
                    RunFolder = "—",
                    FileName = name,
                    LengthBytes = len,
                    LastWriteTimeUtc = lw
                });
            }
        }

        private static int CompareRows(ArchivedDataFileRow a, ArchivedDataFileRow b)
        {
            var dateCmp = string.CompareOrdinal(b.DateFolder, a.DateFolder);
            if (dateCmp != 0) return dateCmp;

            var snCmp = string.Compare(a.SnFolder, b.SnFolder, StringComparison.OrdinalIgnoreCase);
            if (snCmp != 0) return snCmp;

            var runA = int.TryParse(a.RunFolder, out var ra) ? ra : 0;
            var runB = int.TryParse(b.RunFolder, out var rb) ? rb : 0;
            var runCmp = runB.CompareTo(runA);
            if (runCmp != 0) return runCmp;

            var catCmp = CategorySortKey(a.Category).CompareTo(CategorySortKey(b.Category));
            if (catCmp != 0) return catCmp;

            return string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>列表排序：原始 → 音频 → 算法 → 报告 → 运行日志。</summary>
        private static int CategorySortKey(ArchivedDataCategoryFlags c) =>
            c switch
            {
                ArchivedDataCategoryFlags.Raw => 0,
                ArchivedDataCategoryFlags.Audio => 1,
                ArchivedDataCategoryFlags.Algorithm => 2,
                ArchivedDataCategoryFlags.Report => 3,
                ArchivedDataCategoryFlags.RunLog => 4,
                _ => 99
            };

        private static string GetDisplayName(ArchivedDataCategoryFlags c) =>
            c switch
            {
                ArchivedDataCategoryFlags.Raw => "原始数据",
                ArchivedDataCategoryFlags.Audio => "音频数据",
                ArchivedDataCategoryFlags.Algorithm => "算法数据",
                ArchivedDataCategoryFlags.Report => "报告",
                ArchivedDataCategoryFlags.RunLog => "运行日志",
                _ => c.ToString()
            };

        private static bool TryParseDateFolder(string folderName, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(folderName))
                return false;
            return DateTime.TryParseExact(folderName.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date);
        }

        /// <summary>将文件名归为四类之一；不识别的扩展名返回 false。</summary>
        public static bool TryClassifyFile(string fileName, out ArchivedDataCategoryFlags category)
        {
            category = ArchivedDataCategoryFlags.None;
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var lower = fileName.ToLowerInvariant();

            if (lower.EndsWith(".tdms_index", StringComparison.Ordinal))
                return false;

            // 由 Raw 导出拆分的 WAV（与 DefaultWorkflowArchiveService.ExportWavPerChannel 命名一致）
            if (lower.EndsWith(".wav", StringComparison.Ordinal) &&
                lower.Contains("_raw_", StringComparison.Ordinal))
            {
                category = ArchivedDataCategoryFlags.Audio;
                return true;
            }

            if (lower.EndsWith("_raw.tdms", StringComparison.Ordinal))
            {
                category = ArchivedDataCategoryFlags.Raw;
                return true;
            }

            if (lower.EndsWith(".tdms", StringComparison.Ordinal))
            {
                category = ArchivedDataCategoryFlags.Algorithm;
                return true;
            }

            if (lower.EndsWith("_report.html", StringComparison.Ordinal) ||
                lower.EndsWith("_report.pdf", StringComparison.Ordinal))
            {
                category = ArchivedDataCategoryFlags.Report;
                return true;
            }

            if (lower.EndsWith("_run_record.json", StringComparison.Ordinal))
            {
                category = ArchivedDataCategoryFlags.Report;
                return true;
            }

            if (lower.EndsWith(".png", StringComparison.Ordinal) &&
                lower.Contains("_chart_", StringComparison.Ordinal))
            {
                category = ArchivedDataCategoryFlags.Report;
                return true;
            }

            if (lower.EndsWith(".log", StringComparison.Ordinal))
            {
                category = ArchivedDataCategoryFlags.RunLog;
                return true;
            }

            return false;
        }

        private static IEnumerable<string> EnumerateDirectoriesSafe(string path)
        {
            try
            {
                return Directory.EnumerateDirectories(path);
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private static IEnumerable<string> EnumerateFilesSafe(string path)
        {
            try
            {
                return Directory.EnumerateFiles(path);
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }
    }
}
