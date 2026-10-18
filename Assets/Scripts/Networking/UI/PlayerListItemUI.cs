using TMPro;
using UnityEngine;

namespace EvasiveWhisker.Networking.Lan.UI
{
    /// <summary>
    /// One row in the lobby roster. Put this on the root of a prefab with the text fields
    /// below; LanMenuUI instantiates one per player in LanLobby.
    /// </summary>
    public sealed class PlayerListItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject youMarker; // optional, e.g. a small "(You)" label or icon

        public void Bind(LobbyPlayer player, bool isHost, bool isLocalPlayer)
        {
            if (nameText != null) nameText.text = player.Name.ToString();

            if (statusText != null)
                statusText.text = isHost ? "HOST" : (player.Ready ? "READY" : "Not ready");

            if (youMarker != null) youMarker.SetActive(isLocalPlayer);
        }
    }
}
