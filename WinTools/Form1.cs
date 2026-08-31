using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using KeepAlive;

namespace WinTools;

public class Form1 : Form
{
	private volatile bool Run;

	private volatile bool exit;

	private volatile WorkingHoursSchedule currentSchedule = WorkingHoursSchedule.Default;

	private volatile bool keepAliveActive;

	private DateTime lastKeepAlivePulse = DateTime.MinValue;

	private IContainer components = null;

	private Button button1;

	private Label Status_label;

	private Label IdleTime_label;

	private GroupBox scheduleGroup;

	private CheckBox scheduleEnabledCheckBox;

	private CheckedListBox daysCheckedListBox;

	private DateTimePicker startTimePicker;

	private DateTimePicker endTimePicker;

	private Button weekdaysButton;

	private Button allDaysButton;

	private Button clearDaysButton;

	private Label scheduleSummaryLabel;

	public Form1()
	{
		InitializeComponent();
		ApplyScheduleFromUi();
		UpdateLabel(Status_label, "Status: Stopped");
		UpdateLabel(IdleTime_label, "Idle Time: ");
		UpdateScheduleSummary();
		Thread thread = new Thread(MonitorTask);
		thread.IsBackground = true;
		thread.Start();
	}

	private void UpdateLabel(Label label, string text)
	{
		if (((Control)label).InvokeRequired)
		{
			((Control)label).BeginInvoke((Delegate)(MethodInvoker)delegate
			{
				((Control)label).Text = text;
			});
		}
		else
		{
			((Control)label).Text = text;
		}
	}

	private void UpdateScheduleSummary()
	{
		UpdateLabel(scheduleSummaryLabel, "Schedule: " + currentSchedule.Describe());
	}

	private void ApplyScheduleFromUi()
	{
		DayOfWeek[] array = new DayOfWeek[daysCheckedListBox.Items.Count];
		for (int i = 0; i < daysCheckedListBox.Items.Count; i++)
		{
			array[i] = (DayOfWeek)daysCheckedListBox.Items[i];
		}
		System.Collections.Generic.List<DayOfWeek> list = new System.Collections.Generic.List<DayOfWeek>();
		for (int j = 0; j < array.Length; j++)
		{
			if (daysCheckedListBox.GetItemChecked(j))
			{
				list.Add(array[j]);
			}
		}
		currentSchedule = new WorkingHoursSchedule(scheduleEnabledCheckBox.Checked, list, startTimePicker.Value.TimeOfDay, endTimePicker.Value.TimeOfDay);
		UpdateScheduleSummary();
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

	private void button1_Click(object sender, EventArgs e)
	{
		Run = !Run;
		if (!Run && keepAliveActive)
		{
			PowerUtilities.Shutdown();
			keepAliveActive = false;
		}
		UpdateLabel(Status_label, Run ? "Status: Started" : "Status: Stopped");
		if (!Run)
		{
			UpdateLabel(IdleTime_label, "Idle Time: ");
		}
	}

	private void Form1_FormClosing(object sender, FormClosingEventArgs e)
	{
		PowerUtilities.Shutdown("test");
		Run = false;
		exit = true;
		if (keepAliveActive)
		{
			PowerUtilities.Shutdown();
			keepAliveActive = false;
		}
	}

	private void MonitorTask()
	{
		while (!exit)
		{
			WorkingHoursSchedule workingHoursSchedule = currentSchedule;
			DateTime now = DateTime.Now;
			bool flag = Run && workingHoursSchedule.IsActive(now);
			if (flag)
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
				UpdateLabel(Status_label, "Status: Working hours active");
				UpdateLabel(IdleTime_label, "Idle Time: " + FormatIdleTime(IdleTimeFinder.GetIdleTime()));
			}
			else
			{
				if (keepAliveActive)
				{
					PowerUtilities.Shutdown();
					keepAliveActive = false;
				}
				UpdateLabel(Status_label, Run ? (workingHoursSchedule.Enabled ? "Status: Waiting for working hours" : "Status: Schedule disabled") : "Status: Stopped");
				UpdateLabel(IdleTime_label, Run ? (workingHoursSchedule.Enabled ? "Idle Time: Outside working hours" : "Idle Time: Schedule disabled") : "Idle Time: ");
			}
			Thread.Sleep(1000);
		}
	}

	private static string FormatIdleTime(uint idleMilliseconds)
	{
		return TimeSpan.FromMilliseconds(idleMilliseconds).ToString(@"d\.hh\:mm\:ss");
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		button1 = new Button();
		Status_label = new Label();
		IdleTime_label = new Label();
		scheduleGroup = new GroupBox();
		scheduleEnabledCheckBox = new CheckBox();
		daysCheckedListBox = new CheckedListBox();
		startTimePicker = new DateTimePicker();
		endTimePicker = new DateTimePicker();
		weekdaysButton = new Button();
		allDaysButton = new Button();
		clearDaysButton = new Button();
		scheduleSummaryLabel = new Label();
		((Control)this).SuspendLayout();
		((Control)button1).Location = new Point(16, 16);
		((Control)button1).Name = "button1";
		((Control)button1).Size = new Size(120, 34);
		((Control)button1).TabIndex = 0;
		((Control)button1).Text = "Start / Stop";
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
		((Control)scheduleGroup).Controls.Add((Control)(object)scheduleEnabledCheckBox);
		((Control)scheduleGroup).Controls.Add((Control)(object)daysCheckedListBox);
		((Control)scheduleGroup).Controls.Add((Control)(object)startTimePicker);
		((Control)scheduleGroup).Controls.Add((Control)(object)endTimePicker);
		((Control)scheduleGroup).Controls.Add((Control)(object)weekdaysButton);
		((Control)scheduleGroup).Controls.Add((Control)(object)allDaysButton);
		((Control)scheduleGroup).Controls.Add((Control)(object)clearDaysButton);
		((Control)scheduleGroup).Controls.Add((Control)(object)scheduleSummaryLabel);
		((Control)scheduleGroup).Location = new Point(16, 92);
		((Control)scheduleGroup).Name = "scheduleGroup";
		((Control)scheduleGroup).Size = new Size(520, 180);
		((Control)scheduleGroup).TabIndex = 3;
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
			BeginInvoke((Delegate)(MethodInvoker)ApplyScheduleFromUi);
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
		((ContainerControl)this).AutoScaleDimensions = new SizeF(7f, 15f);
		((ContainerControl)this).AutoScaleMode = AutoScaleMode.Font;
		((Form)this).ClientSize = new Size(552, 288);
		((Control)this).Controls.Add((Control)(object)scheduleGroup);
		((Control)this).Controls.Add((Control)(object)IdleTime_label);
		((Control)this).Controls.Add((Control)(object)Status_label);
		((Control)this).Controls.Add((Control)(object)button1);
		((Form)this).FormClosing += new FormClosingEventHandler(Form1_FormClosing);
		((Control)this).Name = "Form1";
		((Control)this).Text = "WinTools";
		((Control)this).ResumeLayout(false);
		((Control)this).PerformLayout();
	}
}
