using System.Windows;
using Forms = System.Windows.Forms;

namespace inplayed;

public partial class App : System.Windows.Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		ShutdownMode = ShutdownMode.OnExplicitShutdown;

		Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
		Forms.Application.EnableVisualStyles();
		Forms.Application.SetCompatibleTextRenderingDefault(false);

		var config = AppConfig.Load();
		var launchOptions = AppLaunchOptions.From(e.Args, config);
		var form = new MainForm(launchOptions);
		form.FormClosed += (_, _) => Shutdown();

		if (launchOptions.StartHidden)
		{
			form.StartHiddenMode();
			return;
		}

		form.Show();
	}
}
