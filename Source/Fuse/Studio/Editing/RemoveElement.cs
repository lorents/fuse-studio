using Outracks.Fuse.Live;
using Outracks.Fuse.Model;

namespace Outracks.Fuse.Editing
{
	public class RemoveElement
	{
		readonly ModelUpdater _updater;
		public RemoveElement(ModelUpdater updater)
		{
			_updater = updater;
		}

		public void Remove(ElementModel element)
		{
			if (element.Parent.IsUnknown)
				throw new ElementIsRoot();

			_updater.UpdateXml(element, e => e.RemoveIndented());
			_updater.UpdateChildren(element.Parent, c => c.OnRemove(c.Value.IndexOf(element)));

			element.Document.Save();
			_updater.Flush();
		}
	}
}