using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Outracks.Fuse.Live;
using Outracks.Fusion;

namespace Outracks.Fuse.Editing
{
	public static partial class Element
	{
		/// <remarks>
		/// Note that queries on the result requring actual data might not push a value until the observable has pushed the first element.
		/// If it is desirable to to start with a specific element (for instance Element.Empty), use StartWith(intialElement) before Switch().
		/// </remarks>>
		public static IElement Switch(this IObservable<IElement> element)
		{
			return new SwitchingElement(element);
		}
	}

	class SwitchingElement : IElement
	{
		readonly IObservable<IElement> _current;

		readonly Lazy<IElement> _parent;
		public IElement Parent
		{
			get { return _parent.Value; }
		}

		readonly Lazy<IElement> _base;
		public IElement Base
		{
			get { return _base.Value; }
		}

		public SwitchingElement(IObservable<IElement> current)
		{
			_current = current
				.DistinctUntilChanged()
				.Replay(1).RefCount()
				;

			_parent = new Lazy<IElement>(() =>
				_current.Select(c => c.Parent).Switch());

			_base = new Lazy<IElement>(() =>
				_current.Select(c => c.Base).Switch());

			Name = _current.Select(e => e.Name).Switch()
				;

			Content = _current.Select(e => e.Content).Switch()
				;

			Children = _current.Select(e => e.Children).Switch()
				//.DistinctUntilSequenceChanged()
				//.Replay(1).RefCount()
				;

			IsEmpty = _current.Select(e => e.IsEmpty).Switch()
				.DistinctUntilChanged()
				.Replay(1).RefCount()
				;

			IsReadOnly = _current.Select(e => e.IsReadOnly).Switch()
				.DistinctUntilChanged()
				.Replay(1).RefCount()
				;
		}

		public IProperty<string> Name { get; private set; }

		public IProperty<string> Content { get; private set; }

		public IObservableList<IElement> Children { get; private set; }

		public IAttribute this[string propertyName]
		{
			get { return new ElementAttributeEditor(_current.Select(c => c[propertyName].StringValue).Switch()); }
		}

		public IObservable<bool> IsEmpty { get; private set; }

		public IObservable<bool> IsReadOnly { get; private set; }

		public Command Remove()
		{
			return _current.Select(c => c.Remove()).Switch();
		}

		public Command Insert(SourceFragment fragment)
		{
			return _current.Select(c => c.Insert(fragment)).Switch();
		}

		public Command InsertAfter(SourceFragment fragment)
		{
			return _current.Select(c => c.InsertAfter(fragment)).Switch();
		}

		public Command InsertBefore(SourceFragment fragment)
		{
			return _current.Select(c => c.InsertBefore(fragment)).Switch();
		}

		
		public IObservable<bool> Is(string elementType)
		{
			return _current.Switch(c => c.Is(elementType)).DistinctUntilChanged();
		}

		public IObservable<bool> IsDescendantOf(IElement element)
		{
			return _current.Select(c => c.IsDescendantOf(element)).Switch();
		}

		public IObservable<bool> IsChildOf(string elementType)
		{
			return _current.Switch(c => c.IsChildOf(elementType)).DistinctUntilChanged();
		}
		
		public IObservable<bool> IsSiblingOf(string elementType)
		{
			return _current.Switch(c => c.IsSiblingOf(elementType)).DistinctUntilChanged();
		}
	}
}