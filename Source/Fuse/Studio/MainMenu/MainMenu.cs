using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Fuse.Preview;
using Outracks.Diagnostics;
using Outracks.Fuse.Designer;
using Outracks.Fuse.Model;
using Outracks.Fuse.Setup;
using Outracks.Fuse.Stage;
using Outracks.Fusion;
using Outracks.IO;

namespace Outracks.Fuse
{
	static class MainMenu 
	{
		public static Menu Create(
			IFuse fuse,
			IShell shell,
			StageController stage,
			Menu elementMenu,
			SketchWatcher sketchConverter,
			PreviewController previewController,
			ProjectModel project,
			IScheduler scheduler,
			SetupGuide setupGuide,
			Menu windowMenu,
			IOutput output)
		{
			var debug = new Debug();
			var about = new About(fuse.Version, debug);
			var help = new Help();
			var preview = new Build(
				previewController,
				new PreviewOnDevice(fuse, project, output),
				project.BuildArgs,
				scheduler);

			var export = new Export(project, fuse, output);

			var toolsMenu
				= setupGuide.Menu
				+ Menu.Separator;

			var menus
				= Menu.Submenu("File", CreateFileMenu(fuse))
				+ Menu.Submenu("Edit", Application.EditMenu)
				+ Menu.Submenu("Element", elementMenu)
				+ Menu.Submenu("Project", ProjectMenu.Create(project, sketchConverter, shell))
				+ Menu.Submenu("Viewport", stage.Menu)
				+ Menu.Submenu("Preview", preview.Menu)
				+ Menu.Submenu("Export", export.Menu)
				+ Menu.Submenu("Tools", toolsMenu)
				+ Menu.Submenu("Window", windowMenu)
				+ Menu.Submenu("Help", CreateHelpMenu(fuse, help, about.Menu))
				+ debug.Menu;

			var fuseMenu = Menu.Submenu("Fuse", CreateFuseMenu(fuse, about.Menu));

			if (fuse.Platform == OS.Mac)
				return fuseMenu + menus;

			return menus;
		}

		private static Menu CreateHelpMenu(IFuse fuse, Help help, Menu aboutMenuItem)
		{
			return fuse.Platform == OS.Windows ? help.Menu + Menu.Separator + aboutMenuItem : help.Menu;
		}

		static Menu CreateFuseMenu(IFuse fuse, Menu aboutMenuItem)
		{
			return aboutMenuItem
				 + Menu.Separator
				 + CreateQuitItem(fuse);
		}

		static Menu CreateFileMenu(IFuse fuse)
		{
			return CreateSplashScreenItem()
				+ CreateOpenItem()
				+ (fuse.Platform == OS.Windows
					? Menu.Separator + CreateQuitItem(fuse)
					: Menu.Empty);
		}


		static Menu CreateSplashScreenItem()
		{
			return Menu.Item(
				"New...",
				hotkey: HotKey.Create(ModifierKeys.Meta, Key.N),
				action: () => Application.LaunchedWithoutDocuments());
		}

		static Menu CreateOpenItem()
		{
			return Menu.Item(
				"Open...",
				hotkey: HotKey.Create(ModifierKeys.Meta, Key.O),
				action: () => Application.ShowOpenDocumentDialog(new FileFilter("Fuse Project", "unoproj")));
		}


		static Menu CreateQuitItem(IFuse fuse)
		{
			var quitOrExit = fuse.Platform == OS.Mac ? "Quit" : "Exit";
			var quitItem = Menu.Item(
				quitOrExit + " " + "Fuse",
				hotkey: fuse.Platform == OS.Windows ? HotKey.Create(ModifierKeys.Alt, Key.F4) : HotKey.Create(ModifierKeys.Command, Key.Q),
				action: () => { Application.Exit(0); });
			return quitItem;
		}


	}
}
