public readonly record struct NvencReadiness(
	bool IsReady,
	string Summary,
	uint MaxSupportedVersion,
	int CudaDriverVersion,
	int FunctionPointerCount,
	bool RequiredSlotsPresent,
	bool OpenSessionBindable,
	bool InitializeEncoderBindable,
	bool PresetApiBindable);
