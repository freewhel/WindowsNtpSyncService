using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsNtpSyncService
{
    internal static class Program
    {
        static void Main()
        {
            try
            {
                var service = new NtpSyncWindowsService();

                // 判断运行模式：交互（手动运行exe） / 非交互（WindowsSCM启动服务）
                if (Environment.UserInteractive)
                {
                    // =====控制台调试模式=====
                    var cts = new CancellationTokenSource();
                    //监听Ctrl+C
                    Console.CancelKeyPress += (s, e) =>
                    {
                        Console.WriteLine("收到Ctrl+C，准备退出");
                        e.Cancel = true;
                        cts.Cancel();
                    };
                    //执行业务循环
                    service.RunConsoleModeAsync(cts.Token).Wait();
                }
                else
                {
                    // =====正式Windows服务模式（SCM调用）=====
                    ServiceBase.Run(service);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("程序异常", ex);
                //调试模式下把异常打印控制台
                if (Environment.UserInteractive)
                {
                    Console.WriteLine(ex);
                    Console.WriteLine("按任意键退出...");
                    Console.ReadKey();
                }
            }
        }
    }
}
