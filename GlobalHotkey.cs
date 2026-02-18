using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace inplayed;

public sealed class GlobalHotkey : IDisposable
{
	private static int _nextId;
	private readonly Window _window;
	private readonly int _id;
	private readonly uint _modifiers;
	private readonly uint _virtualKey;
	private HwndSource? _source;
	private bool _registered;

	public event EventHandler? Pressed;

	public GlobalHotkey(Window window, ModifierKeys modifiers, Key key)
	{
		_window = window;
		_id = System.Threading.Interlocked.Increment(ref _nextId);
		_modifiers = ToNativeModifiers(modifiers);
		_virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);

		_window.SourceInitialized += OnSourceInitialized;
		_window.Closed += OnClosed;
	}

	private void OnSourceInitialized(object? sender, EventArgs e)
	{
		var handle = new WindowInteropHelper(_window).Handle;
		_source = HwndSource.FromHwnd(handle);
		_source?.AddHook(WndProc);
		_registered = RegisterHotKey(handle, _id, _modifiers, _virtualKey);
	}

	private void OnClosed(object? sender, EventArgs e)
	{
		Dispose();
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (msg == 0x0312 && wParam.ToInt32() == _id)
		{
			Pressed?.Invoke(this, EventArgs.Empty);
			handled = true;
		}

		return IntPtr.Zero;
	}

	public void Dispose()
	{
		_window.SourceInitialized -= OnSourceInitialized;
		_window.Closed -= OnClosed;

		var handle = new WindowInteropHelper(_window).Handle;
		if (_registered)
		{
			UnregisterHotKey(handle, _id);
			_registered = false;
		}

		if (_source != null)
		{
			_source.RemoveHook(WndProc);
			_source = null;
		}
	}

	private static uint ToNativeModifiers(ModifierKeys modifiers)
	{
		uint native = 0;
		if ((modifiers & ModifierKeys.Alt) != 0) native |= 0x0001;
		if ((modifiers & ModifierKeys.Control) != 0) native |= 0x0002;
		if ((modifiers & ModifierKeys.Shift) != 0) native |= 0x0004;
		if ((modifiers & ModifierKeys.Windows) != 0) native |= 0x0008;
		return native;
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
