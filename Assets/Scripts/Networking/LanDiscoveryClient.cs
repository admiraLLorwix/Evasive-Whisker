using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace EvasiveWhisker.Networking.Lan
{
    /// <summary>
    /// Client-side. Listens for room announcements and maintains a live room list.
    /// Socket callbacks run on a worker thread and only enqueue; all state is touched on the main thread.
    /// </summary>
    public sealed class LanDiscoveryClient : MonoBehaviour
    {
        struct Packet
        {
            public IPAddress Address;
            public byte[] Data;
        }

        readonly ConcurrentQueue<Packet> _inbox = new ConcurrentQueue<Packet>();
        readonly Dictionary<string, DiscoveredRoom> _rooms = new Dictionary<string, DiscoveredRoom>();
        readonly List<string> _expired = new List<string>();
        volatile UdpClient _udp;

        /// <summary>Raised on the main thread when a room appears, disappears, or its info changes.</summary>
        public event Action RoomsChanged;

        public IReadOnlyCollection<DiscoveredRoom> Rooms => _rooms.Values;
        public bool IsListening => _udp != null;

        public bool StartListening()
        {
            StopListening();
            try
            {
                var udp = new UdpClient(AddressFamily.InterNetwork);
                // Allow several listeners (e.g. two editor instances) to share the port.
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, LanConstants.DiscoveryPort));
                udp.EnableBroadcast = true;
                _udp = udp;
                BeginReceive();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LAN] Could not listen on UDP {LanConstants.DiscoveryPort}: {e.Message}. Use manual IP join.");
                StopListening();
                return false;
            }
        }

        public void StopListening()
        {
            var udp = _udp;
            _udp = null;
            if (udp != null)
            {
                try { udp.Close(); } catch (Exception) { }
            }
            while (_inbox.TryDequeue(out _)) { }
            if (_rooms.Count > 0)
            {
                _rooms.Clear();
                RoomsChanged?.Invoke();
            }
        }

        void OnDestroy() => StopListening();

        void BeginReceive()
        {
            var udp = _udp;
            if (udp == null) return;
            try { udp.BeginReceive(OnReceive, udp); }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }

        void OnReceive(IAsyncResult ar)
        {
            var udp = (UdpClient)ar.AsyncState;
            var ep = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = null;

            try { data = udp.EndReceive(ar, ref ep); }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { /* e.g. Windows UDP connection-reset; keep listening */ }

            if (data != null)
                _inbox.Enqueue(new Packet { Address = ep.Address, Data = data });

            if (udp == _udp) BeginReceive();
        }

        void Update()
        {
            if (_udp == null) return;

            float now = Time.unscaledTime;
            bool changed = false;

            while (_inbox.TryDequeue(out Packet packet))
            {
                if (!LanRoomInfo.TryDecode(packet.Data, packet.Data.Length, out LanRoomInfo info)) continue;

                string address = packet.Address.ToString();
                string key = address + ":" + info.GamePort;

                if (!_rooms.TryGetValue(key, out DiscoveredRoom existing) || !existing.Info.SameAs(info))
                    changed = true;

                _rooms[key] = new DiscoveredRoom(info, address, now);
            }

            _expired.Clear();
            foreach (var kv in _rooms)
            {
                if (now - kv.Value.LastSeen > LanConstants.RoomExpirySeconds)
                    _expired.Add(kv.Key);
            }
            foreach (string key in _expired)
            {
                _rooms.Remove(key);
                changed = true;
            }

            if (changed) RoomsChanged?.Invoke();
        }
    }
}
