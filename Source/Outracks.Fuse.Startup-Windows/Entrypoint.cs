using System;
using System.Linq;

namespace Outracks.Fuse
{
	public static class Entrypoint
	{
		[STAThread]
		public static int Main(string[] cmdArgs)
		{
			return ConsoleProgram.Run(cmdArgs.ToList());
		}
	}

}
