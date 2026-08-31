using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace KeepAlive;

public static class PowerUtilities
{
	[Flags]
	public enum EXECUTION_STATE : uint
	{
		ES_AWAYMODE_REQUIRED = 0x40u,
		ES_CONTINUOUS = 0x80000000u,
		ES_DISPLAY_REQUIRED = 2u,
		ES_SYSTEM_REQUIRED = 1u
	}

	private static AutoResetEvent _event = new AutoResetEvent(initialState: false);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

	public static void PreventPowerSave()
	{
		new TaskFactory().StartNew(delegate
		{
			if (SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_SYSTEM_REQUIRED) == (EXECUTION_STATE)0u)
			{
				throw new Win32Exception();
			}
			_event.WaitOne();
		}, TaskCreationOptions.LongRunning);
	}

	public static void PreventPowerSave(string arg)
	{
		if (arg == "test")
		{
			if (SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_SYSTEM_REQUIRED) == (EXECUTION_STATE)0u)
			{
				throw new Win32Exception();
			}
			SendInputClass.SendInput((uint)TestInput.inputs.Length, TestInput.inputs, Marshal.SizeOf(typeof(SendInputClass.INPUT)));
		}
	}

	public static void Shutdown()
	{
		_event.Set();
	}

	public static void Shutdown(string arg)
	{
		if (arg == "test")
		{
			SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
		}
	}
}
