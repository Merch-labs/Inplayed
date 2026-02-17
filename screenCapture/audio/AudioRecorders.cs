using NAudio.CoreAudioApi;
using NAudio.Wave;

public sealed class AudioRecorder : IDisposable
{
	private WasapiCapture? _capture;
	private readonly object _sync = new();
	private bool _isRecording;
	private WaveFormat? _waveFormat;
	private AudioRingBuffer? _ringBuffer;
	private int _clipSeconds;

	public string? OutputPath { get; private set; }

	public void StartMic(int clipSeconds)
	{
		lock (_sync)
		{
			if (_isRecording)
			{
				return;
			}

			_clipSeconds = Math.Max(1, clipSeconds);
			try
			{
				var device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
				_capture = new WasapiCapture(device);
				_waveFormat = _capture.WaveFormat;
				_ringBuffer = AudioRingBuffer.Create(_waveFormat, _clipSeconds);
				_capture.DataAvailable += OnDataAvailable;
				_capture.RecordingStopped += OnRecordingStopped;
				_isRecording = true;
				_capture.StartRecording();
			}
			catch (System.Runtime.InteropServices.COMException)
			{
				_isRecording = false;
				_waveFormat = new WaveFormat(44100, 16, 2);
				_ringBuffer = null;
				OutputPath = null;
			}
		}
	}

	public void StartSystem(int clipSeconds)
	{
		lock (_sync)
		{
			if (_isRecording)
			{
				return;
			}

			_clipSeconds = Math.Max(1, clipSeconds);
			try
			{
				var device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
				_capture = new WasapiLoopbackCapture(device);
				_waveFormat = _capture.WaveFormat;
				_ringBuffer = AudioRingBuffer.Create(_waveFormat, _clipSeconds);
				_capture.DataAvailable += OnDataAvailable;
				_capture.RecordingStopped += OnRecordingStopped;
				_isRecording = true;
				_capture.StartRecording();
			}
			catch (System.Runtime.InteropServices.COMException)
			{
				_isRecording = false;
				_waveFormat = new WaveFormat(44100, 16, 2);
				_ringBuffer = null;
				OutputPath = null;
			}
		}
	}

	public void SaveClip(string outputPath)
	{
		lock (_sync)
		{
			if (_waveFormat == null)
			{
				return;
			}

			byte[] data;
			if (_ringBuffer != null && _ringBuffer.Length > 0)
			{
				data = _ringBuffer.Snapshot();
			}
			else
			{
				var length = _waveFormat.AverageBytesPerSecond * Math.Max(1, _clipSeconds);
				data = new byte[length];
			}
			using var writer = new WaveFileWriter(outputPath, _waveFormat);
			writer.Write(data, 0, data.Length);
			writer.Flush();
			OutputPath = outputPath;
		}
	}

	public void Stop()
	{
		lock (_sync)
		{
			if (!_isRecording)
			{
				return;
			}

			_capture?.StopRecording();
		}
	}

	private void OnDataAvailable(object? sender, WaveInEventArgs e)
	{
		lock (_sync)
		{
			_ringBuffer?.Write(e.Buffer, 0, e.BytesRecorded);
		}
	}

	private void OnRecordingStopped(object? sender, StoppedEventArgs e)
	{
		lock (_sync)
		{
			_capture?.Dispose();
			_capture = null;

			_isRecording = false;
		}
	}

	public void Dispose()
	{
		Stop();
	}
}

internal sealed class AudioRingBuffer
{
	private readonly byte[] _buffer;
	private int _writePos;
	private int _length;

	private AudioRingBuffer(int capacity)
	{
		_buffer = new byte[capacity];
	}

	public int Length => _length;

	public static AudioRingBuffer Create(WaveFormat format, int clipSeconds)
	{
		var seconds = Math.Max(1, clipSeconds);
		var capacity = Math.Max(format.AverageBytesPerSecond * seconds, format.BlockAlign);
		return new AudioRingBuffer(capacity);
	}

	public void Write(byte[] data, int offset, int count)
	{
		var remaining = count;
		var srcOffset = offset;
		while (remaining > 0)
		{
			var space = _buffer.Length - _writePos;
			var toCopy = Math.Min(space, remaining);
			Buffer.BlockCopy(data, srcOffset, _buffer, _writePos, toCopy);
			_writePos = (_writePos + toCopy) % _buffer.Length;
			_srcAdvance(ref srcOffset, ref remaining, toCopy);

			_length = Math.Min(_length + toCopy, _buffer.Length);
		}
	}

	public byte[] Snapshot()
	{
		var result = new byte[_length];
		if (_length == 0)
		{
			return result;
		}

		var start = (_writePos - _length + _buffer.Length) % _buffer.Length;
		var first = Math.Min(_buffer.Length - start, _length);
		Buffer.BlockCopy(_buffer, start, result, 0, first);
		if (first < _length)
		{
			Buffer.BlockCopy(_buffer, 0, result, first, _length - first);
		}

		return result;
	}

	private static void _srcAdvance(ref int offset, ref int remaining, int copied)
	{
		offset += copied;
		remaining -= copied;
	}
}
