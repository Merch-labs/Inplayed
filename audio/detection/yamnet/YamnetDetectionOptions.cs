namespace inplayed;

internal sealed class YamnetDetectionOptions
{
	public bool Enabled { get; init; }
	public string ModelPath { get; init; } = string.Empty;
	public int SensitivityPercent { get; init; } = 65;
	public int CooldownSeconds { get; init; } = 15;

	public static YamnetDetectionOptions FromConfig(AppConfig config)
	{
		return new YamnetDetectionOptions
		{
			Enabled = config.Recording.YamnetDetection.Enabled,
			ModelPath = config.Recording.YamnetDetection.ModelPath,
			SensitivityPercent = config.Recording.YamnetDetection.SensitivityPercent,
			CooldownSeconds = config.Recording.YamnetDetection.CooldownSeconds
		};
	}
}

internal sealed class YamnetModelValidationResult
{
	public bool IsValid { get; init; }
	public string Message { get; init; } = string.Empty;
}
