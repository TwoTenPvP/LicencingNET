using System;
using System.Net;
using System.Net.Sockets;

namespace LicencingNET
{
    internal static class NTP
    {
        // Address to NTP server
        private const string ntpServer = "pool.ntp.org";
        // Offset to get to the "Transmit Timestamp" field (time at which the reply departed the server for the client, in 64-bit timestamp format.)
        private const byte serverTimeOffset = 40;

        internal static DateTime GetNetworkTime()
        {
            // NTP message size - 16 bytes of the digest (RFC 2030)
            byte[] ntpData = new byte[48];

            // Setting the Leap Indicator, Version Number and Mode values
            ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

            // Resolve NTP address
            IPAddress[] addresses = Dns.GetHostEntry(ntpServer).AddressList;

            // The UDP port number assigned to NTP is 123
            IPEndPoint endPoint = new IPEndPoint(addresses[0], 123);

            // Create UDP socket
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect(endPoint);

                // Stops code hang if NTP is blocked
                socket.ReceiveTimeout = 3000;
                socket.SendTimeout = 3000;

                // Sends NTP request
                socket.SendTo(ntpData, endPoint);

                // Waits for NTP response
                socket.Receive(ntpData);
            }

            // Seconds part (fixes endianess)
            ulong intPart = (ulong)ntpData[serverTimeOffset] << 24 | (ulong)ntpData[serverTimeOffset + 1] << 16 | (ulong)ntpData[serverTimeOffset + 2] << 8 | (ulong)ntpData[serverTimeOffset + 3];
            // Seconds fraction (fixes endianess)
            ulong fractPart = (ulong)ntpData[serverTimeOffset + 4] << 24 | (ulong)ntpData[serverTimeOffset + 5] << 16 | (ulong)ntpData[serverTimeOffset + 6] << 8 | (ulong)ntpData[serverTimeOffset + 7];

            // Milliseconds offset
            ulong milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

            // **UTC** time
            DateTime networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

            return networkDateTime.ToLocalTime();
        }
    }
}
