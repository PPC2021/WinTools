using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WinTools;

/// <summary>
/// Starts WinTools in the system tray at every sign-in, using the per-user
/// Run key (HKCU\Software\Microsoft\Windows\CurrentVersion\Run). No admin rights needed.
/// It shows up in Task Manager > Startup apps and Settings > Apps > Startup.
/// </summary>
internal static class LoginItem
{
	private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

	private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

	private const string ValueName = "WinTools";

	public static string Command => CommandFor(Application.ExecutablePath);

	public static string CommandFor(string exePath)
	{
		return "\"" + exePath + "\" --tray";
	}

	/// <summary>The command registered at login, or null.</summary>
	public static string RegisteredCommand
	{
		get
		{
			using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey);
			return key?.GetValue(ValueName) as string;
		}
	}

	/// <summary>True when the user turned it off in Task Manager / Settings > Startup.</summary>
	public static bool DisabledByUser
	{
		get
		{
			using RegistryKey key = Registry.CurrentUser.OpenSubKey(ApprovedKey);
			// First byte: 2 = enabled, 3 = disabled.
			return key?.GetValue(ValueName) is byte[] data && data.Length > 0 && (data[0] & 1) == 1;
		}
	}

	public static bool IsEnabled => !string.IsNullOrEmpty(RegisteredCommand) && !DisabledByUser;

	/// <param name="exePath">The exe to start at sign-in (default: this one).</param>
	public static void Set(bool enabled, string exePath = null)
	{
		using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
		{
			if (enabled)
			{
				key.SetValue(ValueName, (exePath == null) ? Command : CommandFor(exePath), RegistryValueKind.String);
			}
			else
			{
				key.DeleteValue(ValueName, throwOnMissingValue: false);
			}
		}
		// Clear any "disabled" flag from Task Manager so turning it on really turns it on.
		using RegistryKey approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
		approved?.DeleteValue(ValueName, throwOnMissingValue: false);
	}

	public static string Describe()
	{
		string cmd = RegisteredCommand;
		if (string.IsNullOrEmpty(cmd))
		{
			return "not registered";
		}
		if (DisabledByUser)
		{
			return "registered but disabled in Task Manager > Startup apps (" + cmd + ")";
		}
		return "enabled (" + cmd + ")";
	}
}
