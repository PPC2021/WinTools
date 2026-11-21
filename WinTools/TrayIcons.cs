using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WinTools;

public enum TrayState
{
	/// <summary>Keep-alive is off (snooze moon).</summary>
	Stopped,
	/// <summary>Running, but outside working hours or on a break (outline cup).</summary>
	Waiting,
	/// <summary>Actively keeping the PC awake (filled, steaming cup).</summary>
	Active
}

/// <summary>Draws the tray glyphs (same states as MacTools' menu-bar icon).</summary>
public static class TrayGlyphs
{
	public static Bitmap Render(TrayState state, int size, Color ink)
	{
		Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
		using Graphics g = Graphics.FromImage(bmp);
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.PixelOffsetMode = PixelOffsetMode.HighQuality;
		g.Clear(Color.Transparent);
		g.ScaleTransform(size / 16f, size / 16f);
		switch (state)
		{
		case TrayState.Stopped:
			DrawMoon(g, ink);
			break;
		case TrayState.Waiting:
			DrawCup(g, ink, filled: false);
			break;
		default:
			DrawCup(g, ink, filled: true);
			break;
		}
		return bmp;
	}

	/// <summary>
	/// App/window icon: the state glyph in white on a rounded tile
	/// (slate when stopped, blue when running).
	/// </summary>
	public static Bitmap RenderTile(TrayState state, int size)
	{
		Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
		using Graphics g = Graphics.FromImage(bmp);
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.PixelOffsetMode = PixelOffsetMode.HighQuality;
		g.Clear(Color.Transparent);
		float k = size / 256f;
		float m = Math.Max(0.5f, 8f * k);
		float r = 52f * k;
		using GraphicsPath tile = new GraphicsPath();
		float w = size - 2 * m;
		float d = 2 * r;
		tile.AddArc(m, m, d, d, 180f, 90f);
		tile.AddArc(m + w - d, m, d, d, 270f, 90f);
		tile.AddArc(m + w - d, m + w - d, d, d, 0f, 90f);
		tile.AddArc(m, m + w - d, d, d, 90f, 90f);
		tile.CloseFigure();
		Color top = (state == TrayState.Stopped) ? Color.FromArgb(255, 112, 128, 150) : Color.FromArgb(255, 47, 128, 237);
		Color bottom = (state == TrayState.Stopped) ? Color.FromArgb(255, 58, 68, 84) : Color.FromArgb(255, 18, 60, 150);
		using (LinearGradientBrush brush = new LinearGradientBrush(new PointF(0f, 0f), new PointF(0f, size), top, bottom))
		{
			g.FillPath(brush, tile);
		}
		if (size >= 32)
		{
			using LinearGradientBrush shine = new LinearGradientBrush(new PointF(0f, 0f), new PointF(0f, size * 0.5f), Color.FromArgb(55, 255, 255, 255), Color.FromArgb(0, 255, 255, 255));
			g.FillRectangle(shine, m, m, w, w * 0.45f);
		}
		// Glyph in the middle of the tile; small sizes get a bigger glyph so it stays readable.
		float inset = (size <= 24) ? 2.5f * size / 32f : 5.5f * size / 32f;
		float gs = size - 2 * inset;
		GraphicsState saved = g.Save();
		g.TranslateTransform(inset, inset);
		g.ScaleTransform(gs / 16f, gs / 16f);
		switch (state)
		{
		case TrayState.Stopped:
			DrawMoon(g, Color.White);
			break;
		case TrayState.Waiting:
			DrawCup(g, Color.White, filled: false);
			break;
		default:
			DrawCup(g, Color.White, filled: true);
			break;
		}
		g.Restore(saved);
		return bmp;
	}

	private static void DrawCup(Graphics g, Color ink, bool filled)
	{
		using Pen pen = new Pen(ink, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
		using GraphicsPath body = new GraphicsPath();
		// Cup body: flat top, rounded bottom.
		body.AddLine(1.75f, 6.25f, 11.25f, 6.25f);
		body.AddLine(11.25f, 6.25f, 11.25f, 9.5f);
		body.AddArc(7.25f, 5.5f, 4f, 7.5f, 0f, 90f);
		body.AddLine(9.25f, 13.25f, 3.75f, 13.25f);
		body.AddArc(1.75f, 5.5f, 4f, 7.5f, 90f, 90f);
		body.CloseFigure();
		if (filled)
		{
			using SolidBrush brush = new SolidBrush(ink);
			g.FillPath(brush, body);
		}
		g.DrawPath(pen, body);
		// Handle
		g.DrawArc(pen, 9.25f, 7.25f, 4.75f, 4f, -90f, 180f);
		// Saucer
		g.DrawLine(pen, 1f, 15f, 14.5f, 15f);
		if (filled)
		{
			// Steam
			using Pen steam = new Pen(ink, 1.25f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
			g.DrawBezier(steam, 4.75f, 4.5f, 3.6f, 3.4f, 5.9f, 2.2f, 4.75f, 0.75f);
			g.DrawBezier(steam, 8.25f, 4.5f, 7.1f, 3.4f, 9.4f, 2.2f, 8.25f, 0.75f);
		}
	}

	private static void DrawMoon(Graphics g, Color ink)
	{
		using GraphicsPath outer = new GraphicsPath();
		outer.AddEllipse(0.75f, 4f, 11.5f, 11.5f);
		using GraphicsPath bite = new GraphicsPath();
		bite.AddEllipse(4.25f, 1.25f, 11f, 11f);
		using Region crescent = new Region(outer);
		crescent.Exclude(bite);
		using SolidBrush brush = new SolidBrush(ink);
		g.FillRegion(brush, crescent);
		using Pen z = new Pen(ink, 1.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
		// Big "z"
		g.DrawLines(z, new PointF[4]
		{
			new PointF(10.5f, 1.25f),
			new PointF(14.75f, 1.25f),
			new PointF(10.5f, 5.75f),
			new PointF(14.75f, 5.75f)
		});
		// Small "z"
		using Pen zs = new Pen(ink, 1.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
		g.DrawLines(zs, new PointF[4]
		{
			new PointF(6.5f, 0.75f),
			new PointF(9f, 0.75f),
			new PointF(6.5f, 3.25f),
			new PointF(9f, 3.25f)
		});
	}
}

/// <summary>Tray icons for each state, drawn to match the taskbar theme.</summary>
internal sealed class TrayIcons : IDisposable
{
	[DllImport("user32.dll")]
	private static extern bool DestroyIcon(IntPtr handle);

	private readonly Dictionary<TrayState, Icon> cache = new Dictionary<TrayState, Icon>();

	private readonly List<IntPtr> handles = new List<IntPtr>();

	private readonly bool tile;

	/// <param name="tile">false: bare glyph for the tray; true: glyph on a coloured tile for the window/taskbar.</param>
	public TrayIcons(bool tile = false)
	{
		this.tile = tile;
	}

	public Icon Get(TrayState state)
	{
		if (!cache.TryGetValue(state, out Icon icon))
		{
			using Bitmap bmp = tile
				? TrayGlyphs.RenderTile(state, SystemInformation.IconSize.Width)
				: TrayGlyphs.Render(state, SystemInformation.SmallIconSize.Width, TaskbarIsLight() ? Color.FromArgb(255, 28, 28, 28) : Color.White);
			IntPtr h = bmp.GetHicon();
			handles.Add(h);
			icon = Icon.FromHandle(h);
			cache[state] = icon;
		}
		return icon;
	}

	/// <summary>Call after the Windows theme changes; icons are redrawn on next use.</summary>
	public void Reset()
	{
		Dispose();
	}

	public void Dispose()
	{
		foreach (Icon icon in cache.Values)
		{
			icon.Dispose();
		}
		cache.Clear();
		foreach (IntPtr h in handles)
		{
			DestroyIcon(h);
		}
		handles.Clear();
	}

	private static bool TaskbarIsLight()
	{
		try
		{
			using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
			return key?.GetValue("SystemUsesLightTheme") is int v && v == 1;
		}
		catch (Exception)
		{
			return false;
		}
	}
}
