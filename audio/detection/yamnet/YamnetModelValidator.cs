using System.IO;
using Microsoft.ML.OnnxRuntime;

namespace inplayed;

internal static class YamnetModelValidator
{
	public static YamnetModelValidationResult Validate(string modelPath)
	{
		if (string.IsNullOrWhiteSpace(modelPath))
		{
			return new YamnetModelValidationResult
			{
				IsValid = false,
				Message = "Choose a YAMNet ONNX model file."
			};
		}

		if (!File.Exists(modelPath))
		{
			return new YamnetModelValidationResult
			{
				IsValid = false,
				Message = "The selected YAMNet model file was not found."
			};
		}

		if (!string.Equals(Path.GetExtension(modelPath), ".onnx", StringComparison.OrdinalIgnoreCase))
		{
			return new YamnetModelValidationResult
			{
				IsValid = false,
				Message = "Use an ONNX-exported YAMNet model."
			};
		}

		try
		{
			using var session = new InferenceSession(modelPath);
			if (session.InputMetadata.Count == 0)
			{
				return new YamnetModelValidationResult
				{
					IsValid = false,
					Message = "The ONNX model has no inputs."
				};
			}

			var input = session.InputMetadata.First();
			var dimensions = input.Value.Dimensions.ToArray();
			var looksLikeYamnet =
				(dimensions.Length == 1 || dimensions.Length == 2) &&
				dimensions.Contains(15600);

			return new YamnetModelValidationResult
			{
				IsValid = looksLikeYamnet,
				Message = looksLikeYamnet
					? $"Model looks usable: input '{input.Key}' with shape [{string.Join(", ", dimensions)}]."
					: $"Model loaded, but the first input shape [{string.Join(", ", dimensions)}] does not look like YAMNet."
			};
		}
		catch (Exception ex)
		{
			return new YamnetModelValidationResult
			{
				IsValid = false,
				Message = $"Model load failed: {ex.GetType().Name}."
			};
		}
	}
}
