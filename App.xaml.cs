using System.Windows;
using Forms = System.Windows.Forms;

namespace inplayed;

public partial class App : System.Windows.Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		ShutdownMode = ShutdownMode.OnExplicitShutdown;

		Forms.Application.EnableVisualStyles();
		Forms.Application.SetCompatibleTextRenderingDefault(false);

		var form = new MainForm();
		form.FormClosed += (_, _) => Shutdown();
		form.Show();
	}
}
