using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace WinTools;

internal static class Program
{
	[DllImport("kernel32.dll")]
	private static extern bool AttachConsole(int processId);

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

	/// <summary>
	/// Own taskbar identity: the taskbar button then uses the window's (state) icon
	/// instead of Explorer's cached icon for the .exe file.
	/// </summary>
	public const string AppUserModelId = "PPC2021.WinTools";

	/// <summary>
	///   WinTools.exe                       open the window
	///   WinTools.exe --tray                start hidden in the system tray (used at login)
	///   WinTools.exe --login-item on|off|status
	///   WinTools.exe --install             install for this user (start at sign-in) and run it
	///   WinTools.exe --uninstall
	/// </summary>
	[STAThread]
	private static int Main(string[] args)
	{
		int li = Array.IndexOf(args, "--login-item");
		if (li >= 0)
		{
			return LoginItemCommand(li + 1 < args.Length ? args[li + 1] : "status");
		}
		if (Array.IndexOf(args, "--install") >= 0)
		{
			return InstallCommand();
		}
		if (Array.IndexOf(args, "--uninstall") >= 0)
		{
			return UninstallCommand();
		}
		bool startInTray = Array.IndexOf(args, "--tray") >= 0;
		bool takeover = Array.IndexOf(args, "--takeover") >= 0;

		// One copy per user session: a second launch just brings the first one forward.
		using Mutex mutex = new Mutex(initiallyOwned: true, @"Local\WinTools.SingleInstance", out bool createdNew);
		using EventWaitHandle showEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, @"Local\WinTools.Show");
		if (!createdNew && takeover)
		{
			// Started by "Install on this PC": wait for the portable copy to exit, then take over.
			try
			{
				createdNew = mutex.WaitOne(15000);
			}
			catch (AbandonedMutexException)
			{
				createdNew = true;
			}
		}
		if (!createdNew)
		{
			if (!startInTray)
			{
				showEvent.Set();
			}
			return 0;
		}

		SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		Application.Run(new Form1(startInTray, showEvent));
		GC.KeepAlive(mutex);
		return 0;
	}

	/// <summary>WinTools.exe --install: install for this user, start at sign-in, launch in the tray.</summary>
	private static int InstallCommand()
	{
		AttachConsole(-1);
		Console.WriteLine();
		try
		{
			SelfInstall.Install();
			SelfInstall.LaunchInstalled();
			Console.WriteLine("WinTools installed to " + SelfInstall.InstalledExe);
			Console.WriteLine("It starts at sign-in and is running in the system tray.");
			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine("Install failed: " + ex.Message);
			return 1;
		}
	}

	/// <summary>WinTools.exe --uninstall: remove the install, shortcut and sign-in entry.</summary>
	private static int UninstallCommand()
	{
		AttachConsole(-1);
		Console.WriteLine();
		try
		{
			SelfInstall.Uninstall();
			Console.WriteLine("WinTools uninstalled (settings kept in %APPDATA%\\WinTools).");
			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine("Uninstall failed: " + ex.Message);
			return 1;
		}
	}

	private static int LoginItemCommand(string action)
	{
		AttachConsole(-1);
		int code = 0;
		try
		{
			switch (action)
			{
			case "on":
				LoginItem.Set(enabled: true);
				break;
			case "off":
				LoginItem.Set(enabled: false);
				break;
			}
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine("login item " + action + " failed: " + ex.Message);
			code = 1;
		}
		Console.WriteLine();
		Console.WriteLine("WinTools login item: " + LoginItem.Describe());
		return code;
	}
}
