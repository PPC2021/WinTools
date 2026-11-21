using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WinTools;

/// <summary>
/// Settings remembered between launches, stored as key=value lines in
/// %APPDATA%\WinTools\settings.ini.
/// </summary>
internal sealed class AppSettings
{
	public bool AlwaysActive;

	public bool StartOnLaunch;

	public bool ScheduleEnabled = true;

	public List<DayOfWeek> Days = new List<DayOfWeek>
	{
		DayOfWeek.Monday,
		DayOfWeek.Tuesday,
		DayOfWeek.Wednesday,
		DayOfWeek.Thursday,
		DayOfWeek.Friday
	};

	public TimeSpan StartTime = new TimeSpan(8, 0, 0);

	public TimeSpan EndTime = new TimeSpan(17, 0, 0);

	public int BreakMinutes = 15;

	/// <summary>Time zone the schedule follows; empty = the Windows time zone.</summary>
	public string TimeZoneId = "";

	public bool Use24Hour = true;

	/// <summary>The schedule's time zone (falls back to the Windows zone if the id is unknown).</summary>
	public static TimeZoneInfo ResolveZone(string id)
	{
		if (!string.IsNullOrEmpty(id))
		{
			try
			{
				return TimeZoneInfo.FindSystemTimeZoneById(id);
			}
			catch (Exception)
			{
			}
		}
		return TimeZoneInfo.Local;
	}

	public static string FormatClock(DateTime time, bool use24Hour)
	{
		return use24Hour ? time.ToString("HH:mm", CultureInfo.InvariantCulture) : time.ToString("h:mm tt", CultureInfo.InvariantCulture);
	}

	public const string PortableFileName = "WinTools.ini";

	/// <summary>A WinTools.ini next to the exe (portable settings), if there is one.</summary>
	public static string PortableFilePath => Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), PortableFileName);

	public static bool IsPortable => File.Exists(PortableFilePath);

	/// <summary>
	/// WinTools.ini next to the exe when present (settings travel with the exe),
	/// otherwise %APPDATA%\WinTools\settings.ini.
	/// </summary>
	public static string FilePath => IsPortable ? PortableFilePath : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinTools", "settings.ini");

	public static AppSettings Load()
	{
		AppSettings s = new AppSettings();
		try
		{
			if (!File.Exists(FilePath))
			{
				return s;
			}
			foreach (string line in File.ReadAllLines(FilePath))
			{
				int eq = line.IndexOf('=');
				if (eq <= 0)
				{
					continue;
				}
				string key = line.Substring(0, eq).Trim();
				string value = line.Substring(eq + 1).Trim();
				switch (key)
				{
				case "AlwaysActive":
					bool.TryParse(value, out s.AlwaysActive);
					break;
				case "StartOnLaunch":
					bool.TryParse(value, out s.StartOnLaunch);
					break;
				case "ScheduleEnabled":
					bool.TryParse(value, out s.ScheduleEnabled);
					break;
				case "Days":
				{
					List<DayOfWeek> days = new List<DayOfWeek>();
					foreach (string part in value.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries))
					{
						if (Enum.TryParse(part.Trim(), ignoreCase: true, out DayOfWeek d))
						{
							days.Add(d);
						}
					}
					s.Days = days;
					break;
				}
				case "StartTime":
					if (TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan st))
					{
						s.StartTime = st;
					}
					break;
				case "EndTime":
					if (TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan et))
					{
						s.EndTime = et;
					}
					break;
				case "BreakMinutes":
					if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bm) && bm >= 1 && bm <= 240)
					{
						s.BreakMinutes = bm;
					}
					break;
				case "TimeZone":
					s.TimeZoneId = value;
					break;
				case "Use24Hour":
					bool.TryParse(value, out s.Use24Hour);
					break;
				}
			}
		}
		catch (Exception)
		{
			// Unreadable settings fall back to defaults.
		}
		return s;
	}

	public void Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("AlwaysActive=" + AlwaysActive);
			sb.AppendLine("StartOnLaunch=" + StartOnLaunch);
			sb.AppendLine("ScheduleEnabled=" + ScheduleEnabled);
			sb.AppendLine("Days=" + string.Join(",", Days));
			sb.AppendLine("StartTime=" + StartTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture));
			sb.AppendLine("EndTime=" + EndTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture));
			sb.AppendLine("BreakMinutes=" + BreakMinutes.ToString(CultureInfo.InvariantCulture));
			sb.AppendLine("TimeZone=" + TimeZoneId);
			sb.AppendLine("Use24Hour=" + Use24Hour);
			File.WriteAllText(FilePath, sb.ToString());
		}
		catch (Exception)
		{
			// Settings are a convenience; never crash over them.
		}
	}
}
