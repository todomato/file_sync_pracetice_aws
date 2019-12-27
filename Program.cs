using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    class Program
    {
        private static readonly string _watchPath = ConfigurationManager.AppSettings["WatchFile"];
        private static readonly string _awsPath = ConfigurationManager.AppSettings["AWSFile"];
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static string _filename;

        static void Main(string[] args)
        {
            Console.WriteLine("啟動監控同步資料夾..請勿關閉");

            FileSystemWatcher watcher = new FileSystemWatcher();
            // 監控位置
            watcher.Path = _watchPath;
            // 監控觸發改變的狀態
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            // 過濾檔案
            watcher.Filter = "*.*";
            // 是否監控子檔案
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
            //watcher.Changed += new FileSystemEventHandler(OnChanged);
            watcher.Created += new FileSystemEventHandler(OnCreated);
            watcher.Deleted += new FileSystemEventHandler(OnRemoved);
            watcher.Renamed += new RenamedEventHandler(OnRenamed);

            new System.Threading.AutoResetEvent(false).WaitOne();
        }

        // 新增
        private static void OnCreated(object sender, FileSystemEventArgs e)
        {
            Console.Clear();

            //上傳ref :https://aws.amazon.com/tw/getting-started/tutorials/backup-to-s3-cli/
            var sourcePath = $@"{_watchPath}/{e.Name}";
            string targetPath = GetTargetPath(e);
            var fullName = GetTargetFullName(e);
            var cmdStr = $@"aws s3 cp ""{ sourcePath }"" {targetPath}";
            _filename = Path.GetFileName(fullName);
            //aws s3 cp "D:\test11/PDF\191108-34-0017.png" s3://tci-qr-web-video-jp/Demo/PDF/

            WriteLog($"準備新增_{fullName}");
            ExecuteCommand(cmdStr);
            WriteLog($"新增成功_{fullName}");
            Console.WriteLine($"{e.Name} + 新增完成!!");
            Console.WriteLine($"啟動監控同步資料夾..請勿關閉");
        }

        // 刪除
        private static void OnRemoved(object sender, FileSystemEventArgs e)
        {
            var fullName = GetTargetFullName(e);
            _filename = Path.GetFileName(fullName);
            var cmdStr = $@"aws s3 rm ""{fullName}""";
            WriteLog($"準備刪除_{fullName}");
            ExecuteCommand(cmdStr);
            WriteLog($"刪除成功_{fullName}");
            Console.WriteLine($"{e.Name} + 刪除完成!");
            Console.WriteLine($" ");
        }

        // 更名
        private static void OnRenamed(object sender, FileSystemEventArgs e)
        {
            var sourcePath = $@"{_watchPath}/{e.Name}";
            string targetPath = GetTargetPath(e);
            var fullName = GetTargetFullName(e);
            _filename = Path.GetFileName(fullName);

            var cmdStr = $@"aws s3 rm ""{fullName}"" && aws s3 cp ""{ sourcePath }"" {targetPath}";
            WriteLog($"準備改名_{fullName}");
            ExecuteCommand(cmdStr);
            WriteLog($"改名成功_{fullName}");
            Console.WriteLine($"{e.Name} + 已改名");
            Console.WriteLine($" ");
        }

        // 更新
        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            var sourcePath = $@"{_watchPath}/{e.Name}";
            string targetPath = GetTargetPath(e);
            var fullName = GetTargetFullName(e);
            _filename = Path.GetFileName(fullName);

            var cmdStr = $@"aws s3 rm ""{fullName}"" && aws s3 cp ""{ sourcePath }"" {targetPath}";
            WriteLog($"準備更新_{fullName}");
            ExecuteCommand(cmdStr);
            WriteLog($"更新成功_{fullName}");
            Console.WriteLine($"{e.Name} + 已更新");
            Console.WriteLine($" ");
        }

        // 取得aws路徑 for cmd用
        private static string GetTargetPath(FileSystemEventArgs e)
        {
            // ex. s3://tci-qr-web-video-jp/Demo/PDF/
            var result = Path.GetDirectoryName($"{ _awsPath }/{ e.Name}");
            return result.Replace('\\', '/').Replace("s3:/", "s3://") + "/";
        }

        // 取得aws路徑 for cmd用
        private static string GetTargetFullName(FileSystemEventArgs e)
        {
            // ex. s3://tci-qr-web-video-jp/Demo/PDF/
            var raw = $"{ _awsPath }/{ e.Name}";
            var path = Path.GetDirectoryName(raw);
            path = path.Replace('\\', '/').Replace("s3:/", "s3://") + "/";
            var filename = Path.GetFileName(raw);
            return $"{path}{filename}";
        }

        // 執行command
        static void ExecuteCommand(string command)
        {
            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;

            var process = Process.Start(processInfo);

            process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                Console.WriteLine("output>>" + e.Data);
                if (!string.IsNullOrEmpty(e.Data) && e.Data.Contains("upload"))
                {
                    WriteLog("output>>" + e.Data);
                }
            };

            process.BeginOutputReadLine();

            process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                Console.WriteLine("error>>" + e.Data);
                if (!string.IsNullOrEmpty(e.Data))
                {
                    WriteError("error>>" + e.Data);
                }
            };
            process.BeginErrorReadLine();

            process.WaitForExit();

            Console.WriteLine("ExitCode: {0}", process.ExitCode);
            process.Close();
        }

        // log
        private static void WriteLog(string msg)
        {
            LogEventInfo info = new LogEventInfo(NLog.LogLevel.Trace, "", "");
            info.Properties["Name"] = $"{_filename}";
            info.Properties["msg"] = msg;
            logger.Log(info);
        }

        // log error
        private static void WriteError(string msg)
        {
            LogEventInfo info = new LogEventInfo(NLog.LogLevel.Debug, "", "");
            info.Properties["Name"] = $"{_filename}";
            info.Properties["msg"] = msg;
            logger.Log(info);
        }
    }
}
