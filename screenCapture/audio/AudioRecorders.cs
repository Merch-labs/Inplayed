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
	private bool _captureAvailable;

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
				_captureAvailable = true;
				_isRecording = true;
				_capture.StartRecording();
			}
			catch (System.Runtime.InteropServices.COMException)
			{
				_isRecording = false;
				_captureAvailable = false;
				_waveFormat = null;
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
				_captureAvailable = true;
				_isRecording = true;
				_capture.StartRecording();
			}
			catch (System.Runtime.InteropServices.COMException)
			{
				_isRecording = false;
				_captureAvailable = false;
				_waveFormat = null;
				_ringBuffer = null;
				OutputPath = null;
			}
		}
	}

	public string? SaveClip(string outputPath, long? endTimestampMs = null)
	{
		lock (_sync)
		{
			if (_waveFormat == null || !_captureAvailable || _ringBuffer == null || _ringBuffer.Length <= 0)
			{
				return null;
			}

			var data = _ringBuffer.Snapshot(endTimestampMs);
			if (data.Length <= 0)
			{
				return null;
			}

			using var writer = new WaveFileWriter(outputPath, _waveFormat);
			writer.Write(data, 0, data.Length);
			writer.Flush();
			OutputPath = outputPath;
			return outputPath;
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
			_ringBuffer?.Write(e.Buffer, 0, e.BytesRecorded, CaptureClock.NowMilliseconds());
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
	private readonly int _averageBytesPerSecond;
	private readonly int _blockAlign;
	private int _writePos;
	private int _length;
	private long _firstTimestampMs;
	private long _lastTimestampMs;

	private AudioRingBuffer(int capacity, int averageBytesPerSecond, int blockAlign)
	{
		_buffer = new byte[capacity];
		_averageBytesPerSecond = Math.Max(1, averageBytesPerSecond);
		_blockAlign = Math.Max(1, blockAlign);
	}

	public int Length => _length;

	public static AudioRingBuffer Create(WaveFormat format, int clipSeconds)
	{
		var seconds = Math.Max(1, clipSeconds);
		var capacity = Math.Max(format.AverageBytesPerSecond * seconds, format.BlockAlign);
		return new AudioRingBuffer(capacity, format.AverageBytesPerSecond, format.BlockAlign);
	}

	public void Write(byte[] data, int offset, int count, long endTimestampMs)
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

		_lastTimestampMs = endTimestampMs > 0 ? endTimestampMs : CaptureClock.NowMilliseconds();
		_firstTimestampMs = _lastTimestampMs - GetDurationMsForBytes(_length);
	}

	public byte[] Snapshot(long? endTimestampMs = null)
	{
		if (_length == 0)
		{
			return Array.Empty<byte>();
		}

		var effectiveEndTimestampMs = ResolveEndTimestamp(endTimestampMs);
		var startTimestampMs = Math.Max(_firstTimestampMs, effectiveEndTimestampMs - GetDurationMsForBytes(_length));
		var startTrimBytes = AlignBytesToBlockBoundary(GetBytesForDurationMs(startTimestampMs - _firstTimestampMs));
		var endTrimBytes = AlignBytesToBlockBoundary(GetBytesForDurationMs(_lastTimestampMs - effectiveEndTimestampMs));
		var copyLength = Math.Max(0, _length - startTrimBytes - endTrimBytes);
		if (copyLength <= 0)
		{
			return Array.Empty<byte>();
		}

		var result = new byte[copyLength];
		var start = (_writePos - _length + _buffer.Length) % _buffer.Length;
		var absoluteStart = (start + startTrimBytes) % _buffer.Length;
		var first = Math.Min(_buffer.Length - absoluteStart, copyLength);
		Buffer.BlockCopy(_buffer, absoluteStart, result, 0, first);
		if (first < copyLength)
		{
			Buffer.BlockCopy(_buffer, 0, result, first, copyLength - first);
		}

		return result;
	}

	private long ResolveEndTimestamp(long? requestedEndTimestampMs)
	{
		if (_lastTimestampMs <= 0)
		{
			return CaptureClock.NowMilliseconds();
		}

		if (!requestedEndTimestampMs.HasValue)
		{
			return _lastTimestampMs;
		}

		return Math.Min(requestedEndTimestampMs.Value, _lastTimestampMs);
	}

	private long GetDurationMsForBytes(int byteCount)
	{
		if (byteCount <= 0)
		{
			return 0;
		}

		return (long)Math.Round(byteCount * 1000.0 / _averageBytesPerSecond);
	}

	private int GetBytesForDurationMs(long durationMs)
	{
		if (durationMs <= 0)
		{
			return 0;
		}

		var bytes = (int)Math.Round(durationMs * _averageBytesPerSecond / 1000.0);
		return Math.Min(bytes, _length);
	}

	private int AlignBytesToBlockBoundary(int byteCount)
	{
		if (byteCount <= 0)
		{
			return 0;
		}

		return Math.Min(byteCount - (byteCount % _blockAlign), _length);
	}

	private static void _srcAdvance(ref int offset, ref int remaining, int copied)
	{
		offset += copied;
		remaining -= copied;
	}
}
