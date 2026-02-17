public readonly record struct EncodedPacket(
	ReadOnlyMemory<byte> Data,
	long PresentationTimestamp,
	long DecodeTimestamp,
	bool IsKeyFrame);
