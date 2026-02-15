public readonly record struct NvencReadiness(
	bool IsReady,
	string Summary,
	uint MaxSupportedVersion,
	int CudaDriverVersion);
