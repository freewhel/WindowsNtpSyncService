using System;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
namespace WindowsNtpSyncService
{
    public class NtpSyncWindowsService : ServiceBase
    {
        // 同步间隔1小时
        private readonly int _syncInterval = 3600 * 1000;
        // 网络失败重试间隔
        private readonly int _reconnectInterval = 30 * 1000;
        private Task _workTask;
        private CancellationTokenSource _cts;

        // Win32 API 网络检测
        [DllImport("wininet.dll")]
        private extern static bool InternetGetConnectedState(ref int Description, int ReservedValue);

        public NtpSyncWindowsService()
        {
            this.ServiceName = "WindowsNtpSyncService";
        }

        protected override void OnStart(string[] args)
        {
            SimpleLogger.Info("=== NTP时间同步服务 V1.5 已启动 ===");
            SimpleLogger.Info($"同步间隔：{_syncInterval / 3600000}小时");
            SimpleLogger.Info($"NTP服务器：{NtpHelper.NtpServer}");

            _cts = new CancellationTokenSource();
            _workTask = RunServiceLoopAsync(_cts.Token);
        }

        protected override void OnStop()
        {
            SimpleLogger.Info("收到停止信号，准备关闭服务...");
            _cts?.Cancel();
            try
            {
                _workTask?.Wait(5000);
            }
            catch (AggregateException)
            {
            }
            SimpleLogger.Info("=== NTP时间同步服务已停止 ===");
        }

        //【新增】控制台调试模式入口，直接跑业务循环，不走SCM服务
        public async Task RunConsoleModeAsync(CancellationToken token)
        {
            SimpleLogger.Info("===== [控制台模式] NTP同步启动 =====");
            SimpleLogger.Info($"同步间隔：{_syncInterval / 3600000}小时");
            SimpleLogger.Info($"NTP服务器：{NtpHelper.NtpServer}");
            await RunServiceLoopAsync(token);
            SimpleLogger.Info("===== [控制台模式] 退出 =====");
        }

        private async Task RunServiceLoopAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                bool isConnectSuccess = WaitConnect((count) =>
                {
                    SimpleLogger.Info($"正在尝试连接网络（第{count + 1}次）...");
                    return IsConnectInternet();
                });
                if (!isConnectSuccess)
                {
                    SimpleLogger.Error($"网络连接失败，{_reconnectInterval / 1000}s后再次尝试执行...");
                    await Task.Delay(_reconnectInterval, stoppingToken);
                    continue;
                }
                SimpleLogger.Warn("网络连接成功...");

                bool isPingSuccess = WaitConnect((count) =>
                {
                    SimpleLogger.Info($"正在尝试连接NTP服务器[{NtpHelper.NtpServer}]（第{count + 1}次）...");
                    return PingIp(NtpHelper.NtpServer).Result;
                });

                if (!isPingSuccess)
                {
                    SimpleLogger.Error($"NTP服务器[{NtpHelper.NtpServer}]连接失败，等待下一次任务...");
                }
                else
                {
                    SimpleLogger.Info($"NTP服务器[{NtpHelper.NtpServer}]连接成功...");
                    await SyncNtpTimeAsync();
                }

                await Task.Delay(_syncInterval, stoppingToken);
            }
        }

        private async Task<bool> PingIp(string strIP)
        {
            bool bRet = false;
            try
            {
                Ping pingSend = new Ping();
                var reply = await pingSend.SendPingAsync(strIP, 1000);
                if (reply.Status == IPStatus.Success)
                    bRet = true;
            }
            catch (Exception)
            {
                bRet = false;
            }
            return bRet;
        }

        public bool IsConnectInternet()
        {
            int Description = 0;
            return InternetGetConnectedState(ref Description, 0);
        }

        public bool WaitConnect(Func<int, bool> func, int delayTime = 1000 * 30, int maxCount = 10)
        {
            int count = 0;
            do
            {
                bool isConnect = func(count);
                count++;
                if (count > maxCount) return false;
                if (isConnect) return true;
                Task.Delay(delayTime).Wait();
            } while (true);
        }

        /// <summary>
        /// 单次NTP同步逻辑
        /// </summary>
        private async Task SyncNtpTimeAsync()
        {
            SimpleLogger.Info($"开始时间同步：{DateTime.Now:yyyy‑MM‑dd HH:mm:ss}");
            try
            {
                DateTime ntpUtcTime = await NtpHelper.GetNtpUtcTimeAsync();
                DateTime ntpLocalTime = ntpUtcTime.ToLocalTime();
                SimpleLogger.Info($"获取NTP时间成功 - UTC：{ntpUtcTime:yyyy‑MM‑dd HH:mm:ss} | 本地：{ntpLocalTime:yyyy‑MM‑dd HH:mm:ss}");

                DateTime localSystemTime = DateTime.Now;
                TimeSpan timeDiff = (ntpLocalTime - localSystemTime).Duration();
                if (timeDiff.TotalSeconds < 10)
                {
                    SimpleLogger.Info($"系统时间与NTP时间差异较小（{timeDiff.TotalSeconds:F2}秒），无需同步");
                    return;
                }

                NtpHelper.SetWindowsSystemTime(ntpUtcTime);
                SimpleLogger.Info($"时间同步成功！原系统时间：{localSystemTime:yyyy‑MM‑dd HH:mm:ss} | 新系统时间：{ntpLocalTime:yyyy‑MM‑dd HH:mm:ss}");
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                SimpleLogger.Error("网络错误：无法连接NTP服务器（检查网络或服务器地址）", ex);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                SimpleLogger.Error("权限错误：设置系统时间失败（服务需以Local System账户运行）", ex);
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("同步失败：未知错误", ex);
            }
            finally
            {
                SimpleLogger.Info("本次同步流程结束");
            }
        }
    }
}
