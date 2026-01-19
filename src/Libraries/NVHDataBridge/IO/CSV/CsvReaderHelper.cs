using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NVHDataBridge.IO.CSV
{
    /// <summary>
    /// CSV文件读取器 - 提供高效的大数据CSV文件读取功能
    /// </summary>
    /// <example>
    /// 基本使用:
    /// <code>
    /// using (var reader = CsvReaderHelper.Open&lt;MyClass&gt;("data.csv"))
    /// {
    ///     var records = reader.ReadAll();
    /// }
    /// </code>
    /// 
    /// 流式读取（大数据）:
    /// <code>
    /// using (var reader = CsvReaderHelper.Open&lt;MyClass&gt;("large.csv"))
    /// {
    ///     foreach (var record in reader.ReadStreaming())
    ///     {
    ///         // 处理每条记录
    ///     }
    /// }
    /// </code>
    /// </example>
    public class CsvReaderHelper<T> : IDisposable where T : class
    {
        #region 私有字段

        private readonly StreamReader _streamReader;
        private readonly CsvReader _csvReader;
        private readonly bool _ownsStream;
        private bool _isDisposed;

        #endregion

        #region 构造函数

        /// <summary>
        /// 从文件路径创建CSV读取器
        /// </summary>
        /// <param name="filePath">CSV文件路径</param>
        /// <param name="configuration">CSV配置（可选）</param>
        internal CsvReaderHelper(string filePath, CsvConfiguration configuration = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"CSV文件不存在: {filePath}", filePath);

            _streamReader = new StreamReader(filePath);
            if (configuration == null)
            {
                configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    Delimiter = ",",
                    TrimOptions = TrimOptions.Trim
                };
            }
            _csvReader = new CsvReader(_streamReader, configuration);
            _ownsStream = true;
        }

        /// <summary>
        /// 从流创建CSV读取器
        /// </summary>
        /// <param name="stream">包含CSV数据的流</param>
        /// <param name="configuration">CSV配置（可选）</param>
        internal CsvReaderHelper(Stream stream, CsvConfiguration configuration = null)
        {
            _streamReader = stream == null
                ? throw new ArgumentNullException(nameof(stream), "流不能为空")
                : new StreamReader(stream);
            
            if (configuration == null)
            {
                configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    Delimiter = ",",
                    TrimOptions = TrimOptions.Trim
                };
            }
            _csvReader = new CsvReader(_streamReader, configuration);
            _ownsStream = false;
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取底层的CsvReader对象（用于高级操作）
        /// </summary>
        public CsvReader UnderlyingReader => _csvReader;

        /// <summary>
        /// 获取CSV配置
        /// </summary>
        public IReaderConfiguration Configuration => _csvReader.Context.Reader.Configuration;

        #endregion

        #region 读取方法

        /// <summary>
        /// 读取所有记录（注意：对于大文件，建议使用流式读取）
        /// </summary>
        /// <returns>所有记录的列表</returns>
        public List<T> ReadAll()
        {
            ThrowIfDisposed();
            return _csvReader.GetRecords<T>().ToList();
        }

        /// <summary>
        /// 流式读取记录（高效处理大文件）
        /// </summary>
        /// <returns>记录的可枚举集合</returns>
        public IEnumerable<T> ReadStreaming()
        {
            ThrowIfDisposed();
            return _csvReader.GetRecords<T>();
        }

        /// <summary>
        /// 读取指定数量的记录
        /// </summary>
        /// <param name="count">要读取的记录数</param>
        /// <returns>记录的列表</returns>
        public List<T> Read(int count)
        {
            ThrowIfDisposed();
            if (count <= 0)
                throw new ArgumentException("读取数量必须大于0", nameof(count));

            var records = new List<T>(count);
            int readCount = 0;

            foreach (var record in _csvReader.GetRecords<T>())
            {
                records.Add(record);
                readCount++;
                if (readCount >= count)
                    break;
            }

            return records;
        }

        /// <summary>
        /// 尝试读取下一条记录
        /// </summary>
        /// <param name="record">输出参数：读取的记录</param>
        /// <returns>如果成功读取返回true，否则返回false</returns>
        public bool TryRead(out T record)
        {
            ThrowIfDisposed();

            if (_csvReader.Read())
            {
                record = _csvReader.GetRecord<T>();
                return record != null;
            }

            record = null;
            return false;
        }

        /// <summary>
        /// 读取头部记录
        /// </summary>
        public void ReadHeader()
        {
            ThrowIfDisposed();
            _csvReader.ReadHeader();
        }

        /// <summary>
        /// 获取指定字段的值
        /// </summary>
        /// <typeparam name="TField">字段类型</typeparam>
        /// <param name="index">字段索引</param>
        /// <returns>字段值</returns>
        public TField GetField<TField>(int index)
        {
            ThrowIfDisposed();
            return _csvReader.GetField<TField>(index);
        }

        /// <summary>
        /// 获取指定字段的值
        /// </summary>
        /// <typeparam name="TField">字段类型</typeparam>
        /// <param name="name">字段名称</param>
        /// <returns>字段值</returns>
        public TField GetField<TField>(string name)
        {
            ThrowIfDisposed();
            return _csvReader.GetField<TField>(name);
        }

        #endregion

        #region 辅助方法

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CsvReaderHelper<T>));
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

            _csvReader?.Dispose();
            if (_ownsStream)
            {
                _streamReader?.Dispose();
            }
            _isDisposed = true;
        }

        #endregion
    }

    /// <summary>
    /// CSV读取器静态工厂类
    /// </summary>
    public static class CsvReaderHelper
    {
        /// <summary>
        /// 打开CSV文件并返回读取器实例
        /// </summary>
        /// <typeparam name="TRecord">记录类型</typeparam>
        /// <param name="filePath">CSV文件路径</param>
        /// <param name="configuration">CSV配置（可选）</param>
        /// <returns>已打开的CsvReaderHelper实例</returns>
        public static CsvReaderHelper<TRecord> Open<TRecord>(string filePath, CsvConfiguration configuration = null) where TRecord : class
        {
            return new CsvReaderHelper<TRecord>(filePath, configuration);
        }

        /// <summary>
        /// 从流打开CSV文件并返回读取器实例
        /// </summary>
        /// <typeparam name="TRecord">记录类型</typeparam>
        /// <param name="stream">包含CSV数据的流</param>
        /// <param name="configuration">CSV配置（可选）</param>
        /// <returns>已打开的CsvReaderHelper实例</returns>
        public static CsvReaderHelper<TRecord> Open<TRecord>(Stream stream, CsvConfiguration configuration = null) where TRecord : class
        {
            return new CsvReaderHelper<TRecord>(stream, configuration);
        }
    }
}
