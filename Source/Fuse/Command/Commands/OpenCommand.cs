﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Outracks.Diagnostics;
using Outracks.IO;

namespace Outracks.Fuse
{
	public class OpenCommand : DefaultCliCommand
	{
		public static CliCommand CreateOpenCommand()
		{
			return new OpenCommand(new Shell(), FuseApi.Initialize("Fuse", new List<string>()));
		}

		readonly IFileSystem _fileSystem;
		readonly IFuse _fuse;

		public OpenCommand(IFileSystem fileSystem, IFuse fuse)
			: base("open", "Open fuse project")
		{
			_fileSystem = fileSystem;
			_fuse = fuse;
		}

		public override void Help() {}

		public override void RunDefault(string[] args, CancellationToken ct)
		{
			Run(args, ct);
		}

		public override void Run(string[] args, CancellationToken ct)
		{
			if (args.Length == 0)
			{
				LaunchFuseOpen(args);
			}
			else
			{
				try
				{
					var projectPath = new ProjectDetector(_fileSystem).GetProject(_fileSystem.ResolveAbsolutePath(args[0]));
					LaunchFuseOpen(args.Skip(1).ToArray(), projectPath);
				}
				catch (ProjectNotFound)
				{
					throw new ExitWithError("Project '" + args[0] + "' was not found");
				}
			}
		}

		private void LaunchFuseOpen(string[] args, IAbsolutePath projectPath = null)
		{
			if (Platform.OperatingSystem == OS.Windows)
			{
				var actualArgs = new List<string> { "--override-fuse-exe=\"" + _fuse.FuseExe + "\"" };
				if (projectPath != null)
				{
					actualArgs.Add(projectPath.NativePath);
				}
				actualArgs.AddRange(args);
				Program.Main(actualArgs.ToArray());
			}
			else if (Platform.OperatingSystem == OS.Mac)
			{
				var startInfo = new ProcessStartInfo()
				{
					Arguments = args.Select(a => "\"" + a + "\"").Join(" "),
				};
				if (projectPath == null)
					_fuse.Designer.Start(startInfo);
				else
					_fuse.Designer.Open(_fileSystem.ResolveAbsolutePath(projectPath.NativePath), startInfo);
			}
		}
	}
}
