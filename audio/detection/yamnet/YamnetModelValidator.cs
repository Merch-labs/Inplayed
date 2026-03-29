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

			KeyValuePair<string, NodeMetadata>? firstInput = null;
			foreach (var input in session.InputMetadata)
			{
				firstInput = input;
				break;
			}

			if (firstInput == null)
			{
				return new YamnetModelValidationResult
				{
					IsValid = false,
					Message = "The ONNX model has no inputs."
				};
			}

			var dimensions = new List<int>();
			foreach (var dimension in firstInput.Value.Value.Dimensions)
			{
				dimensions.Add(dimension);
			}

			var looksLikeYamnet = false;
			if (dimensions.Count == 1 || dimensions.Count == 2)
			{
				foreach (var dimension in dimensions)
				{
					if (dimension == 15600)
					{
						looksLikeYamnet = true;
						break;
					}
				}
			}

			var shape = string.Join(", ", dimensions);

			return new YamnetModelValidationResult
			{
				IsValid = looksLikeYamnet,
				Message = looksLikeYamnet
					? $"Model looks usable: input '{firstInput.Value.Key}' with shape [{shape}]."
					: $"Model loaded, but the first input shape [{shape}] does not look like YAMNet."
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
