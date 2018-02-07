using System;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using Outracks.Fusion;

namespace Outracks.Fuse
{
	static class SchedulerCommand
	{
		public static Command CreateCommand(this IScheduler scheduler, Action action)
		{
			var isEnabled = new BehaviorSubject<bool>(true);
			return Command.Create(isEnabled, () =>
			{
				isEnabled.OnNext(false);
				scheduler.Schedule(() =>
				{
					try
					{
						action();
					}
					catch (Exception) { }
					finally
					{
						isEnabled.OnNext(true);
					}
				});
			});
		}
	}

	class Build
	{
		public Build(PreviewController preview, PreviewOnDevice previewOnDevice, BuildArgs args, IScheduler scheduler)
		{
			Rebuild = scheduler.CreateCommand(preview.Build);
			Refresh = scheduler.CreateCommand(preview.Refresh);
			var enableUsbMode = scheduler.CreateCommand(preview.EnableUsbMode);

			var buildFlagsWindowVisible = new BehaviorSubject<bool>(false);

			BuildFlags = Command.Enabled(() => buildFlagsWindowVisible.OnNext(true));

			Application.Desktop.CreateSingletonWindow(
				isVisible: buildFlagsWindowVisible,
				window: window => BuildFlagsWindow.Create(buildFlagsWindowVisible, args));

			Menu =
				  Menu.Item("Refresh", Refresh, hotkey: HotKey.Create(ModifierKeys.Meta, Key.R))
				+ Menu.Item("Rebuild", Rebuild, hotkey: HotKey.Create(ModifierKeys.Meta | ModifierKeys.Shift, Key.R))
				+ Menu.Separator
				+ Menu.Item("Reconnect USB (Android)", enableUsbMode)
				+ Menu.Separator
				+ previewOnDevice.Menu
				+ Menu.Separator
				+ Menu.Item("Build flags", BuildFlags);
		}

		public Menu Menu { get; private set; }

		public Command Refresh { get; private set; }
		public Command Rebuild { get; private set; }
		public Command BuildFlags { get; private set; }


	}
}