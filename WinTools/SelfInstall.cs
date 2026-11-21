using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace WinTools;

/// <summary>
/// Lets the portable exe install itself for the current user (no admin rights):
/// copies to %LOCALAPPDATA%\Programs\WinTools, adds a Start menu shortcut and
/// starts it at sign-in.
/// </summary>
internal static class SelfInstall
{
	public static string InstallDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinTools");

	public static string InstalledExe => Path.Combine(InstallDir, "WinTools.exe");

	private static string ShortcutPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "WinTools.lnk");

	public static bool IsRunningInstalled => string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase);

	public static bool IsInstalled => File.Exists(InstalledExe);

	/// <summary>Copies this exe into place (replacing an older install), adds the shortcut and the sign-in entry.</summary>
	public static void Install()
	{
		Directory.CreateDirectory(InstallDir);
		if (!IsRunningInstalled)
		{
			StopInstalledCopies();
			File.Copy(Application.ExecutablePath, InstalledExe, overwrite: true);
			// A portable WinTools.ini travels with the install.
			string ini = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), AppSettings.PortableFileName);
			if (File.Exists(ini))
			{
				File.Copy(ini, Path.Combine(InstallDir, AppSettings.PortableFileName), overwrite: true);
			}
		}
		CreateShortcut(ShortcutPath, InstalledExe);
		LoginItem.Set(enabled: true, InstalledExe);
	}

	/// <summary>Removes the sign-in entry, shortcut and installed files. Settings in %APPDATA% are kept.</summary>
	public static void Uninstall()
	{
		string registered = LoginItem.RegisteredCommand;
		if (registered != null && registered.IndexOf(InstallDir, StringComparison.OrdinalIgnoreCase) >= 0)
		{
			LoginItem.Set(enabled: false);
		}
		try
		{
			File.Delete(ShortcutPath);
		}
		catch (Exception)
		{
		}
		if (!Directory.Exists(InstallDir))
		{
			return;
		}
		if (IsRunningInstalled)
		{
			// Can't delete our own exe while running: remove the folder a moment after we exit.
			Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"" + InstallDir + "\"")
			{
				CreateNoWindow = true,
				UseShellExecute = false
			});
		}
		else
		{
			StopInstalledCopies();
			Directory.Delete(InstallDir, recursive: true);
		}
	}

	/// <summary>Starts the installed copy; it takes over once this process exits.</summary>
	public static void LaunchInstalled(bool inTray = true)
	{
		// Shell execute: the long-running tray app must not inherit our console/stdout handles.
		Process.Start(new ProcessStartInfo(InstalledExe, inTray ? "--tray --takeover" : "--takeover")
		{
			UseShellExecute = true,
			WorkingDirectory = InstallDir
		});
	}

	private static void StopInstalledCopies()
	{
		int self = Process.GetCurrentProcess().Id;
		foreach (Process p in Process.GetProcessesByName("WinTools"))
		{
			try
			{
				if (p.Id != self && string.Equals(p.MainModule.FileName, InstalledExe, StringComparison.OrdinalIgnoreCase))
				{
					p.Kill();
					p.WaitForExit(5000);
				}
			}
			catch (Exception)
			{
			}
			finally
			{
				p.Dispose();
			}
		}
	}

	private static void CreateShortcut(string path, string target)
	{
		Type shellType = Type.GetTypeFromProgID("WScript.Shell");
		if (shellType == null)
		{
			return;
		}
		object shell = Activator.CreateInstance(shellType);
		object link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[1] { path });
		Type linkType = link.GetType();
		linkType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[1] { target });
		linkType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[1] { Path.GetDirectoryName(target) });
		linkType.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[1] { "WinTools - keep the PC awake during working hours" });
		linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
	}
}
