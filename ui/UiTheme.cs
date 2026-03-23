using System.Windows.Forms;

namespace inplayed;

internal static class UiTheme
{
	public static readonly Color ShellBackground = Color.FromArgb(243, 246, 250);
	public static readonly Color SurfaceBackground = Color.White;
	public static readonly Color SidebarBackground = Color.FromArgb(232, 237, 243);
	public static readonly Color SidebarHover = Color.FromArgb(221, 228, 236);
	public static readonly Color SidebarActive = Color.FromArgb(205, 218, 237);
	public static readonly Color TitleColor = Color.FromArgb(33, 40, 48);
	public static readonly Color AccentColor = Color.FromArgb(66, 101, 186);
	public static readonly Color AccentHoverColor = Color.FromArgb(55, 88, 164);
	public static readonly Color SecondaryButtonColor = Color.FromArgb(229, 234, 240);
	public static readonly Color SecondaryTextColor = Color.FromArgb(76, 88, 102);

	public static void StyleAccentButton(Button button)
	{
		button.FlatStyle = FlatStyle.Flat;
		button.FlatAppearance.BorderSize = 0;
		button.FlatAppearance.MouseOverBackColor = AccentHoverColor;
		button.BackColor = AccentColor;
		button.ForeColor = Color.White;
		button.UseVisualStyleBackColor = false;
		button.Padding = new Padding(12, 8, 12, 8);
	}

	public static void StyleSecondaryButton(Button button)
	{
		button.FlatStyle = FlatStyle.Flat;
		button.FlatAppearance.BorderSize = 0;
		button.BackColor = SecondaryButtonColor;
		button.ForeColor = SecondaryTextColor;
		button.UseVisualStyleBackColor = false;
		button.Padding = new Padding(12, 8, 12, 8);
	}

	public static void ApplyPalette(Control root)
	{
		foreach (Control control in root.Controls)
		{
			switch (control)
			{
				case TableLayoutPanel or FlowLayoutPanel or SplitContainer:
					control.BackColor = ShellBackground;
					break;
				case GroupBox:
					control.BackColor = SurfaceBackground;
					control.ForeColor = SecondaryTextColor;
					break;
				case Label:
					control.ForeColor = SecondaryTextColor;
					break;
				case ListBox or TextBox or ComboBox or NumericUpDown:
					control.BackColor = SurfaceBackground;
					control.ForeColor = Color.Black;
					break;
			}

			if (control.HasChildren)
			{
				ApplyPalette(control);
			}
		}
	}
}
