using System;
using System.Linq;

namespace Outracks.Fuse
{
	public static class Entrypoint
	{
		[STAThread]
		static int Main(string[] cmdArgs)
		{
			return ConsoleProgram.Run(cmdArgs.ToList());
		}
	}
}
