using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using KeepAlive;
using Microsoft.Win32;

namespace WinTools;

public class Form1 : Form
{
	private volatile bool Run;

	private volatile bool exit;

	/// <summary>Override: keep alive regardless of the schedule (breaks still apply).</summary>
	private volatile bool alwaysActive;

	private volatile WorkingHoursSchedule currentSchedule = WorkingHoursSchedule.Default;

	private volatile bool keepAliveActive;

	private DateTime lastKeepAlivePulse = DateTime.MinValue;

	private long breakUntilTicks;

	/// <summary>The current pause came from End Day (shown as "Day ended" / "Resume").</summary>
	private volatile bool dayEnded;

	/// <summary>Countdown text for the current break, e.g. "14:32" (written by the monitor thread).</summary>
	private volatile string breakRemaining = "";

	private Button endDayButton;

	private ToolStripMenuItem trayEndDayItem;

	private Button installButton;

	private ToolStripMenuItem trayInstallItem;

	private readonly AutoResetEvent wake = new AutoResetEvent(initialState: false);

	private readonly EventWaitHandle showEvent;

	private readonly AppSettings settings = AppSettings.Load();

	private bool loading;

	private bool startInTray;

	private bool quitting;

	private bool trayHintShown;

	private bool threadsStarted;

	private TrayIcons trayIcons = new TrayIcons();

	private readonly TrayIcons windowIcons = new TrayIcons(tile: true);

	private TrayState trayState = TrayState.Stopped;

	/// <summary>Time zone id the schedule follows ("" = Windows time zone).</summary>
	private volatile string zoneId = "";

	private volatile bool use24Hour = true;

	/// <summary>"Now: 3:21 PM Eastern Standard Time", written by the monitor thread.</summary>
	private volatile string nowText = "";

	private Label timeZoneLabel;

	private ComboBox timeZoneComboBox;

	private Label nowLabel;

	private CheckBox use24HourCheckBox;

	/// <summary>An entry in the time zone list.</summary>
	private sealed class ZoneItem
	{
		public readonly string Id;

		public readonly string Text;

		public ZoneItem(string id, string text)
		{
			Id = id;
			Text = text;
		}

		public override string ToString()
		{
			return Text;
		}
	}

	private IContainer components = null;

	private Button button1;

	private Label Status_label;

	private Label IdleTime_label;

	private CheckBox alwaysActiveCheckBox;

	private GroupBox scheduleGroup;

	private CheckBox scheduleEnabledCheckBox;

	private CheckedListBox daysCheckedListBox;

	private DateTimePicker startTimePicker;

	private DateTimePicker endTimePicker;

	private Button weekdaysButton;

	private Button allDaysButton;

	private Button clearDaysButton;

	private Label scheduleSummaryLabel;

	private NumericUpDown breakMinutesUpDown;

	private Label breakMinutesLabel;

	private Button takeBreakButton;

	private CheckBox loginCheckBox;

	private CheckBox startOnLaunchCheckBox;

	private NotifyIcon notifyIcon;

	private ContextMenuStrip trayMenu;

	private ToolStripMenuItem trayOpenItem;

	private ToolStripMenuItem trayStartStopItem;

	private ToolStripMenuItem trayAlwaysActiveItem;

	private ToolStripMenuItem trayBreakItem;

	private ToolStripMenuItem trayQuitItem;

	public Form1()
		: this(startInTray: false, null)
	{
	}

	public Form1(bool startInTray, EventWaitHandle showEvent)
	{
		this.startInTray = startInTray;
		this.showEvent = showEvent;
		loading = true;
		InitializeComponent();
		LoadSettingsIntoUi();
		loading = false;
		ApplyScheduleFromUi();
		SetStatus(Run ? "Status: Started" : "Status: Stopped", "Idle Time: ", onBreak: false, TrayState.Stopped);
		SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
	}

	// ---------- Settings ----------

	private void LoadSettingsIntoUi()
	{
		use24Hour = settings.Use24Hour;
		use24HourCheckBox.Checked = settings.Use24Hour;
		ApplyClockFormat();
		zoneId = settings.TimeZoneId;
		FillTimeZones();
		scheduleEnabledCheckBox.Checked = settings.ScheduleEnabled;
		for (int i = 0; i < daysCheckedListBox.Items.Count; i++)
		{
			daysCheckedListBox.SetItemChecked(i, settings.Days.Contains((DayOfWeek)daysCheckedListBox.Items[i]));
		}
		startTimePicker.Value = DateTime.Today.Add(settings.StartTime);
		endTimePicker.Value = DateTime.Today.Add(settings.EndTime);
		breakMinutesUpDown.Value = settings.BreakMinutes;
		alwaysActiveCheckBox.Checked = settings.AlwaysActive;
		((Control)scheduleGroup).Enabled = !settings.AlwaysActive;
		startOnLaunchCheckBox.Checked = settings.StartOnLaunch;
		loginCheckBox.Checked = LoginItem.IsEnabled;
		alwaysActive = settings.AlwaysActive;
		Run = settings.StartOnLaunch || settings.AlwaysActive;
	}

	private void SaveSettings()
	{
		if (loading)
		{
			return;
		}
		WorkingHoursSchedule s = currentSchedule;
		settings.AlwaysActive = alwaysActiveCheckBox.Checked;
		settings.StartOnLaunch = startOnLaunchCheckBox.Checked;
		settings.ScheduleEnabled = s.Enabled;
		settings.Days = new List<DayOfWeek>(s.Days);
		settings.StartTime = s.StartTime;
		settings.EndTime = s.EndTime;
		settings.BreakMinutes = (int)breakMinutesUpDown.Value;
		settings.TimeZoneId = zoneId;
		settings.Use24Hour = use24Hour;
		settings.Save();
	}

	// ---------- Time zone and clock format ----------

	private void FillTimeZones()
	{
		timeZoneComboBox.BeginUpdate();
		timeZoneComboBox.Items.Clear();
		timeZoneComboBox.Items.Add(new ZoneItem("", "Windows time zone - " + TimeZoneInfo.Local.DisplayName));
		int selected = 0;
		foreach (TimeZoneInfo tz in TimeZoneInfo.GetSystemTimeZones())
		{
			int index = timeZoneComboBox.Items.Add(new ZoneItem(tz.Id, tz.DisplayName));
			if (tz.Id == zoneId)
			{
				selected = index;
			}
		}
		if (selected == 0)
		{
			zoneId = "";
		}
		timeZoneComboBox.SelectedIndex = selected;
		timeZoneComboBox.EndUpdate();
	}

	private void timeZoneComboBox_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (loading || !(timeZoneComboBox.SelectedItem is ZoneItem item))
		{
			return;
		}
		zoneId = item.Id;
		SaveSettings();
		wake.Set();
	}

	private void ApplyClockFormat()
	{
		string format = use24Hour ? "HH:mm" : "h:mm tt";
		startTimePicker.CustomFormat = format;
		endTimePicker.CustomFormat = format;
		((Control)startTimePicker).Width = use24Hour ? 96 : 104;
		((Control)endTimePicker).Width = use24Hour ? 96 : 104;
	}

	private void use24HourCheckBox_CheckedChanged(object sender, EventArgs e)
	{
		if (loading)
		{
			return;
		}
		use24Hour = use24HourCheckBox.Checked;
		ApplyClockFormat();
		UpdateScheduleSummary();
		SaveSettings();
		wake.Set();
	}

	private static string ZoneName(TimeZoneInfo zone, DateTime zoneTime)
	{
		return zone.IsDaylightSavingTime(zoneTime) ? zone.DaylightName : zone.StandardName;
	}

	// ---------- Schedule ----------

	private void UpdateScheduleSummary()
	{
		((Control)scheduleSummaryLabel).Text = currentSchedule.Describe(use24Hour);
	}

	private void ApplyScheduleFromUi()
	{
		if (loading)
		{
			return;
		}
		List<DayOfWeek> list = new List<DayOfWeek>();
		for (int j = 0; j < daysCheckedListBox.Items.Count; j++)
		{
			if (daysCheckedListBox.GetItemChecked(j))
			{
				list.Add((DayOfWeek)daysCheckedListBox.Items[j]);
			}
		}
		currentSchedule = new WorkingHoursSchedule(scheduleEnabledCheckBox.Checked, list, startTimePicker.Value.TimeOfDay, endTimePicker.Value.TimeOfDay);
		UpdateScheduleSummary();
		SaveSettings();
		wake.Set();
	}

	private void SelectDays(params DayOfWeek[] selectedDays)
	{
		for (int i = 0; i < daysCheckedListBox.Items.Count; i++)
		{
			DayOfWeek item = (DayOfWeek)daysCheckedListBox.Items[i];
			daysCheckedListBox.SetItemChecked(i, Array.IndexOf(selectedDays, item) >= 0);
		}
		ApplyScheduleFromUi();
	}

	// ---------- Commands (window buttons and tray menu) ----------

	private void StartStop()
	{
		if (Run)
		{
			// Stop also turns off the always-active override (same as MacTools).
			Run = false;
			alwaysActiveCheckBox.Checked = false;
			Interlocked.Exchange(ref breakUntilTicks, 0L);
			dayEnded = false;
		}
		else
		{
			Run = true;
		}
		wake.Set();
	}

	private void SetAlwaysActive(bool on)
	{
		alwaysActive = on;
		((Control)scheduleGroup).Enabled = !on;
		if (on && !Run)
		{
			Run = true;
		}
		SaveSettings();
		wake.Set();
	}

	private bool OnBreak => Interlocked.Read(ref breakUntilTicks) > DateTime.Now.Ticks;

	private void ToggleBreak()
	{
		if (OnBreak)
		{
			// End Break / Resume (also undoes End Day).
			Interlocked.Exchange(ref breakUntilTicks, 0L);
			dayEnded = false;
		}
		else
		{
			int num = (int)breakMinutesUpDown.Value;
			if (num <= 0)
			{
				return;
			}
			if (!Run)
			{
				Run = true;
			}
			dayEnded = false;
			Interlocked.Exchange(ref breakUntilTicks, DateTime.Now.AddMinutes(num).Ticks);
		}
		wake.Set();
	}

	/// <summary>
	/// Like a break, but until the next working-hours start (or midnight when the schedule
	/// has no days or is turned off). Resume undoes it.
	/// </summary>
	private void EndDay()
	{
		if (!Run)
		{
			Run = true;
		}
		dayEnded = true;
		Interlocked.Exchange(ref breakUntilTicks, NextDayStart(DateTime.Now).Ticks);
		wake.Set();
	}

	/// <summary>Local time when the next working day starts, in the schedule's time zone.</summary>
	private DateTime NextDayStart(DateTime localNow)
	{
		WorkingHoursSchedule s = currentSchedule;
		TimeZoneInfo zone = AppSettings.ResolveZone(zoneId);
		DateTime zoneNow = TimeZoneInfo.ConvertTime(localNow, TimeZoneInfo.Local, zone);
		DateTime target = zoneNow.Date.AddDays(1.0);
		if (s.Enabled && s.Days.Count > 0)
		{
			// Overnight shifts (e.g. 22:00-06:00) can start again later today; day shifts start on a later day.
			bool overnight = s.StartTime > s.EndTime;
			for (int i = overnight ? 0 : 1; i <= 8; i++)
			{
				DateTime candidate = zoneNow.Date.AddDays(i).Add(s.StartTime);
				if (candidate > zoneNow && s.Days.Contains(candidate.DayOfWeek))
				{
					target = candidate;
					break;
				}
			}
		}
		if (zone.IsInvalidTime(target))
		{
			target = target.AddHours(1.0);
		}
		return TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(target, DateTimeKind.Unspecified), zone, TimeZoneInfo.Local);
	}

	/// <summary>Portable copy: install for this user and hand over to the installed copy.</summary>
	private void InstallOnThisPc()
	{
		string message = "Install WinTools on this PC for " + Environment.UserName + "?\n\n" + "It copies WinTools to " + SelfInstall.InstallDir + ", adds it to the Start menu and starts it in the system tray at every sign-in. No admin rights are needed.\n\n" + "Your settings are kept.";
		if (MessageBox.Show(this, message, "Install WinTools", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
		{
			return;
		}
		try
		{
			SelfInstall.Install();
			SelfInstall.LaunchInstalled(inTray: false);
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "Install failed:\n" + ex.Message, "WinTools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}
		Quit();
	}

	private void Quit()
	{
		quitting = true;
		Close();
	}

	private void button1_Click(object sender, EventArgs e)
	{
		StartStop();
	}

	private void takeBreakButton_Click(object sender, EventArgs e)
	{
		ToggleBreak();
	}

	private void loginCheckBox_CheckedChanged(object sender, EventArgs e)
	{
		if (loading)
		{
			return;
		}
		try
		{
			LoginItem.Set(loginCheckBox.Checked);
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "Couldn't change the startup setting:\n" + ex.Message, "WinTools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			loading = true;
			loginCheckBox.Checked = LoginItem.IsEnabled;
			loading = false;
		}
	}

	// ---------- Tray / window lifetime ----------

	protected override void SetVisibleCore(bool value)
	{
		if (startInTray)
		{
			// Started with --tray (at login): create the window but keep it hidden.
			startInTray = false;
			if (!IsHandleCreated)
			{
				CreateHandle();
			}
			value = false;
		}
		base.SetVisibleCore(value);
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		if (threadsStarted)
		{
			return;
		}
		threadsStarted = true;
		new Thread(MonitorTask) { IsBackground = true, Name = "WinTools monitor" }.Start();
		if (showEvent != null)
		{
			new Thread(ShowRequestListener) { IsBackground = true, Name = "WinTools show listener" }.Start();
		}
	}

	private void ShowRequestListener()
	{
		while (!exit)
		{
			if (showEvent.WaitOne(1000) && !exit && IsHandleCreated)
			{
				BeginInvoke((MethodInvoker)ShowFromTray);
			}
		}
	}

	private void ShowFromTray()
	{
		Show();
		if (WindowState == FormWindowState.Minimized)
		{
			WindowState = FormWindowState.Normal;
		}
		Activate();
	}

	private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			if (Visible && WindowState != FormWindowState.Minimized)
			{
				Hide();
			}
			else
			{
				ShowFromTray();
			}
		}
	}

	private void trayMenu_Opening(object sender, CancelEventArgs e)
	{
		trayStartStopItem.Text = Run ? "Stop" : "Start";
		trayAlwaysActiveItem.Checked = alwaysActiveCheckBox.Checked;
		bool onBreak = OnBreak;
		trayBreakItem.Text = onBreak ? (dayEnded ? "Resume" : "End Break") : "Take a Break (" + (int)breakMinutesUpDown.Value + " min)";
		trayEndDayItem.Enabled = !onBreak;
	}

	private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
	{
		// Taskbar light/dark switch: redraw the tray glyph in the right colour.
		if (e.Category != UserPreferenceCategory.General || !IsHandleCreated)
		{
			return;
		}
		BeginInvoke((MethodInvoker)delegate
		{
			TrayIcons old = trayIcons;
			trayIcons = new TrayIcons();
			notifyIcon.Icon = trayIcons.Get(trayState);
			old.Dispose();
		});
	}

	private void Form1_FormClosing(object sender, FormClosingEventArgs e)
	{
		if (!quitting && e.CloseReason == CloseReason.UserClosing)
		{
			// The X button hides to the tray; Quit (tray menu) exits.
			e.Cancel = true;
			Hide();
			if (!trayHintShown)
			{
				trayHintShown = true;
				notifyIcon.ShowBalloonTip(4000, "WinTools is still running", "It keeps working from the system tray. Right-click the tray icon and choose Quit to exit.", ToolTipIcon.Info);
			}
			return;
		}
		SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
		PowerUtilities.Shutdown("test");
		Run = false;
		exit = true;
		wake.Set();
		if (keepAliveActive)
		{
			PowerUtilities.Shutdown();
			keepAliveActive = false;
		}
		notifyIcon.Visible = false;
	}

	// ---------- Monitor ----------

	private void MonitorTask()
	{
		while (!exit)
		{
			WorkingHoursSchedule workingHoursSchedule = currentSchedule;
			DateTime now = DateTime.Now;
			// The schedule is evaluated in the chosen time zone.
			TimeZoneInfo zone = AppSettings.ResolveZone(zoneId);
			DateTime zoneNow = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local, zone);
			bool h24 = use24Hour;
			nowText = "Now: " + AppSettings.FormatClock(zoneNow, h24) + " " + ZoneName(zone, zoneNow);
			long num = Interlocked.Read(ref breakUntilTicks);
			DateTime breakUntil = ((num > 0) ? new DateTime(num) : DateTime.MinValue);
			string breakUntilText = (num > 0) ? AppSettings.FormatClock(TimeZoneInfo.ConvertTime(breakUntil, TimeZoneInfo.Local, zone), h24) : "";
			bool aa = alwaysActive;
			bool onBreak = Run && now < breakUntil;
			breakRemaining = onBreak ? FormatRemaining(breakUntil - now) : "";
			if (!onBreak && dayEnded)
			{
				dayEnded = false;
			}
			bool active = Run && !onBreak && (aa || workingHoursSchedule.IsActive(zoneNow));
			if (active)
			{
				if (!keepAliveActive)
				{
					PowerUtilities.PreventPowerSave();
					keepAliveActive = true;
					lastKeepAlivePulse = DateTime.MinValue;
				}
				if (lastKeepAlivePulse == DateTime.MinValue || (now - lastKeepAlivePulse).TotalSeconds >= 10.0)
				{
					PowerUtilities.PreventPowerSave("test");
					lastKeepAlivePulse = now;
				}
				SetStatus(aa ? "Status: Always active" : "Status: Working hours active", "Idle Time: " + FormatIdleTime(IdleTimeFinder.GetIdleTime()), onBreak: false, TrayState.Active);
			}
			else
			{
				if (keepAliveActive)
				{
					PowerUtilities.Shutdown();
					keepAliveActive = false;
				}
				if (onBreak && dayEnded)
				{
					DateTime resume = TimeZoneInfo.ConvertTime(breakUntil, TimeZoneInfo.Local, zone);
					SetStatus("Status: Day ended - resumes " + resume.ToString("ddd ", CultureInfo.InvariantCulture) + breakUntilText, "Resumes in " + breakRemaining, onBreak: true, TrayState.Waiting);
				}
				else if (onBreak)
				{
					SetStatus("Status: On break until " + breakUntilText, "Break ends in " + breakRemaining, onBreak: true, TrayState.Waiting);
				}
				else if (Run)
				{
					SetStatus(workingHoursSchedule.Enabled ? "Status: Waiting for working hours" : "Status: Schedule disabled", workingHoursSchedule.Enabled ? "Idle Time: Outside working hours" : "Idle Time: Schedule disabled", onBreak: false, TrayState.Waiting);
				}
				else
				{
					SetStatus("Status: Stopped", "Idle Time: ", onBreak: false, TrayState.Stopped);
				}
			}
			wake.WaitOne(1000);
		}
	}

	private void SetStatus(string status, string idle, bool onBreak, TrayState state)
	{
		if (InvokeRequired)
		{
			if (IsHandleCreated && !exit)
			{
				BeginInvoke((MethodInvoker)delegate
				{
					SetStatus(status, idle, onBreak, state);
				});
			}
			return;
		}
		Color color = (state == TrayState.Active) ? Color.ForestGreen : Color.Red;
		((Control)Status_label).Text = status;
		((Control)Status_label).ForeColor = color;
		((Control)IdleTime_label).Text = idle;
		((Control)IdleTime_label).ForeColor = color;
		((Control)button1).Text = Run ? "Stop" : "Start";
		((Control)takeBreakButton).Text = onBreak ? ((dayEnded ? "Resume" : "End Break") + " (" + breakRemaining + ")") : "Take a Break";
		((Control)endDayButton).Enabled = !onBreak;
		((Control)nowLabel).Text = nowText;
		string tip = onBreak ? ("WinTools - " + (dayEnded ? "day ended, resumes in " : "on break, ") + breakRemaining + (dayEnded ? "" : " left")) : ("WinTools - " + status.Replace("Status: ", ""));
		notifyIcon.Text = (tip.Length > 63) ? tip.Substring(0, 63) : tip;
		if (state != trayState || notifyIcon.Icon == null)
		{
			trayState = state;
			notifyIcon.Icon = trayIcons.Get(state);
			((Form)this).Icon = windowIcons.Get(state);
		}
	}

	/// <summary>Countdown: "m:ss" under an hour, "h:mm:ss" otherwise.</summary>
	private static string FormatRemaining(TimeSpan left)
	{
		if (left < TimeSpan.Zero)
		{
			left = TimeSpan.Zero;
		}
		// Round up so it never shows 0:00 while the break is still running.
		long secs = (long)Math.Ceiling(left.TotalSeconds);
		if (secs >= 86400)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}d {1}:{2:00}:{3:00}", secs / 86400, secs % 86400 / 3600, secs % 3600 / 60, secs % 60);
		}
		return (secs >= 3600) ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", secs / 3600, secs % 3600 / 60, secs % 60) : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", secs / 60, secs % 60);
	}

	private static string FormatIdleTime(uint idleMilliseconds)
	{
		return TimeSpan.FromMilliseconds(idleMilliseconds).ToString(@"d\.hh\:mm\:ss");
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			components?.Dispose();
			notifyIcon?.Dispose();
			trayIcons?.Dispose();
			windowIcons.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		components = new Container();
		button1 = new Button();
		Status_label = new Label();
		IdleTime_label = new Label();
		alwaysActiveCheckBox = new CheckBox();
		breakMinutesUpDown = new NumericUpDown();
		breakMinutesLabel = new Label();
		takeBreakButton = new Button();
		endDayButton = new Button();
		scheduleGroup = new GroupBox();
		scheduleEnabledCheckBox = new CheckBox();
		daysCheckedListBox = new CheckedListBox();
		startTimePicker = new DateTimePicker();
		endTimePicker = new DateTimePicker();
		weekdaysButton = new Button();
		allDaysButton = new Button();
		clearDaysButton = new Button();
		scheduleSummaryLabel = new Label();
		loginCheckBox = new CheckBox();
		startOnLaunchCheckBox = new CheckBox();
		use24HourCheckBox = new CheckBox();
		nowLabel = new Label();
		timeZoneLabel = new Label();
		timeZoneComboBox = new ComboBox();
		notifyIcon = new NotifyIcon(components);
		trayMenu = new ContextMenuStrip(components);
		trayOpenItem = new ToolStripMenuItem();
		trayStartStopItem = new ToolStripMenuItem();
		trayAlwaysActiveItem = new ToolStripMenuItem();
		trayBreakItem = new ToolStripMenuItem();
		trayEndDayItem = new ToolStripMenuItem();
		trayInstallItem = new ToolStripMenuItem();
		installButton = new Button();
		trayQuitItem = new ToolStripMenuItem();
		((Control)this).SuspendLayout();
		((Control)button1).Location = new Point(16, 16);
		((Control)button1).Name = "button1";
		((Control)button1).Size = new Size(120, 34);
		((Control)button1).TabIndex = 0;
		((Control)button1).Text = "Start";
		((ButtonBase)button1).UseVisualStyleBackColor = true;
		((Control)button1).Click += button1_Click;
		((Control)Status_label).AutoSize = true;
		((Control)Status_label).Font = new Font("Consolas", 9.75f, (FontStyle)0, (GraphicsUnit)3, (byte)0);
		((Control)Status_label).ForeColor = Color.Red;
		((Control)Status_label).Location = new Point(152, 22);
		((Control)Status_label).Name = "Status_label";
		((Control)Status_label).Size = new Size(71, 15);
		((Control)Status_label).TabIndex = 1;
		((Control)Status_label).Text = "Status: Stopped";
		Status_label.TextAlign = (ContentAlignment)4;
		((Control)IdleTime_label).AutoSize = true;
		((Control)IdleTime_label).Font = new Font("Consolas", 9.75f, (FontStyle)0, (GraphicsUnit)3, (byte)0);
		((Control)IdleTime_label).ForeColor = Color.Red;
		((Control)IdleTime_label).Location = new Point(16, 60);
		((Control)IdleTime_label).Name = "IdleTime_label";
		((Control)IdleTime_label).Size = new Size(71, 15);
		((Control)IdleTime_label).TabIndex = 2;
		((Control)IdleTime_label).Text = "Idle Time: ";
		IdleTime_label.TextAlign = (ContentAlignment)4;
		((Control)alwaysActiveCheckBox).AutoSize = true;
		((Control)alwaysActiveCheckBox).Location = new Point(16, 86);
		((Control)alwaysActiveCheckBox).Name = "alwaysActiveCheckBox";
		((Control)alwaysActiveCheckBox).TabIndex = 3;
		((Control)alwaysActiveCheckBox).Text = "Always active (ignore schedule)";
		((ButtonBase)alwaysActiveCheckBox).UseVisualStyleBackColor = true;
		alwaysActiveCheckBox.CheckedChanged += delegate
		{
			if (!loading)
			{
				SetAlwaysActive(alwaysActiveCheckBox.Checked);
			}
		};
		((Control)scheduleGroup).Controls.Add((Control)(object)scheduleEnabledCheckBox);
		((Control)scheduleGroup).Controls.Add((Control)(object)daysCheckedListBox);
		((Control)scheduleGroup).Controls.Add((Control)(object)startTimePicker);
		((Control)scheduleGroup).Controls.Add((Control)(object)endTimePicker);
		((Control)scheduleGroup).Controls.Add((Control)(object)weekdaysButton);
		((Control)scheduleGroup).Controls.Add((Control)(object)allDaysButton);
		((Control)scheduleGroup).Controls.Add((Control)(object)clearDaysButton);
		((Control)scheduleGroup).Controls.Add((Control)(object)scheduleSummaryLabel);
		((Control)scheduleGroup).Controls.Add((Control)(object)nowLabel);
		((Control)scheduleGroup).Controls.Add((Control)(object)timeZoneLabel);
		((Control)scheduleGroup).Controls.Add((Control)(object)timeZoneComboBox);
		((Control)scheduleGroup).Location = new Point(16, 116);
		((Control)scheduleGroup).Name = "scheduleGroup";
		((Control)scheduleGroup).Size = new Size(520, 222);
		((Control)scheduleGroup).TabIndex = 4;
		scheduleGroup.TabStop = false;
		((Control)scheduleGroup).Text = "Working Hours Schedule";
		((Control)scheduleEnabledCheckBox).AutoSize = true;
		((Control)scheduleEnabledCheckBox).Location = new Point(16, 28);
		((Control)scheduleEnabledCheckBox).Name = "scheduleEnabledCheckBox";
		((Control)scheduleEnabledCheckBox).Size = new Size(235, 19);
		((Control)scheduleEnabledCheckBox).TabIndex = 0;
		((Control)scheduleEnabledCheckBox).Text = "Enable working-hours schedule";
		((ButtonBase)scheduleEnabledCheckBox).UseVisualStyleBackColor = true;
		scheduleEnabledCheckBox.Checked = true;
		scheduleEnabledCheckBox.CheckedChanged += delegate
		{
			ApplyScheduleFromUi();
		};
		daysCheckedListBox.CheckOnClick = true;
		daysCheckedListBox.FormattingEnabled = true;
		((Control)daysCheckedListBox).Location = new Point(16, 56);
		((Control)daysCheckedListBox).Name = "daysCheckedListBox";
		((Control)daysCheckedListBox).Size = new Size(128, 100);
		((Control)daysCheckedListBox).TabIndex = 1;
		daysCheckedListBox.Items.Add(DayOfWeek.Monday, true);
		daysCheckedListBox.Items.Add(DayOfWeek.Tuesday, true);
		daysCheckedListBox.Items.Add(DayOfWeek.Wednesday, true);
		daysCheckedListBox.Items.Add(DayOfWeek.Thursday, true);
		daysCheckedListBox.Items.Add(DayOfWeek.Friday, true);
		daysCheckedListBox.Items.Add(DayOfWeek.Saturday, false);
		daysCheckedListBox.Items.Add(DayOfWeek.Sunday, false);
		daysCheckedListBox.ItemCheck += delegate
		{
			// ItemCheck fires before the item changes; apply once it has.
			if (!loading && IsHandleCreated)
			{
				BeginInvoke((Delegate)(MethodInvoker)ApplyScheduleFromUi);
			}
		};
		startTimePicker.CustomFormat = "HH:mm";
		startTimePicker.Format = DateTimePickerFormat.Custom;
		((Control)startTimePicker).Location = new Point(192, 56);
		((Control)startTimePicker).Name = "startTimePicker";
		startTimePicker.ShowUpDown = true;
		((Control)startTimePicker).Size = new Size(96, 23);
		((Control)startTimePicker).TabIndex = 2;
		startTimePicker.Value = DateTime.Today.AddHours(8.0);
		startTimePicker.ValueChanged += delegate
		{
			ApplyScheduleFromUi();
		};
		endTimePicker.CustomFormat = "HH:mm";
		endTimePicker.Format = DateTimePickerFormat.Custom;
		((Control)endTimePicker).Location = new Point(192, 92);
		((Control)endTimePicker).Name = "endTimePicker";
		endTimePicker.ShowUpDown = true;
		((Control)endTimePicker).Size = new Size(96, 23);
		((Control)endTimePicker).TabIndex = 3;
		endTimePicker.Value = DateTime.Today.AddHours(17.0);
		endTimePicker.ValueChanged += delegate
		{
			ApplyScheduleFromUi();
		};
		((Control)weekdaysButton).Location = new Point(310, 56);
		((Control)weekdaysButton).Name = "weekdaysButton";
		((Control)weekdaysButton).Size = new Size(88, 24);
		((Control)weekdaysButton).TabIndex = 4;
		((Control)weekdaysButton).Text = "Weekdays";
		((ButtonBase)weekdaysButton).UseVisualStyleBackColor = true;
		((Control)weekdaysButton).Click += delegate
		{
			SelectDays(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday);
		};
		((Control)allDaysButton).Location = new Point(404, 56);
		((Control)allDaysButton).Name = "allDaysButton";
		((Control)allDaysButton).Size = new Size(88, 24);
		((Control)allDaysButton).TabIndex = 5;
		((Control)allDaysButton).Text = "All Days";
		((ButtonBase)allDaysButton).UseVisualStyleBackColor = true;
		((Control)allDaysButton).Click += delegate
		{
			SelectDays(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday);
		};
		((Control)clearDaysButton).Location = new Point(310, 92);
		((Control)clearDaysButton).Name = "clearDaysButton";
		((Control)clearDaysButton).Size = new Size(88, 24);
		((Control)clearDaysButton).TabIndex = 6;
		((Control)clearDaysButton).Text = "Clear";
		((ButtonBase)clearDaysButton).UseVisualStyleBackColor = true;
		((Control)clearDaysButton).Click += delegate
		{
			SelectDays(Array.Empty<DayOfWeek>());
		};
		((Control)scheduleSummaryLabel).AutoSize = true;
		((Control)scheduleSummaryLabel).Location = new Point(192, 128);
		((Control)scheduleSummaryLabel).Name = "scheduleSummaryLabel";
		((Control)scheduleSummaryLabel).Size = new Size(114, 15);
		((Control)scheduleSummaryLabel).TabIndex = 7;
		((Control)scheduleSummaryLabel).Text = "Schedule: ";
		((Control)breakMinutesLabel).AutoSize = true;
		((Control)breakMinutesLabel).Location = new Point(16, 358);
		((Control)breakMinutesLabel).Name = "breakMinutesLabel";
		((Control)breakMinutesLabel).Size = new Size(89, 15);
		((Control)breakMinutesLabel).TabIndex = 5;
		((Control)breakMinutesLabel).Text = "Break minutes:";
		((Control)breakMinutesUpDown).Location = new Point(112, 356);
		((Control)breakMinutesUpDown).Name = "breakMinutesUpDown";
		breakMinutesUpDown.Minimum = 1m;
		breakMinutesUpDown.Maximum = 240m;
		breakMinutesUpDown.Value = 15m;
		((Control)breakMinutesUpDown).Size = new Size(72, 23);
		((Control)breakMinutesUpDown).TabIndex = 6;
		breakMinutesUpDown.ValueChanged += delegate
		{
			SaveSettings();
		};
		((Control)takeBreakButton).Location = new Point(200, 354);
		((Control)takeBreakButton).Name = "takeBreakButton";
		((Control)takeBreakButton).Size = new Size(150, 28);
		((Control)takeBreakButton).TabIndex = 7;
		((Control)takeBreakButton).Text = "Take a Break";
		((ButtonBase)takeBreakButton).UseVisualStyleBackColor = true;
		((Control)takeBreakButton).Click += takeBreakButton_Click;
		((Control)endDayButton).Location = new Point(358, 354);
		((Control)endDayButton).Name = "endDayButton";
		((Control)endDayButton).Size = new Size(110, 28);
		((Control)endDayButton).TabIndex = 8;
		((Control)endDayButton).Text = "End Day";
		((ButtonBase)endDayButton).UseVisualStyleBackColor = true;
		((Control)endDayButton).Click += delegate
		{
			EndDay();
		};
		((Control)loginCheckBox).AutoSize = true;
		((Control)loginCheckBox).Location = new Point(16, 398);
		((Control)loginCheckBox).Name = "loginCheckBox";
		((Control)loginCheckBox).TabIndex = 8;
		((Control)loginCheckBox).Text = "Open WinTools at sign-in (runs in the system tray)";
		((ButtonBase)loginCheckBox).UseVisualStyleBackColor = true;
		loginCheckBox.CheckedChanged += loginCheckBox_CheckedChanged;
		((Control)startOnLaunchCheckBox).AutoSize = true;
		((Control)startOnLaunchCheckBox).Location = new Point(16, 422);
		((Control)startOnLaunchCheckBox).Name = "startOnLaunchCheckBox";
		((Control)startOnLaunchCheckBox).TabIndex = 9;
		((Control)startOnLaunchCheckBox).Text = "Start keep-alive when WinTools launches";
		((ButtonBase)startOnLaunchCheckBox).UseVisualStyleBackColor = true;
		startOnLaunchCheckBox.CheckedChanged += delegate
		{
			SaveSettings();
		};
		((Control)nowLabel).AutoSize = true;
		((Control)nowLabel).Location = new Point(192, 150);
		((Control)nowLabel).Name = "nowLabel";
		((Control)nowLabel).ForeColor = SystemColors.GrayText;
		((Control)nowLabel).TabIndex = 8;
		((Control)timeZoneLabel).AutoSize = true;
		((Control)timeZoneLabel).Location = new Point(16, 186);
		((Control)timeZoneLabel).Name = "timeZoneLabel";
		((Control)timeZoneLabel).TabIndex = 9;
		((Control)timeZoneLabel).Text = "Time zone:";
		timeZoneComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		timeZoneComboBox.DropDownWidth = 470;
		timeZoneComboBox.MaxDropDownItems = 20;
		((Control)timeZoneComboBox).Location = new Point(92, 182);
		((Control)timeZoneComboBox).Name = "timeZoneComboBox";
		((Control)timeZoneComboBox).Size = new Size(412, 23);
		((Control)timeZoneComboBox).TabIndex = 10;
		timeZoneComboBox.SelectedIndexChanged += timeZoneComboBox_SelectedIndexChanged;
		((Control)use24HourCheckBox).AutoSize = true;
		((Control)use24HourCheckBox).Location = new Point(16, 446);
		((Control)use24HourCheckBox).Name = "use24HourCheckBox";
		((Control)use24HourCheckBox).TabIndex = 10;
		((Control)use24HourCheckBox).Text = "Use 24-hour clock (13:30 instead of 1:30 PM)";
		((ButtonBase)use24HourCheckBox).UseVisualStyleBackColor = true;
		use24HourCheckBox.Checked = true;
		use24HourCheckBox.CheckedChanged += use24HourCheckBox_CheckedChanged;
		trayOpenItem.Text = "Open WinTools";
		trayOpenItem.Font = new Font(trayOpenItem.Font, FontStyle.Bold);
		trayOpenItem.Click += delegate
		{
			ShowFromTray();
		};
		trayStartStopItem.Text = "Start";
		trayStartStopItem.Click += delegate
		{
			StartStop();
		};
		trayAlwaysActiveItem.Text = "Always active (ignore schedule)";
		trayAlwaysActiveItem.Click += delegate
		{
			alwaysActiveCheckBox.Checked = !alwaysActiveCheckBox.Checked;
		};
		trayBreakItem.Text = "Take a Break";
		trayBreakItem.Click += delegate
		{
			ToggleBreak();
		};
		trayEndDayItem.Text = "End Day";
		trayEndDayItem.Click += delegate
		{
			EndDay();
		};
		trayInstallItem.Text = "Install on this PC...";
		trayInstallItem.Visible = !SelfInstall.IsRunningInstalled;
		trayInstallItem.Click += delegate
		{
			ShowFromTray();
			InstallOnThisPc();
		};
		((Control)installButton).Location = new Point(386, 418);
		((Control)installButton).Name = "installButton";
		((Control)installButton).Size = new Size(150, 28);
		((Control)installButton).TabIndex = 11;
		((Control)installButton).Text = "Install on this PC...";
		((ButtonBase)installButton).UseVisualStyleBackColor = true;
		((Control)installButton).Visible = !SelfInstall.IsRunningInstalled;
		((Control)installButton).Click += delegate
		{
			InstallOnThisPc();
		};
		trayQuitItem.Text = "Quit WinTools";
		trayQuitItem.Click += delegate
		{
			Quit();
		};
		trayMenu.Items.AddRange(new ToolStripItem[9]
		{
			trayOpenItem,
			new ToolStripSeparator(),
			trayStartStopItem,
			trayAlwaysActiveItem,
			trayBreakItem,
			trayEndDayItem,
			new ToolStripSeparator(),
			trayInstallItem,
			trayQuitItem
		});
		trayMenu.Opening += trayMenu_Opening;
		notifyIcon.ContextMenuStrip = trayMenu;
		notifyIcon.Text = "WinTools";
		notifyIcon.Icon = trayIcons.Get(TrayState.Stopped);
		notifyIcon.Visible = true;
		notifyIcon.MouseClick += notifyIcon_MouseClick;
		((ContainerControl)this).AutoScaleDimensions = new SizeF(7f, 15f);
		((ContainerControl)this).AutoScaleMode = AutoScaleMode.Font;
		((Control)this).Font = new Font("Segoe UI", 9f);
		((Form)this).ClientSize = new Size(552, 480);
		((Control)this).Controls.Add((Control)(object)use24HourCheckBox);
		((Control)this).Controls.Add((Control)(object)startOnLaunchCheckBox);
		((Control)this).Controls.Add((Control)(object)loginCheckBox);
		((Control)this).Controls.Add((Control)(object)installButton);
		((Control)this).Controls.Add((Control)(object)endDayButton);
		((Control)this).Controls.Add((Control)(object)takeBreakButton);
		((Control)this).Controls.Add((Control)(object)breakMinutesUpDown);
		((Control)this).Controls.Add((Control)(object)breakMinutesLabel);
		((Control)this).Controls.Add((Control)(object)scheduleGroup);
		((Control)this).Controls.Add((Control)(object)alwaysActiveCheckBox);
		((Control)this).Controls.Add((Control)(object)IdleTime_label);
		((Control)this).Controls.Add((Control)(object)Status_label);
		((Control)this).Controls.Add((Control)(object)button1);
		((Form)this).FormBorderStyle = FormBorderStyle.FixedSingle;
		((Form)this).MaximizeBox = false;
		((Form)this).FormClosing += new FormClosingEventHandler(Form1_FormClosing);
		((Control)this).Name = "Form1";
		((Control)this).Text = SelfInstall.IsRunningInstalled ? "WinTools" : "WinTools (portable)";
		((Form)this).Icon = windowIcons.Get(TrayState.Stopped);
		((Control)this).ResumeLayout(false);
		((Control)this).PerformLayout();
	}
}
