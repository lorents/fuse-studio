using Outracks.Fuse.Live;
using Outracks.Fuse.Model;

namespace Outracks.Fuse.Editing
{
	public class InsertElement
	{
		readonly ModelUpdater _updater;
		public InsertElement(ModelUpdater updater)
		{
			_updater = updater;
		}

		public ElementModel Insert(ElementModel element, SourceFragment fragment)
		{
			var child = _updater.CreateChildElement(element, fragment);

			_updater.UpdateXml(element, e => e.AddIndented(child.XElement));
			_updater.UpdateChildren(element, c => c.OnAdd(child));

			element.Document.Save();
			_updater.Flush();

			return child;
		}

		public ElementModel InsertBefore(ElementModel element, SourceFragment fragment)
		{
			if (element.Parent.IsUnknown)
				throw new ElementIsRoot();

			var sibling = _updater.CreateChildElement(element.Parent, fragment);

			_updater.UpdateXml(element, e => e.AddBeforeSelfIndented(sibling.XElement));
			_updater.UpdateChildren(element.Parent, c => c.OnInsert(c.Value.IndexOf(element), sibling));

			element.Document.Save();
			_updater.Flush();

			return sibling;
		}

		public ElementModel InsertAfter(ElementModel element, SourceFragment fragment)
		{
			if (element.Parent.IsUnknown)
				throw new ElementIsRoot();

			var sibling = _updater.CreateChildElement(element.Parent, fragment);

			_updater.UpdateXml(element, e => e.AddAfterSelfIndented(sibling.XElement));
			_updater.UpdateChildren(element.Parent, c => c.OnInsert(c.Value.IndexOf(element) + 1, sibling));

			element.Document.Save();
			_updater.Flush();

			return sibling;
		}

	}
}