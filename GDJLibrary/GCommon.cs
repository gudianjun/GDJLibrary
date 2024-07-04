using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using NLog;
using OfficeOpenXml;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Formats.Asn1;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Ude;

namespace GDJLibrary
{
    public class GCommon
    {
        #region LOG
        private static readonly NLog.Logger logger = LogManager.GetCurrentClassLogger();

        public static void Debug(string message)
        {
            logger.Debug(message);
        }

        public static void Info(string message)
        {
            logger.Info(message);
        }

        public static void Error(string message)
        {
            logger.Error(message);
        }
        #endregion
        public static string CurPath { get { return AppDomain.CurrentDomain.BaseDirectory; } }

        public static T? LoadObjectFromJson<T>(string fileName) where T : class, new()
        {
            string json = File.ReadAllText(fileName, Encoding.UTF8);
            return JsonConvert.DeserializeObject<T>(json);
        }
        public static string GetObjectJsonString(object obj)
        {
            string json = JsonConvert.SerializeObject(obj, Formatting.Indented);
            return json;
        }
        public static void SaveObjectToJson(object obj, string fileName)
        {
            FileInfo fileInfo = new FileInfo(fileName);
            if (!fileInfo.Exists)
            {
                Directory.CreateDirectory(fileInfo.DirectoryName!);
                string json = JsonConvert.SerializeObject(obj, Formatting.Indented);
                File.WriteAllText(fileName, json, Encoding.UTF8);
            }
        }

        public static T? CopyObject<T>(T obj) where T : class, new()
        {
            try
            {
                string json = JsonConvert.SerializeObject(obj, Formatting.Indented);
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch { return null; }
        }
        public static bool EqualsObject<T>(T obj1, T obj2) where T : class, new()
        {
            try
            {
                string json1 = JsonConvert.SerializeObject(obj1, Formatting.Indented);
                string json2 = JsonConvert.SerializeObject(obj2, Formatting.Indented);
                return json1.Equals(json2);
            }
            catch { return false; }
        }
        /// <summary>
        /// 必须有头的CSV文件读取
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static async Task<List<Dictionary<string, object>>> ReadCsv(string fileName)
        {
            var records = new List<Dictionary<string, object>>();
            using (var reader = new StreamReader(fileName))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = null
            }))
            {
                csv.Read();
                csv.ReadHeader();
                while (await csv.ReadAsync())
                {
                    var record = new Dictionary<string, object>();
                    foreach (var header in csv.HeaderRecord)
                    {
                        record[header] = csv.GetField(header);
                    }
                    records.Add(record);
                }
            }

            return records;
        }
        static void MissingFieldFound(MissingFieldFoundArgs args)
        {
            
        }
        static void csvHelper_BadDataFound(BadDataFoundArgs args)
        {
            // 获取问题字段的索引
            string fieldIndex = args.Field;

            // 获取问题记录的原始数据
            string  rawRecord = args.RawRecord;

            // 处理错误
            // ...

            
            // ...
        }
        public static void ReadCsv(string fileName, Func<Dictionary<string, object>, bool> rowDispFunc )
        {
            using (var reader = new StreamReader(fileName))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = MissingFieldFound,
                BadDataFound = csvHelper_BadDataFound,
            }))
            {
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var record = new Dictionary<string, object>();
                    try
                    {
                        //for(int i = 0;i<csv.HeaderRecord.Count();i++)
                        //{
                        //    record[csv.HeaderRecord[i]] = csv.GetField(i);
                        //}

                        foreach (var header in csv.HeaderRecord)
                        {
                            record[header] = csv.GetField(header);
                        }
                    }
                    catch(Exception ex)
                    {
                        logger.Error(ex); 
                    }
                    if(csv.ColumnCount != csv.HeaderRecord.Count())
                    {
                        GCommon.Error($"CSV:{(new FileInfo(fileName)).Name}, Row:{csv.Parser.RawRow}, RawRecord：{csv.Parser.RawRecord}, 読みない！");
                    }
                    else
                    {
                        if (!rowDispFunc(record))
                        {
                            break;
                        }
                    } 
                }
            } 
        }
        
        public static string IsUTF8(string filePath)
        {
            using (FileStream fs = File.OpenRead(filePath))
            {
                CharsetDetector cdet = new CharsetDetector();
                cdet.Feed(fs);
                cdet.DataEnd();

                return cdet.Charset;
            }
           
        }
        public static bool IsUtf8(string filePath)
        {
            var c = IsUTF8(filePath);
            byte[] buffer = new byte[10000];
            try
            { 
                int nReadedLen = 0; 
                using(FileStream fs = File.OpenRead(filePath))
                {
                    nReadedLen = fs.Read(buffer, 0, 5000);
 
                    if(fs.Length > 5000)
                    {
                        while(true)
                        {
                            byte by = (byte)fs.ReadByte();
                            if((int)by != -1)
                            {
                                if(by == '\r' || by == '\n')
                                {
                                    break;
                                }
                                buffer[nReadedLen++] = by;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }

                string content = Encoding.UTF8.GetString(buffer, 0, nReadedLen); 
                byte[] checkBuffer = Encoding.UTF8.GetBytes(content);
  
                bool equal = nReadedLen == checkBuffer.Length;
                return equal;
            }
            catch(Exception ex)
            {
                Error(ex.Message ); 
                return true;
            }
        }
        public static void ClearCurrentConsoleLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, currentLineCursor);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }
        public static void ReadCsvHeader(string fileName, Action<List<string>> rowDispFunc)
        {
            using (var reader = new StreamReader(fileName))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = null
            }))
            {
                csv.Read();
                csv.ReadHeader();
                rowDispFunc(csv.HeaderRecord.ToList());
            }
        }
        /// <summary>
        /// 读取嵌入的资源, 默认为exe的assembly
        /// </summary>
        /// <param name="fullName">资源为全路径名</param>
        /// <param name="action">资源流，</param>
        /// <returns></returns>
        public static bool LoadEmbedded(string fullName, Action<Stream> action)
        {
            try
            {
                var assembly = Assembly.GetEntryAssembly();
                Stream? resStream = assembly!.GetManifestResourceStream(fullName);
                if (resStream != null)
                {
                    action(resStream);
                    resStream.Close();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Error(ex.Message);
            }
            return false;
        }/// <summary>
         /// 读取嵌入的资源, 默认为exe的assembly
         /// </summary>
         /// <param name="fullName">资源为全路径名</param>
         /// <param name="action">资源流，</param>
         /// <returns></returns>
        public static bool LoadEmbedded(Assembly assembly, string fullName, Action<Stream> action)
        {
            try
            {
                Stream? resStream = assembly!.GetManifestResourceStream(fullName);
                if (resStream != null)
                {
                    action(resStream);
                    resStream.Close();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Error(ex.Message);
            }
            return false;
        }
        /// <summary>
        /// 检查一个字符串是否全为日语
        /// </summary>
        /// <param name="inputStr"></param>
        /// <param name="errorStr"></param>
        /// <returns></returns>
        public static bool CheckJpnString(string inputStr, out string errorStr)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                StringBuilder stringBuilder = new StringBuilder();
                foreach (var s in inputStr)
                {
                    string input = s.ToString();


                    byte[] bytesUtf8 = Encoding.UTF8.GetBytes(input);

                    byte[] shiftJisBytes = Encoding.Convert(Encoding.UTF8, Encoding.GetEncoding(932), bytesUtf8);
                    if (bytesUtf8.Length > 1 && shiftJisBytes.Length == 1 && shiftJisBytes[0] == 63)
                    {
                        stringBuilder.AppendLine(input);
                    }
                    else
                    {
                        bool bl = IsJapaneseByte(shiftJisBytes);
                        if (!bl)
                        {
                            stringBuilder.Append(input + ",");
                        }
                    }

                }
                errorStr = stringBuilder.ToString();
                return errorStr.Length == 0 ? true : false;
            }
            catch(Exception ex)
            {
                Error(ex.Message);
                errorStr = ex.Message;
            }
            return false;
        }
        /// <summary>
        /// 检查字节是否为日语字符
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static bool IsJapaneseByte(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                byte bt = bytes[i];
                if ((bt >= 0x00 & bt <= 0x1f) || bt == 0x7f)
                {
                    continue;
                }
                else if ((bt >= 0x20 & bt <= 0x7e) || (bt >= 0xa1 & bt <= 0xdf))
                {
                    continue;
                }
                else if ((bt >= 0x81 & bt <= 0x9f) || (bt >= 0xe0 & bt <= 0xef) && i + 1 < bytes.Length)
                {
                    byte btNex = bytes[i + 1];
                    if ((btNex >= 0x40 & btNex <= 0x7e) || (btNex >= 0x80 & btNex <= 0xfc))
                    {
                        i++;
                        continue;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
        /// <summary>
        /// 读取一个文件名指定的Excel文件，需要保存的时候需要回调中调用Save
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="action"></param>
        public static void StartExcel(string fileName, Action<OfficeOpenXml.ExcelWorkbook> action)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            if (!string.IsNullOrEmpty(fileName))
            {
                try
                {
                    using (ExcelPackage excel = new ExcelPackage(fileName))
                    {
                        ExcelWorkbook workbook = excel.Workbook;
                        try
                        {
                            if (action != null)
                            {
                                action(workbook);
                            }
                        }
                        catch (Exception ex)
                        {
                            Error("StartExcel:" + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Error("StartExcel:" + ex.Message);
                }
            }
        }
        /// <summary>
        /// 读取一个Excel文件流，需要保存的时候需要回调中调用Save
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="action"></param>
        public static void StartExcel(Stream stream, Action<OfficeOpenXml.ExcelWorkbook> action)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            try
            {
                using (ExcelPackage excel = new ExcelPackage(stream))
                {
                    ExcelWorkbook workbook = excel.Workbook;
                    try
                    {
                        if (action != null)
                        {
                            action(workbook);
                        }
                    }
                    catch (Exception ex)
                    {
                        Error("StartExcel:" + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Error("StartExcel:" + ex.Message);
            }
        }
        /// <summary>
        /// 输出字符串的Base64文本
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        /// <summary>
        /// 运行一个命令行命令
        /// </summary>
        public static string StartCmd()
        {
            Process CmdProcess = new Process();
            CmdProcess.StartInfo.FileName = "cmd.exe";
            CmdProcess.StartInfo.CreateNoWindow = false;         // 不创建新窗口    
            CmdProcess.StartInfo.UseShellExecute = false;       //不启用shell启动进程  
            CmdProcess.StartInfo.RedirectStandardInput = true;  // 重定向输入    
            CmdProcess.StartInfo.RedirectStandardOutput = true; // 重定向标准输出    
            CmdProcess.StartInfo.RedirectStandardError = true;  // 重定向错误输出  

            CmdProcess.StandardInput.WriteLine("dir" + "&exit"); //向cmd窗口发送输入信息  
            CmdProcess.StandardInput.AutoFlush = true;  //提交  
            CmdProcess.Start();//执行   
            string rtnStr = CmdProcess.StandardOutput.ReadToEnd();//输出   
            CmdProcess.WaitForExit();//等待程序执行完退出进程   
            CmdProcess.Close();//结束  
            return rtnStr;
        }
        /// <summary>
        /// 运行一个exe文件
        /// </summary>
        /// <param name="exeFileName"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static int StartExe(string exeFileName, string arguments)
        {
            Process myProcess = new Process();
            string fileName = exeFileName;
            ProcessStartInfo myProcessStartInfo = new ProcessStartInfo(fileName, arguments);
            myProcess.StartInfo = myProcessStartInfo;
            myProcess.StartInfo.CreateNoWindow = true;
            myProcess.StartInfo.UseShellExecute = false;
            myProcess.StartInfo.RedirectStandardOutput = true;
            bool isStart = myProcess.Start();
            while (!myProcess.HasExited)
            {
                myProcess.WaitForExit();
            }
            int returnValue = myProcess.ExitCode;
            return returnValue;
        }
        /// <summary>
        /// 用一个默认程序打开一个指定文件
        /// </summary>
        /// <param name="fileName"></param>
        public static void ShellOpenFile(string fileName)
        {
            using (Process fileopener = new Process())
            {
                fileopener.StartInfo.FileName = "explorer";
                fileopener.StartInfo.Arguments = fileName;
                fileopener.Start();
            }
        }

        /// <summary>
        /// MD5加密一个字符串
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string MD5EncryptString(string str)
        {
            MD5 md5 = MD5.Create(); 
            byte[] byteOld = Encoding.UTF8.GetBytes(str); 
            byte[] byteNew = md5.ComputeHash(byteOld); 
            StringBuilder sb = new StringBuilder();
            foreach (byte b in byteNew)
            { 
                sb.Append(b.ToString("x2"));
            } 
            return sb.ToString();
        }

        /// <summary>
        /// 压缩一个路径下的文件到指定zip包中
        /// </summary>
        /// <param name="path"></param>
        /// <param name="zipFileName"></param>
        public static void CreateZipFromDirectory(string path, string zipFileName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Error("The path cannot be null or empty." );
            }

            if (string.IsNullOrWhiteSpace(zipFileName))
            {
                Error("The zip file name cannot be null or empty." );
            }

            ZipFile.CreateFromDirectory(path, zipFileName);

        }
        /// <summary>
        /// 解压一个zip包到指定路径中
        /// </summary>
        /// <param name="zipFileName"></param>
        /// <param name="outPath"></param>
        public static void ExtractZipToDirectory(string zipFileName, string outPath)
        {
            if (string.IsNullOrWhiteSpace(zipFileName))
            {
                Error("The zip file name cannot be null or empty." );
            }

            if (string.IsNullOrWhiteSpace(outPath))
            {
                Error("The output path cannot be null or empty." );
            }

            ZipFile.ExtractToDirectory(zipFileName, outPath);
        }

        /// <summary>
        /// 获取列类型对应关系
        /// </summary>
        /// <param name="columnType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static SqlDbType GetSqlDbType(string columnType)
        {
            int index = columnType.IndexOf('(');
            if (index >= 0)
            {
                columnType = columnType.Substring(0, index);
            }
            switch (columnType.ToLower())
            {
                case "bigint":
                    return SqlDbType.BigInt;
                case "binary":
                    return SqlDbType.Binary;
                case "bit":
                    return SqlDbType.Bit;
                case "char":
                    return SqlDbType.Char;
                case "date":
                    return SqlDbType.Date;
                case "datetime":
                    return SqlDbType.DateTime;
                case "datetime2":
                    return SqlDbType.DateTime2;
                case "datetimeoffset":
                    return SqlDbType.DateTimeOffset;
                case "decimal":
                    return SqlDbType.Decimal;
                case "float":
                    return SqlDbType.Float;
                case "image":
                    return SqlDbType.Image;
                case "int":
                    return SqlDbType.Int;
                case "money":
                    return SqlDbType.Money;
                case "nchar":
                    return SqlDbType.NChar;
                case "ntext":
                    return SqlDbType.NText;
                case "nvarchar":
                    return SqlDbType.NVarChar;
                case "real":
                    return SqlDbType.Real;
                case "smalldatetime":
                    return SqlDbType.SmallDateTime;
                case "smallint":
                    return SqlDbType.SmallInt;
                case "smallmoney":
                    return SqlDbType.SmallMoney;
                case "text":
                    return SqlDbType.Text;
                case "time":
                    return SqlDbType.Time;
                case "timestamp":
                    return SqlDbType.Timestamp;
                case "tinyint":
                    return SqlDbType.TinyInt;
                case "uniqueidentifier":
                    return SqlDbType.UniqueIdentifier;
                case "varbinary":
                    return SqlDbType.VarBinary;
                case "varchar":
                    return SqlDbType.VarChar;
                case "xml":
                    return SqlDbType.Xml;
                default:
                    GCommon.Error($"Unsupported column type: {columnType}");
                    return SqlDbType.NVarChar;
                    //throw new ArgumentException($"Unsupported column type: {columnType}");
            }
        }
        public static byte[] HexStringToByteArray(string hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }
        #region DB Type Check
        public static bool IsValidTimestamp(string value)
        {
            DateTime tempDate;
            return DateTime.TryParse(value, out tempDate);
        }

        public static bool IsValidChar(string value)
        {
            return !string.IsNullOrEmpty(value.Trim());
        }

        public static bool IsValidClob(string value)
        {
            return !string.IsNullOrEmpty(value.Trim());
        }

        public static bool IsValidDate(string value)
        {
            DateTime tempDate;
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out tempDate);
        }

        public static bool IsValidBlob(string value)
        {
            return !string.IsNullOrEmpty(value.Trim()) && IsBase64String(value);
        }

        public static bool IsValidRaw(string value)
        {
            return !string.IsNullOrEmpty(value.Trim()) && IsHexString(value);
        }

        public static bool IsBase64String(string value)
        {
            try
            {
                Convert.FromBase64String(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsHexString(string value)
        {
            foreach (char c in value)
            {
                if (!Uri.IsHexDigit(c))
                    return false;
            }
            return true;
        }
        public static bool IsValidNumber(string value, int? maxDigits = null, int? maxDecimals = null)
        {
            if(value.StartsWith("-"))
            {
                value = value.Substring(1);
            }
            Regex regex = new Regex(@"^[0-9]+(\.[0-9]*)?$");
            if(regex.IsMatch(value))
            {  
                string[] parts = value.Split('.');
                int integerPartLength = parts[0].Length;
                int decimalPartLength = parts.Length > 1 ? parts[1].Length : 0;

                if (maxDigits.HasValue && integerPartLength > maxDigits.Value)
                    return false;

                if (maxDecimals.HasValue && decimalPartLength > maxDecimals.Value)
                    return false;
                return true;
            }
            return false;

        }
        #endregion
    }

}
