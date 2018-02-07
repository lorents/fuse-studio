using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using Fuse.Preview;
using Outracks.Diagnostics;
using Outracks.Fuse.Model;
using Outracks.Fusion;

namespace Outracks.Fuse
{
	class Export
	{
		public Export(ProjectModel project, IFuse fuse, IOutput output)
		{
			
			var isMac = fuse.Platform == OS.Mac;

			ExportForAndroid =
				Command.Enabled(
					action: () => fuse.RunFuse(
						"build",
						new[] { project.Path.NativePath, "-t=android", "--run" }.Concat(project.BuildArgs.All.Value).ToArray(),
						Observer.Create<string>(output.Write)));

			ExportForIos =
				Command.Create(
					isEnabled: isMac,
					action: () => fuse.RunFuse(
						"build",
						new[] { project.Path.NativePath, "-t=ios", "--run" }.Concat(project.BuildArgs.All.Value).ToArray(),
						Observer.Create<string>(output.Write)));

			Menu = Menu.Item("Export for Android", ExportForAndroid)
				+ Menu.Item("Export for iOS" + (!isMac ? " (Mac only)" : ""), ExportForIos);
		}

		public Menu Menu { get; private set; }

		public Command ExportForAndroid { get; private set; }
		public Command ExportForIos { get; private set; }
	}
}