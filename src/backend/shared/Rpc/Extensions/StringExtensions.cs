namespace Rpc.Extensions;

public static class StringExtensions
{
    public static string OrEmpty(this string? value) => value ?? "";
    public static string? NullIfEmpty(this string? value) => string.IsNullOrEmpty(value) ? null : value;
}
