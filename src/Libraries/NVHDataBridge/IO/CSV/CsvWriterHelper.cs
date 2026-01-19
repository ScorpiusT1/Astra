using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace NVHDataBridge.IO.CSV
{
    /// <summary>
    /// CSV文件写入器 - 提供高效的大数据CSV文件写入功能
    /// </summary>
    /// <example>
    /// 基本使用:
    /// <code>
    /// using (var writer = new CsvWriterHelper&lt;MyClass&gt;("output.csv"))
    /// {
    ///     writer.WriteRecords(records);
    /// }
    /// </code>
    /// 
    /// 批量写入（大数据）:
    /// <code>
    /// using (var writer = new CsvWriterHelper&lt;MyClass&gt;("large.csv"))
    /// {
    ///     foreach (var batch in recordBatches)
    ///     {
    ///         writer.WriteRecords(batch);
    ///         writer.Flush();
    ///     }
    /// }
    /// </code>
    /// </example>
    public class CsvWriterHelper<T> : IDisposable where T : class
    {
        #region 私有字段

        private readonly StreamWriter _streamWriter;
        private readonly CsvWriter _csvWriter;
        private readonly bool _ownsStream;
        private bool _isDisposed;
        private bool _headerWritten;

        // 缓冲区配置
        private const int DEFAULT_FLUSH_THRESHOLD = 10000; // 默认每10000条记录刷新一次

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建CSV文件写入器
        /// </summary>
        /// <param name="filePath">输出文件路径</param>
        /// <param name="configuration">CSV配置（可选）</param>
        /// <param name="writeHeader">是否写入头部（默认true）</param>
        public CsvWriterHelper(string filePath, CsvConfiguration configuration = null, bool writeHeader = true)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");

            _streamWriter = new StreamWriter(filePath);
            if (configuration == null)
            {
                configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = writeHeader,
                    Delimiter = ",",
                    TrimOptions = TrimOptions.Trim
                };
            }
            _csvWriter = new CsvWriter(_streamWriter, configuration);
            _ownsStream = true;
            _headerWritten = false;
        }

        /// <summary>
        /// 从流创建CSV文件写入器
        /// </summary>
        /// <param name="stream">输出流</param>
        /// <param name="configuration">CSV配置（可选）</param>
        /// <param name="writeHeader">是否写入头部（默认true）</param>
        public CsvWriterHelper(Stream stream, CsvConfiguration configuration = null, bool writeHeader = true)
        {
            _streamWriter = stream == null
                ? throw new ArgumentNullException(nameof(stream), "流不能为空")
                : new StreamWriter(stream);

            if (configuration == null)
            {
                configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = writeHeader,
                    Delimiter = ",",
                    TrimOptions = TrimOptions.Trim
                };
            }
            _csvWriter = new CsvWriter(_streamWriter, configuration);
            _ownsStream = false;
            _headerWritten = false;
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取底层的CsvWriter对象（用于高级操作）
        /// </summary>
        public CsvWriter UnderlyingWriter => _csvWriter;

        /// <summary>
        /// 获取CSV配置
        /// </summary>
        public IWriterConfiguration Configuration => _csvWriter.Context.Writer.Configuration;

        #endregion

        #region 写入方法

        /// <summary>
        /// 写入单条记录
        /// </summary>
        /// <param name="record">要写入的记录</param>
        public void WriteRecord(T record)
        {
            ThrowIfDisposed();

            if (record == null)
                throw new ArgumentNullException(nameof(record));

            EnsureHeaderWritten();
            _csvWriter.WriteRecord(record);
            _csvWriter.NextRecord();
        }

        /// <summary>
        /// 写入多条记录
        /// </summary>
        /// <param name="records">要写入的记录集合</param>
        public void WriteRecords(IEnumerable<T> records)
        {
            ThrowIfDisposed();

            if (records == null)
                throw new ArgumentNullException(nameof(records));

            EnsureHeaderWritten();
            _csvWriter.WriteRecords(records);
        }

        /// <summary>
        /// 批量写入记录（高效处理大数据，自动刷新）
        /// </summary>
        /// <param name="records">要写入的记录集合</param>
        /// <param name="flushThreshold">刷新阈值（每N条记录刷新一次，默认10000）</param>
        public void WriteRecordsBatch(IEnumerable<T> records, int flushThreshold = DEFAULT_FLUSH_THRESHOLD)
        {
            ThrowIfDisposed();

            if (records == null)
                throw new ArgumentNullException(nameof(records));

            EnsureHeaderWritten();

            int count = 0;
            foreach (var record in records)
            {
                _csvWriter.WriteRecord(record);
                _csvWriter.NextRecord();
                count++;

                // 定期刷新以降低内存占用
                if (count % flushThreshold == 0)
                {
                    _csvWriter.Flush();
                }
            }

            // 最后刷新
            _csvWriter.Flush();
        }

        /// <summary>
        /// 写入字段值
        /// </summary>
        /// <param name="value">字段值</param>
        public void WriteField(object value)
        {
            ThrowIfDisposed();
            _csvWriter.WriteField(value);
        }

        /// <summary>
        /// 写入字段值
        /// </summary>
        /// <typeparam name="TField">字段类型</typeparam>
        /// <param name="value">字段值</param>
        public void WriteField<TField>(TField value)
        {
            ThrowIfDisposed();
            _csvWriter.WriteField(value);
        }

        /// <summary>
        /// 写入下一行
        /// </summary>
        public void NextRecord()
        {
            ThrowIfDisposed();
            _csvWriter.NextRecord();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 确保头部已写入
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureHeaderWritten()
        {
            if (!_headerWritten && Configuration.HasHeaderRecord)
            {
                _csvWriter.WriteHeader<T>();
                _csvWriter.NextRecord();
                _headerWritten = true;
            }
        }

        /// <summary>
        /// 刷新写入缓冲区
        /// </summary>
        public void Flush()
        {
            ThrowIfDisposed();
            _csvWriter.Flush();
            _streamWriter?.Flush();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CsvWriterHelper<T>));
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _csvWriter?.Flush();
            _csvWriter?.Dispose();
            if (_ownsStream)
            {
                _streamWriter?.Flush();
                _streamWriter?.Dispose();
            }
            _isDisposed = true;
        }

        #endregion
    }
}

