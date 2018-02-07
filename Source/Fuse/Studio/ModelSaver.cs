using Outracks.Fuse.Model;

namespace Outracks.Fuse
{
	public static class ModelSaver
	{
		public static void Save(this IDocumentModel document)
		{
			var sourceFragment = SourceFragment.FromXml(document.Root.XElement);
			document.File.Save(sourceFragment.ToBytes());
		}
	}
}