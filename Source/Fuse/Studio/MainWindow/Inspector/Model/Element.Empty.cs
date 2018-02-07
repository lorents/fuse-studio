using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Outracks.Fusion;
using Outracks.Simulator;

namespace Outracks.Fuse.Editing
{
	public static partial class Element
	{
		static Element()
		{
			Empty = new EmptyElement();
		}

		public static IElement Empty { get; private set; }
	}

	class EmptyElement : IElement
	{
		readonly IProperty<string> _property;
		readonly IObservable<bool> _false;

		public EmptyElement()
		{
			_false = Observable.Return(false);
			_property = Property.Default<string>();
			Children = ObservableList.Empty<IElement>();
			Name = Property.Constant("Unknown");
		}

		public IProperty<string> Name { get; private set; }

		public IObservableList<IElement> Children { get; private set; }

		public Command Remove()
		{
			return Command.Disabled;
		}

		public Command Insert(SourceFragment fragment)
		{
			return Command.Disabled;
		}

		public Command InsertAfter(SourceFragment fragment)
		{
			return Command.Disabled;
		}

		public Command InsertBefore(SourceFragment fragment)
		{
			return Command.Disabled;
		}

		public IProperty<string> Content
		{
			get { return _property; }
		}

		public IObservable<bool> IsEmpty
		{
			get { return Observable.Return(true); }
		}

		public IObservable<bool> IsReadOnly
		{
			get { return Observable.Return(true); }
		}

		public IElement Parent
		{
			get { return Element.Empty; }
		}

		public IElement Base
		{
			get { return Element.Empty; }
		}

		public IObservable<bool> Is(string elementType)
		{
			return _false;
		}

		public IObservable<bool> IsChildOf(string elementType)
		{
			return _false;
		}

		public IObservable<bool> IsSiblingOf(string elementType)
		{
			return _false;
		}

		public IObservable<bool> IsDescendantOf(IElement element)
		{
			return _false;
		}

		public IAttribute this[string propertyName]
		{
			get { return new EmptyAttribute(); }
		}

		public IObservable<Optional<SourceReference>> SourceReference
		{
			get { return Observable.Return(new Optional<SourceReference>()); }
		}
	}
}