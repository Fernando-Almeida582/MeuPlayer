using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace MeuPlayer;

internal static class WindowsStartup
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Entry = "MeuPlayer";
    private static string? Command =>
        Environment.ProcessPath is { } path && string.Equals(Path.GetFileName(path), "MeuPlayer.exe", StringComparison.OrdinalIgnoreCase) &&
        path.Length <= 258 ? $"\"{path}\"" : null;

    internal static bool IsRegistered
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(Key);
                return key?.GetValue(Entry) is string;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException) { return false; }
        }
    }

    internal static bool Set(bool enabled)
    {
        try
        {
            if (enabled)
            {
                if (Command is not { } command) return false;
                using var key = Registry.CurrentUser.CreateSubKey(Key);
                key.SetValue(Entry, command, RegistryValueKind.String);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(Key, writable: true);
                key?.DeleteValue(Entry, throwOnMissingValue: false);
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException) { return false; }
    }
}
