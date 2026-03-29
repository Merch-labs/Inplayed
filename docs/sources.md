# Sources

This file lists the main places a student developer could reasonably have used to research the less obvious parts of this repo.

It does not try to cover normal C# basics like classes, methods, loops, properties, or events.

It also does not cover anything inside `screenCapture/`, because that part was excluded for this pass.

## WinForms UI

Used for:
- `ui/MainForm.cs`
- `ui/pages/SettingsPage.cs`
- `ui/pages/SocialPage.cs`

Sources:
- Windows Forms overview: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/overview/
- TableLayoutPanel overview: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/tablelayoutpanel-control-overview
- FileDialog API: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.filedialog
- NotifyIcon overview: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/notifyicon-component-overview-windows-forms
- UseShellExecute property: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute

## Config Files And Local JSON Storage

Used for:
- `config/AppConfig.cs`
- `config/AppConfigStorage.cs`
- `social/LocalSocialStore.cs`

Sources:
- System.Text.Json how-to: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/how-to
- JsonSerializer API: https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer
- JsonDocument API: https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsondocument

## Global Hotkeys

Used for:
- `GlobalHotkey.cs`

Sources:
- RegisterHotKey function: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
- UnregisterHotKey function: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-unregisterhotkey

## Windows Startup

Used for:
- `startup/WindowsStartupRegistration.cs`
- `startup/AppLaunchOptions.cs`

Sources:
- Run and RunOnce Registry Keys: https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys
- RegistryKey.CreateSubKey API: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.registrykey.createsubkey

## YAMNet Model Validation

Used for:
- `audio/detection/yamnet/YamnetDetectionOptions.cs`
- `audio/detection/yamnet/YamnetModelValidator.cs`

Sources:
- ONNX Runtime C# getting started: https://onnxruntime.ai/docs/get-started/with-csharp.html
- MediaPipe Audio Classifier guide: https://ai.google.dev/edge/mediapipe/solutions/audio/audio_classifier

## Algorithms

Used for:
- `algorithms/ContainsAlgorithm.cs`
- `algorithms/FindIndexAlgorithm.cs`
- `algorithms/FindFirstExistingDistinctPathAlgorithm.cs`
- `algorithms/InsertionSortAlgorithm.cs`
- `algorithms/TryFindFirstAlgorithm.cs`

Sources:
- Insertion sort overview: https://www.programiz.com/dsa/insertion-sort
- Linear search overview: https://www.programiz.com/dsa/linear-search
- `File.Exists` API: https://learn.microsoft.com/en-us/dotnet/api/system.io.file.exists

## Backend Server

Used for:
- `server/inplayed.Server/Program.cs`
- `server/inplayed.Server/Data/SocialDbContext.cs`
- `server/inplayed.Server/Models/*`

Sources:
- ASP.NET Core minimal APIs: https://learn.microsoft.com/en-us/aspnet/core/tutorials/min-web-api
- Npgsql basic usage: https://www.npgsql.org/doc/basic-usage.html
- PostgreSQL tutorial: https://www.postgresql.org/docs/current/tutorial.html

## General Style Target

For this pass, the code outside `screenCapture/` was kept closer to:
- direct control flow
- smaller helper methods
- fewer compact LINQ expressions
- explicit UI wiring

The idea was to make it read more like a strong student project rather than a heavily abstracted codebase.
