using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WindowsNtpSyncService
{
    /// <summary>
    /// NTP 时间同步工具类（仅Windows，纯原生无第三方包）
    /// </summary>
    public static class NtpHelper
    {
        // NTP服务器（国内推荐：ntp.aliyun.com、ntp.tencent.com）
        public static string NtpServer { get; set; } = "ntp.aliyun.com";
        // NTP协议端口
        private const int NtpPort = 123;
        // NTP时间戳与Unix时间戳的偏移秒数（1900‑01‑01 到 1970‑01‑01）
        private const long NtpEpochToUnixEpoch = 2208988800L;

        /// <summary>
        /// 从NTP服务器获取UTC时间
        /// </summary>
        public static async Task<DateTime> GetNtpUtcTimeAsync()
        {
            byte[] ntpRequest = new byte[48];
            ntpRequest[0] = 0x1B; // 00011011 LI=0, VN=3, Mode=3 客户端模式
            var udpClient = new UdpClient();
            udpClient.Client.ReceiveTimeout = 5000;
            udpClient.Client.SendTimeout = 5000;
            try
            {
                IPAddress[] ipAddresses = await Dns.GetHostAddressesAsync(NtpServer);
                if (ipAddresses.Length == 0)
                    throw new InvalidOperationException("无法解析NTP服务器地址");

                IPEndPoint ntpEndPoint = new IPEndPoint(ipAddresses[0], NtpPort);
                await udpClient.SendAsync(ntpRequest, ntpRequest.Length, ntpEndPoint);
                UdpReceiveResult result = await udpClient.ReceiveAsync();
                byte[] ntpResponse = result.Buffer;

                if (ntpResponse.Length != 48)
                    throw new InvalidDataException("NTP响应数据异常");

                ulong seconds = BitConverter.ToUInt32(ntpResponse, 40);
                ulong fractional = BitConverter.ToUInt32(ntpResponse, 44);
                seconds = SwapEndianness(seconds);
                fractional = SwapEndianness(fractional);

                double totalSeconds = seconds + (double)fractional / 0x100000000L;

                DateTime utcTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddSeconds(totalSeconds - NtpEpochToUnixEpoch);
                return utcTime;
            }
            finally
            {
                udpClient.Close();
            }
        }

        /// <summary>
        /// 设置Windows系统时间（需管理员/System权限，传入UTC时间）
        /// </summary>
        public static bool SetWindowsSystemTime(DateTime utcTime)
        {
            SYSTEMTIME systemTime = new SYSTEMTIME
            {
                wYear = (ushort)utcTime.Year,
                wMonth = (ushort)utcTime.Month,
                wDay = (ushort)utcTime.Day,
                wHour = (ushort)utcTime.Hour,
                wMinute = (ushort)utcTime.Minute,
                wSecond = (ushort)utcTime.Second,
                wMilliseconds = (ushort)utcTime.Millisecond
            };

            bool success = SetSystemTime(ref systemTime);
            if (!success)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new System.ComponentModel.Win32Exception(errorCode, "设置系统时间失败");
            }
            return success;
        }

        #region Windows API
        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetSystemTime(ref SYSTEMTIME lpSystemTime);
        #endregion

        private static uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }
    }
}
