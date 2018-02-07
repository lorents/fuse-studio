using Outracks.Fuse.Model;

namespace Outracks.Fuse
{
	public interface IModelChangeListener
	{
		void ElementAttributeChanged(ElementModel element, string attribute);

		void ElementChildrenChanged(ElementModel element);

		void ElementNameChanged(ElementModel element);

		void Flush();
	}
}