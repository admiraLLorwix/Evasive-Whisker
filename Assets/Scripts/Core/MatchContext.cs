public static class MatchContext
{
    public static string PreviousLobbyId;
    public static string PreviousLobbyName;
    public static string PreviousLobbyCode;
    public static int PreviousMaxPlayers;

    public static void Clear()
    {
        PreviousLobbyId = null;
        PreviousLobbyName = null;
        PreviousLobbyCode = null;
        PreviousMaxPlayers = 0;
    }
}