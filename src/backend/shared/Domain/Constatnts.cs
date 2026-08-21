namespace Domain;

public static class Environments
{
    public const string Testing = "Testing";

    public static bool IsTesting =>
        Environment.GetEnvironmentVariable(nameof(Testing)) == nameof(Testing);
}

public enum UserRole
{
    Default = 0,
    Admin = 1,
    SuperAdmin = 2
}

/// <summary>Кто написал сообщение в чате. Лежит здесь, чтобы enum видели и контракты, и Domain сервиса.</summary>
public enum ChatDirection
{
    Visitor = 0,
    Admin = 1
}

public static class Constatnts
{
    public static class FieldLength
    {
        public const int Text32 = 32;
        public const int Text64 = 64;
        public const int Text128 = 128;
        public const int Text255 = 255;
        public const int Text512 = 512;
        public const int Text1024 = 1024;
        public const int Text2048 = 2048;
    }

}