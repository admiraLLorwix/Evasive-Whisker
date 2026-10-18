using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EvasiveWhisker.Networking.Lan.UI
{
    /// <summary>
    /// One row in the "games found on this network" list. Put this on the root of a prefab
    /// with a Button and the text fields below; LanMenuUI instantiates one per discovered room.
    /// </summary>
    public sealed class RoomListItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private TMP_Text hostNameText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private Button joinButton;

        /// <summary>Wires this row to one discovered room. Call right after Instantiate.</summary>
        public void Bind(DiscoveredRoom room, Action<DiscoveredRoom> onJoin)
        {
            if (roomNameText != null) roomNameText.text = room.Info.RoomName;
            if (hostNameText != null) hostNameText.text = room.Info.HostName;
            if (playerCountText != null)
                playerCountText.text = $"{room.Info.PlayerCount}/{room.Info.MaxPlayers}";

            if (joinButton != null)
            {
                joinButton.interactable = room.Info.Open;
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener(() => onJoin?.Invoke(room));
            }
        }
    }
}
