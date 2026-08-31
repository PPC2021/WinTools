using System;
using System.Collections.Generic;
using System.Text;

namespace KeepAlive;

public sealed class WorkingHoursSchedule
{
	private static readonly DayOfWeek[] WeekdayOrder = new DayOfWeek[7]
	{
		DayOfWeek.Monday,
		DayOfWeek.Tuesday,
		DayOfWeek.Wednesday,
		DayOfWeek.Thursday,
		DayOfWeek.Friday,
		DayOfWeek.Saturday,
		DayOfWeek.Sunday
	};

	public static WorkingHoursSchedule Default { get; } = new WorkingHoursSchedule(enabled: true, new DayOfWeek[5]
	{
		DayOfWeek.Monday,
		DayOfWeek.Tuesday,
		DayOfWeek.Wednesday,
		DayOfWeek.Thursday,
		DayOfWeek.Friday
	}, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));

	public bool Enabled { get; }

	public HashSet<DayOfWeek> Days { get; }

	public TimeSpan StartTime { get; }

	public TimeSpan EndTime { get; }

	public WorkingHoursSchedule(bool enabled, IEnumerable<DayOfWeek> days, TimeSpan startTime, TimeSpan endTime)
	{
		Enabled = enabled;
		StartTime = startTime;
		EndTime = endTime;
		HashSet<DayOfWeek> hashSet = new HashSet<DayOfWeek>();
		if (days != null)
		{
			foreach (DayOfWeek day in days)
			{
				hashSet.Add(day);
			}
		}
		Days = hashSet;
	}

	public bool IsActive(DateTime now)
	{
		if (!Enabled || Days.Count == 0)
		{
			return false;
		}
		if (StartTime == EndTime)
		{
			return Days.Contains(now.DayOfWeek);
		}
		TimeSpan timeOfDay = now.TimeOfDay;
		if (StartTime < EndTime)
		{
			return Days.Contains(now.DayOfWeek) && timeOfDay >= StartTime && timeOfDay < EndTime;
		}
		if (Days.Contains(now.DayOfWeek) && timeOfDay >= StartTime)
		{
			return true;
		}
		DateTime previousDay = now.AddDays(-1.0);
		return Days.Contains(previousDay.DayOfWeek) && timeOfDay < EndTime;
	}

	public string Describe()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(Enabled ? "Enabled: " : "Disabled: ");
		if (Days.Count == 0)
		{
			stringBuilder.Append("no days selected");
			return stringBuilder.ToString();
		}
		bool flag = true;
		DayOfWeek[] array = WeekdayOrder;
		foreach (DayOfWeek dayOfWeek in array)
		{
			if (!Days.Contains(dayOfWeek))
			{
				continue;
			}
			if (!flag)
			{
				stringBuilder.Append(", ");
			}
			stringBuilder.Append(ShortName(dayOfWeek));
			flag = false;
		}
		stringBuilder.Append(" ");
		stringBuilder.Append(FormatTime(StartTime));
		stringBuilder.Append(" - ");
		stringBuilder.Append(FormatTime(EndTime));
		return stringBuilder.ToString();
	}

	private static string ShortName(DayOfWeek day)
	{
		switch (day)
		{
		case DayOfWeek.Monday:
			return "Mon";
		case DayOfWeek.Tuesday:
			return "Tue";
		case DayOfWeek.Wednesday:
			return "Wed";
		case DayOfWeek.Thursday:
			return "Thu";
		case DayOfWeek.Friday:
			return "Fri";
		case DayOfWeek.Saturday:
			return "Sat";
		default:
			return "Sun";
		}
	}

	private static string FormatTime(TimeSpan time)
	{
		return DateTime.Today.Add(time).ToString("HH:mm");
	}
}
