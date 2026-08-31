using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using KeepAlive;

namespace WinTools;

public class Form1 : Form
{
	private bool Run = false;

	private bool exit = false;

	private string status = "WinTools is currently Stopped.";

	private IContainer components = null;

	private Button button1;

	private Label Status_label;

	private Label IdleTime_label;

	public Form1()
	{
		InitializeComponent();
		((Control)Status_label).Text = "Status: Stopped";
		((Control)Status_label).Update();
		((Control)IdleTime_label).Text = "Idle Time: ";
		((Control)IdleTime_label).Update();
		Thread thread = new Thread(ThreadTask2);
		thread.IsBackground = true;
		thread.Start();
	}

	private void update_Label(Label label, string string1)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		if (((Control)label).InvokeRequired)
		{
			((Control)label).BeginInvoke((Delegate)(MethodInvoker)delegate
			{
				((Control)label).Text = string1;
			});
		}
		else
		{
			((Control)label).Text = string1;
		}
	}

	private void button1_Click(object sender, EventArgs e)
	{
		Run = !Run;
		if (Run && !exit)
		{
			Thread thread = new Thread(ThreadTask);
			thread.IsBackground = true;
			thread.Start();
			update_Label(Status_label, "Status: Started");
		}
	}

	private void Form1_FormClosing(object sender, FormClosingEventArgs e)
	{
		PowerUtilities.Shutdown("test");
		Run = false;
		exit = true;
	}

	private void ThreadTask()
	{
		PowerUtilities.PreventPowerSave();
		while (Run)
		{
			PowerUtilities.PreventPowerSave("test");
			update_Label(Status_label, "Status: Started");
			Thread.Sleep(10000);
		}
		PowerUtilities.Shutdown();
		update_Label(Status_label, "Status: Aborted");
	}

	private void ThreadTask2()
	{
		while (!exit)
		{
			update_Label(IdleTime_label, "Idle Time: " + IdleTimeFinder.GetIdleTime());
			Thread.Sleep(1000);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		((Form)this).Dispose(disposing);
	}

	private void InitializeComponent()
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected O, but got Unknown
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected O, but got Unknown
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Expected O, but got Unknown
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Expected O, but got Unknown
		//IL_020c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_030e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0318: Expected O, but got Unknown
		button1 = new Button();
		Status_label = new Label();
		IdleTime_label = new Label();
		((Control)this).SuspendLayout();
		((Control)button1).Anchor = (AnchorStyles)10;
		((Control)button1).AutoSize = true;
		((Control)button1).Location = new Point(122, 47);
		((Control)button1).Margin = new Padding(4, 5, 4, 5);
		((Control)button1).Name = "button1";
		((Control)button1).Size = new Size(112, 35);
		((Control)button1).TabIndex = 0;
		((Control)button1).Text = "Start/Stop";
		((ButtonBase)button1).UseVisualStyleBackColor = true;
		((Control)button1).Click += button1_Click;
		((Control)Status_label).Anchor = (AnchorStyles)10;
		((Control)Status_label).AutoSize = true;
		((Control)Status_label).Font = new Font("Consolas", 9.75f, (FontStyle)0, (GraphicsUnit)3, (byte)0);
		((Control)Status_label).ForeColor = Color.Red;
		((Control)Status_label).Location = new Point(66, 87);
		((Control)Status_label).Margin = new Padding(4, 0, 4, 0);
		((Control)Status_label).Name = "Status_label";
		((Control)Status_label).Size = new Size(175, 23);
		((Control)Status_label).TabIndex = 10001;
		((Control)Status_label).Text = "Status: Stopped";
		Status_label.TextAlign = (ContentAlignment)4;
		((Control)IdleTime_label).Anchor = (AnchorStyles)10;
		((Control)IdleTime_label).AutoSize = true;
		((Control)IdleTime_label).Font = new Font("Consolas", 9.75f, (FontStyle)0, (GraphicsUnit)3, (byte)0);
		((Control)IdleTime_label).ForeColor = Color.Red;
		((Control)IdleTime_label).Location = new Point(69, 19);
		((Control)IdleTime_label).Margin = new Padding(4, 0, 4, 0);
		((Control)IdleTime_label).Name = "IdleTime_label";
		((Control)IdleTime_label).Size = new Size(131, 23);
		((Control)IdleTime_label).TabIndex = 10002;
		((Control)IdleTime_label).Text = "Idle Time: ";
		IdleTime_label.TextAlign = (ContentAlignment)4;
		((ContainerControl)this).AutoScaleDimensions = new SizeF(9f, 20f);
		((ContainerControl)this).AutoScaleMode = (AutoScaleMode)1;
		((Control)this).AutoSize = true;
		((Form)this).ClientSize = new Size(320, 124);
		((Control)this).Controls.Add((Control)(object)IdleTime_label);
		((Control)this).Controls.Add((Control)(object)Status_label);
		((Control)this).Controls.Add((Control)(object)button1);
		((Form)this).Margin = new Padding(4, 5, 4, 5);
		((Control)this).Name = "Form1";
		((Control)this).Text = "Form1";
		((Form)this).FormClosing += new FormClosingEventHandler(Form1_FormClosing);
		((Control)this).ResumeLayout(false);
		((Control)this).PerformLayout();
	}
}
