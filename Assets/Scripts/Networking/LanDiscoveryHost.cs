using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace EvasiveWhisker.Networking.Lan
{
    /// <summary>
    /// Host-only. Broadcasts the room announcement once per second while the lobby is open.
    /// Sends only; never binds the discovery port, so a host and a client listener can share a machine.
    /// </summary>
    public sealed class LanDiscoveryHost : MonoBehaviour
    {
        Func<LanRoomInfo> _provider;
        UdpClient _udp;
        List<IPEndPoint> _targets = new List<IPEndPoint>();
        float _nextSend;
        float _nextTargetRefresh;

        public bool IsBroadcasting => _udp != null;

        public void Begin(Func<LanRoomInfo> provider)
        {
            End();
            _provider = provider;
            _udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
            _targets = LanNetUtil.GetBroadcastTargets(LanConstants.DiscoveryPort);
            _nextSend = 0f;
            _nextTargetRefresh = Time.unscaledTime + 5f;
        }

        public void End()
        {
            if (_udp != null)
            {
                try { _udp.Close(); } catch (Exception) { }
                _udp = null;
            }
            _provider = null;
        }

        void OnDestroy() => End();

        void Update()
        {
            if (_udp == null || _provider == null) return;

            float now = Time.unscaledTime;
            if (now < _nextSend) return;
            _nextSend = now + LanConstants.BroadcastIntervalSeconds;

            // Networks can change (Wi-Fi reconnect, hotspot toggled), so refresh targets periodically.
            if (now >= _nextTargetRefresh)
            {
                _targets = LanNetUtil.GetBroadcastTargets(LanConstants.DiscoveryPort);
                _nextTargetRefresh = now + 5f;
            }

            byte[] data = _provider().Encode();
            foreach (var target in _targets)
            {
                try { _udp.Send(data, data.Length, target); }
                catch (SocketException e) { Debug.LogWarning($"[LAN] Broadcast to {target} failed: {e.SocketErrorCode}"); }
                catch (ObjectDisposedException) { return; }
            }
        }
    }
}
