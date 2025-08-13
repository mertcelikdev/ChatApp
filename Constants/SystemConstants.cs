namespace ChatApp.Constants;

public static class SystemUsers
{
    /// <summary>
    /// Genel chat için sistem kullanıcısı ID'si
    /// Bu ID hiçbir gerçek kullanıcı tarafından kullanılamaz
    /// </summary>
    public const int GENERAL_CHAT_USER_ID = 999;
    
    /// <summary>
    /// Genel chat için sistem kullanıcısı username'i
    /// </summary>
    public const string GENERAL_CHAT_USERNAME = "SYSTEM_GENERAL_CHAT";
}

public static class MessageTypes
{
    public const string PRIVATE = "Private";
    public const string PUBLIC = "Public"; 
    public const string SYSTEM = "System";
}
