using System;
using System.Threading.Tasks;
using Outracks.Fusion;
using Outracks.IO;

namespace Outracks.Fuse
{
	static class OpenTerminal
	{
		public static Command CreateCommand(IShell shell, IObservable<Optional<AbsoluteFilePath>> path)
		{
			return path.Switch(file =>
				Command.Create(
					isEnabled: file.HasValue,
					action: () => Task.Run(() => shell.OpenTerminal(file.Value.ContainingDirectory))));
		}
	}
}