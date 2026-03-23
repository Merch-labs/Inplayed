using System.Diagnostics;
using Microsoft.Win32;

namespace inplayed;

internal static class WindowsStartupRegistration
{
	private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
	private const string ValueName = "inplayed";

	public static bool TryApply(AppConfig.StartupConfig startup, out string? error)
	{
		error = null;

		try
		{
			using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
			if (key == null)
			{
				error = "Could not access the Windows startup registry key.";
				return false;
			}

			if (startup.LaunchOnWindowsStartup)
			{
				key.SetValue(ValueName, BuildRegistrationCommand());
			}
			else
			{
				key.DeleteValue(ValueName, throwOnMissingValue: false);
			}

			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}

	internal static string BuildRegistrationCommand()
	{
		var executablePath = Environment.ProcessPath;
		if (string.IsNullOrWhiteSpace(executablePath))
		{
			executablePath = Process.GetCurrentProcess().MainModule?.FileName;
		}

		if (string.IsNullOrWhiteSpace(executablePath))
		{
			throw new InvalidOperationException("Could not resolve the inplayed executable path.");
		}

		return BuildRegistrationCommand(executablePath);
	}

	internal static string BuildRegistrationCommand(string executablePath)
	{
		return $"\"{executablePath}\" {AppLaunchOptions.BackgroundArgument}";
	}
}
