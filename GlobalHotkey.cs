using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;

namespace inplayed;

public sealed class GlobalHotkey : IDisposable
{
	private static int _nextId;
	private readonly Form _form;
	private readonly int _id;
	private readonly uint _modifiers;
	private readonly uint _virtualKey;
	private HotkeyWindow? _window;
	private bool _registered;

	public event EventHandler? Pressed;

	public GlobalHotkey(Form form, ModifierKeys modifiers, Key key)
	{
		_form = form;
		_id = Interlocked.Increment(ref _nextId);
		_modifiers = ToNativeModifiers(modifiers);
		_virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);

		_form.HandleCreated += OnHandleCreated;
		_form.HandleDestroyed += OnHandleDestroyed;
		_form.FormClosed += OnFormClosed;

		if (_form.IsHandleCreated)
		{
			RegisterHotkey();
		}
	}

	private void OnHandleCreated(object? sender, EventArgs e)
	{
		RegisterHotkey();
	}

	private void OnHandleDestroyed(object? sender, EventArgs e)
	{
		UnregisterHotkey();
	}

	private void OnFormClosed(object? sender, FormClosedEventArgs e)
	{
		Dispose();
	}

	private void RegisterHotkey()
	{
		if (_registered || !_form.IsHandleCreated)
		{
			return;
		}

		_window = new HotkeyWindow(_form.Handle, OnPressed);
		_registered = RegisterHotKey(_form.Handle, _id, _modifiers, _virtualKey);
	}

	private void UnregisterHotkey()
	{
		if (_registered && _form.IsHandleCreated)
		{
			UnregisterHotKey(_form.Handle, _id);
		}

		_registered = false;
		_window?.Dispose();
		_window = null;
	}

	private void OnPressed(int id)
	{
		if (id == _id)
		{
			Pressed?.Invoke(this, EventArgs.Empty);
		}
	}

	public void Dispose()
	{
		_form.HandleCreated -= OnHandleCreated;
		_form.HandleDestroyed -= OnHandleDestroyed;
		_form.FormClosed -= OnFormClosed;
		UnregisterHotkey();
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

	private sealed class HotkeyWindow : NativeWindow, IDisposable
	{
		private readonly Action<int> _onHotkey;

		public HotkeyWindow(IntPtr handle, Action<int> onHotkey)
		{
			_onHotkey = onHotkey;
			AssignHandle(handle);
		}

		protected override void WndProc(ref Message m)
		{
			if (m.Msg == 0x0312)
			{
				_onHotkey(m.WParam.ToInt32());
			}

			base.WndProc(ref m);
		}

		public void Dispose()
		{
			ReleaseHandle();
		}
	}
}
