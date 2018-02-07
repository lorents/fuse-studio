using Outracks.Diagnostics;
using Outracks.Fuse.Theming.Themes;
using Outracks.Fusion;

namespace Outracks.Fuse
{
	static class WindowMenu
	{
		public static Menu Create(IProperty<Mode> mode, IProperty<bool> topMost)
		{
			return Menu.Toggle(
				name: "Compact mode",
				toggle: mode.Convert(
					convert: m => m == Mode.Compact,
					convertBack: b => b ? Mode.Compact : Mode.Normal),
				hotkey: HotKey.Create(ModifierKeys.Meta, Key.M))
				+ Menu.Separator
				+ Menu.Toggle(
					name: "Keep window on top",
					toggle: topMost,
					hotkey: HotKey.Create((Platform.OperatingSystem == OS.Windows ? ModifierKeys.Shift : ModifierKeys.Alt) | ModifierKeys.Meta, Key.T))
				+ Menu.Separator
				+ Menu.Option(
					value: Themes.OriginalLight,
					name: "Light theme",
					property: Theme.CurrentTheme)
				+ Menu.Option(
					value: Themes.OriginalDark,
					name: "Dark theme",
					property: Theme.CurrentTheme);

		}
	}
}