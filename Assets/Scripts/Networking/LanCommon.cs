using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace EvasiveWhisker.Networking.Lan
{
    /// <summary>
    /// LAN-specific tunables. Placeholders are flagged with TODO.
    /// </summary>
    public static class LanConstants
    {
        public const string Magic = "EWLAN";
        public const int ProtocolVersion = 1;

        /// <summary>UDP port used only for room announcements (host -> LAN).</summary>
        public const int DiscoveryPort = 47777;

        /// <summary>UDP port used by Netcode's UnityTransport for the game itself.</summary>
        public const ushort GamePort = 7777;

        // TODO: reconcile with GameConstants (3-6 players) when the Core scripts are next touched.
        public const int MinPlayers = 3;
        public const int MaxPlayers = 6;

        public const float BroadcastIntervalSeconds = 1f;
        public const float RoomExpirySeconds = 4f;
        public const int MaxNameLength = 20;

        // Since reconnection is dropped, this is how long a silent connection lives before
        // the host treats the player as disconnected (= forfeit). TODO: tune in playtests.
        public const int DisconnectTimeoutMs = 8000;
        public const int ConnectTimeoutMs = 1000;
        public const int MaxConnectAttempts = 5;
    }

    /// <summary>What a host announces on the LAN. Host IP is taken from the UDP sender, not the payload.</summary>
    public struct LanRoomInfo
    {
        public string RoomName;
        public string HostName;
        public int PlayerCount;
        public int MaxPlayers;
        public ushort GamePort;
        public bool Open;

        public static string Sanitize(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s.Replace('|', ' ').Trim();
            return s.Length > maxLen ? s.Substring(0, maxLen) : s;
        }

        public string ToWireString()
        {
            var inv = CultureInfo.InvariantCulture;
            return string.Join("|",
                LanConstants.Magic,
                LanConstants.ProtocolVersion.ToString(inv),
                Sanitize(RoomName, LanConstants.MaxNameLength),
                Sanitize(HostName, LanConstants.MaxNameLength),
                PlayerCount.ToString(inv),
                MaxPlayers.ToString(inv),
                GamePort.ToString(inv),
                Open ? "1" : "0");
        }

        public byte[] Encode() => Encoding.UTF8.GetBytes(ToWireString());

        public bool SameAs(LanRoomInfo other) => ToWireString() == other.ToWireString();

        public static bool TryDecode(byte[] data, int length, out LanRoomInfo info)
        {
            info = default;
            if (data == null || length <= 0) return false;

            string text;
            try { text = Encoding.UTF8.GetString(data, 0, length); }
            catch (Exception) { return false; }

            string[] p = text.Split('|');
            if (p.Length != 8 || p[0] != LanConstants.Magic) return false;

            var inv = CultureInfo.InvariantCulture;
            if (!int.TryParse(p[1], NumberStyles.Integer, inv, out int version) ||
                version != LanConstants.ProtocolVersion) return false;
            if (!int.TryParse(p[4], NumberStyles.Integer, inv, out int count)) return false;
            if (!int.TryParse(p[5], NumberStyles.Integer, inv, out int max)) return false;
            if (!ushort.TryParse(p[6], NumberStyles.Integer, inv, out ushort port)) return false;

            info = new LanRoomInfo
            {
                RoomName = p[2],
                HostName = p[3],
                PlayerCount = count,
                MaxPlayers = max,
                GamePort = port,
                Open = p[7] == "1"
            };
            return true;
        }
    }

    /// <summary>A room seen on the LAN. HostAddress is the UDP sender's IPv4 address.</summary>
    public readonly struct DiscoveredRoom
    {
        public readonly LanRoomInfo Info;
        public readonly string HostAddress;
        public readonly float LastSeen;

        public DiscoveredRoom(LanRoomInfo info, string hostAddress, float lastSeen)
        {
            Info = info;
            HostAddress = hostAddress;
            LastSeen = lastSeen;
        }
    }

    public static class LanNetUtil
    {
        /// <summary>Local IPv4 addresses, for showing the host their IP (manual-join fallback).</summary>
        public static List<string> GetLocalIPv4Addresses()
        {
            var result = new List<string>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                            result.Add(ua.Address.ToString());
                    }
                }
            }
            catch (Exception) { /* some platforms restrict interface enumeration */ }
            return result;
        }

        /// <summary>
        /// Global broadcast plus each interface's directed broadcast (e.g. 192.168.1.255).
        /// Directed broadcasts help on networks/phones where 255.255.255.255 is not forwarded.
        /// </summary>
        public static List<IPEndPoint> GetBroadcastTargets(int port)
        {
            var targets = new List<IPEndPoint> { new IPEndPoint(IPAddress.Broadcast, port) };
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;

                        byte[] ip = ua.Address.GetAddressBytes();
                        byte[] mask = ua.IPv4Mask.GetAddressBytes();
                        var b = new byte[4];
                        for (int i = 0; i < 4; i++) b[i] = (byte)(ip[i] | (~mask[i] & 0xFF));
                        targets.Add(new IPEndPoint(new IPAddress(b), port));
                    }
                }
            }
            catch (Exception) { /* fall back to global broadcast only */ }
            return targets;
        }
    }
}
