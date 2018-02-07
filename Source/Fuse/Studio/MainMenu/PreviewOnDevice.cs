using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using Fuse.Preview;
using Outracks.Diagnostics;
using Outracks.Fuse.Model;
using Outracks.Fuse.Setup;
using Outracks.Fusion;

namespace Outracks.Fuse
{
	class PreviewOnDevice
	{
		public PreviewOnDevice(IFuse fuse, ProjectModel project, IOutput output)
		{
			var isMac = fuse.Platform == OS.Mac;
			var startedAndroidPreview = new Subject<Unit>();
			MissingAndroidNotification.Create(fuse, startedAndroidPreview);

			PreviewAndroidApp = 
				Command.Enabled(
					action: () =>
					{
						startedAndroidPreview.OnNext(Unit.Default);
						fuse.RunFuse(
							"preview",
							new[] { project.Path.NativePath, "-t=android", "--quit-after-apk-launch" }.Concat(project.BuildArgs.All.Value).ToArray(),
							Observer.Create<string>(output.Write));
					});

			PreviewIosApp = 
				Command.Create(
					isEnabled: isMac,
					action: () => fuse.RunFuse(
						"preview",
						new[] { project.Path.NativePath, "-t=ios" }.Concat(project.BuildArgs.All.Value).ToArray(),
						Observer.Create<string>(output.Write)));

			Menu = Menu.Item("Preview on Android", PreviewAndroidApp)
				 + Menu.Item("Preview on iOS" + (!isMac ? " (Mac only)" : ""), PreviewIosApp);

		}

		public Menu Menu { get; private set; }

		public Command PreviewIosApp { get; private set; }
		public Command PreviewAndroidApp { get; private set; }
	}
}