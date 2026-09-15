namespace EvasiveWhisker.Core
{
    /// <summary>
    /// Shared game-balance constants that Core and Networking both need to agree on.
    /// </summary>
    public static class GameConstants
    {
        // TODO: placeholder pending confirmation against real game design. Chosen to
        // comfortably exceed any expected hand size for a 3–6 player free-for-all.
        // Drives the fixed-size array used by HiddenInfoChannel's hand-resync RPC —
        // raise this in one place if the real max hand size differs.
        public const int MaxHandSize = 10;
    }
}
