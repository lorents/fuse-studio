using System;
using Outracks.Fuse.Model;
using Outracks.IO;

namespace Outracks.Fuse.Refactoring
{
	public interface IClassExtractor
	{
		void ExtractClass(ElementModel element, string name, Optional<RelativeFilePath> fileName);
		IObservable<string> LogMessages { get; }
	}
}