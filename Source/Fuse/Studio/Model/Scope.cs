using System.Reactive.Subjects;

namespace Outracks.Fuse.Model
{
	public class Scope
	{
		public readonly Optional<Scope> Parent;

		public readonly ElementModel Root;

		public readonly BehaviorSubject<ElementModel> PreviewedSelection;
		public readonly BehaviorSubject<ElementModel> CurrentSelection;
		
		public Scope(ElementModel root, Optional<Scope> parent = default(Optional<Scope>))
		{
			Parent = parent;
			Root = root;
			PreviewedSelection = new BehaviorSubject<ElementModel>(root);
			CurrentSelection = new BehaviorSubject<ElementModel>(new UnknownElement());
		}
	}
}