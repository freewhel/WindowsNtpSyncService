using System;
using System.IO;

namespace WindowsNtpSyncService
{
    public static class SimpleLogger
    {
        private static readonly string LogRoot = @"C:\Logs\WindowsNtpSyncService";
        private const int RetainDays = 7;
        private static DateTime _lastCleanTime = DateTime.MinValue;

        static SimpleLogger()
        {
            if (!Directory.Exists(LogRoot))
                Directory.CreateDirectory(LogRoot);
        }

        /// <summary>
        /// 清理7天过期日志，24小时最多执行一次
        /// </summary>
        private static void CleanExpiredLogs()
        {
            //距离上次清理不足24小时，直接跳过
            if ((DateTime.Now - _lastCleanTime).TotalHours < 24)
                return;

            try
            {
                DateTime expireTime = DateTime.Now.AddDays(-RetainDays);
                var files = Directory.GetFiles(LogRoot, "log_*.txt", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    FileInfo fi = new FileInfo(file);
                    if (fi.LastWriteTime < expireTime)
                    {
                        fi.Delete();
                    }
                }
            }
            catch
            {
                //忽略清理异常
            }
            finally
            {
                _lastCleanTime = DateTime.Now;
            }
        }

        private static string GetLogFilePath()
        {
            CleanExpiredLogs();
            string fileName = $"log_{DateTime.Now:yyyyMMdd}.txt";
            return Path.Combine(LogRoot, fileName);
        }

        public static void Info(string msg) => WriteLine("INF", msg, null);
        public static void Warn(string msg) => WriteLine("WRN", msg, null);
        public static void Error(string msg, Exception ex = null) => WriteLine("ERR", msg, ex);

        private static void WriteLine(string level, string message, Exception ex)
        {
            try
            {
                string time = DateTime.Now.ToString("yyyy‑MM‑dd HH:mm:ss.fff");
                string line = $"{time} [{level}] {message}";
                if (ex != null)
                {
                    line += Environment.NewLine + ex.ToString();
                }
                File.AppendAllText(GetLogFilePath(), line + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
