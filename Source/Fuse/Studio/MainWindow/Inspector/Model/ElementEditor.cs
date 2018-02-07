using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Outracks.Fuse.Editing;
using Outracks.Fuse.Model;

namespace Outracks.Fuse.Live
{
	using Fusion;
	using Simulator;


	public class ElementEditor : IElement
	{
		readonly PreviewController _preview;
		readonly CutCopyPaste _cutCopyPaste;
		readonly InsertElement _insertElement;
		readonly RemoveElement _removeElement;

		public ElementModel Model
		{
			get; private set;
		}

		readonly IObservable<ILookup<ObjectIdentifier, ObjectIdentifier>> _metadata;
		
		readonly IScheduler _scheduler;


		public ElementEditor(
			ElementModel model,
			IObservable<ILookup<ObjectIdentifier, ObjectIdentifier>> metadata,
			PreviewController preview,
			CutCopyPaste cutCopyPaste, 
			InsertElement insertElement, 
			RemoveElement removeElement,
			IScheduler scheduler)
		{
			Model = model;
			_metadata = metadata;
			_cutCopyPaste = cutCopyPaste;
			_scheduler = scheduler;
			_insertElement = insertElement;
			_removeElement = removeElement;
			_preview = preview;
		}

		public ElementEditor(ElementModel model, ElementEditor parent)
		{
			Model = model;
			_insertElement = parent._insertElement;
			_removeElement = parent._removeElement;
			_scheduler = parent._scheduler;
			_metadata = parent._metadata;
			_cutCopyPaste = parent._cutCopyPaste;
			_preview = parent._preview;
		}
		
		public IObservable<bool> IsReadOnly
		{
			get { return Model.Document.IsReadOnly; }
		}


		public IObservable<bool> IsEmpty
		{
			get { return Observable.Return(false); }
		}

		public IElement Base
		{
			get
			{
				return _metadata
					.Select(metadata =>
					{
						//foreach (var baseId in metadata[Model.Id])
						//	return new ElementEditor(Model.(baseId), this);

						return Element.Empty;
					})
					.Switch();
			}
		}

		public IElement Parent
		{
			get { return Model.Parent.IsUnknown ? Element.Empty : new ElementEditor(Model.Parent, this); }
		}

		public IProperty<string> Name
		{
			get
			{
				return new ScheduledProperty<string>(
					new ElementTypeEditor(Model, _preview),
					_scheduler);
			}
		}

		public IObservableList<IElement> Children
		{
			get { return Model.Children.Select(child => new ElementEditor(child, this)); }
		}

		public IProperty<string> Content 
		{
			get
			{
				return new ScheduledProperty<string>(
					new ElementContentEditor(Model, _preview),
					_scheduler);
			} 
		}

		public IAttribute this[string attributeName]
		{
			get
			{
				return new ElementAttributeEditor(
					new ScheduledProperty<string>( 
						new ElementAttributeProperty(Model, attributeName, _preview),
						_scheduler));
			}
		}

		public IObservable<bool> Is(string elementType)
		{
			return Observable.Return(elementType == "Fuse.Elements.Element");
			//return _metadata
			//	.Select(types => Is(Model.Id, new ObjectIdentifier(elementType), types))
			//	.DistinctUntilChanged();
		}

		static bool Is(ObjectIdentifier type, ObjectIdentifier targetBaseType, ILookup<ObjectIdentifier, ObjectIdentifier> baseTypes)
		{
			if (type == targetBaseType)
				return true;

			foreach (var baseType in baseTypes[type])
				if (Is(baseType, targetBaseType, baseTypes))
					return true;

			return false;
		}

		public IObservable<bool> IsChildOf(string type)
		{
			return Parent.Is(type);
		}

		public IObservable<bool> IsDescendantOf(IElement element)
		{
			return Observable.Return(false);
			//return Parent.Id.CombineLatest(element.SimulatorId, (our, their) => our.Equals(their))
			//	.Or(Parent.IsDescendantOf(element));
		}

		public IObservable<bool> IsSiblingOf(string type)
		{
			return Observable.Return(false);

			// Here we exploit the fact that siblings of a LiveElement will always be a LiveElement
			// This is to avoid combining with all siblings.
			//var typeObjectIdentifier = new ObjectIdentifier(type);
			//return _parent.Select(
			//	parent => parent.Children.WherePerElement(x => x != this).CombineLatest(
			//		_metadata,
			//		(siblings, metadata) =>
			//		{
			//			return siblings.OfType<ElementEditor>()
			//				.Any(sibling => Is(sibling._elementId.Value, typeObjectIdentifier, metadata));
			//		})).Or(Observable.Return(false));
		}

		//public IObservable<IEnumerable<IElement>> Siblings()
		//{
		//	return Parent.Children.Select(x => x.ExceptOne(this));
		//}


		public Command Remove()
		{
			return _scheduler.CreateCommand(() => _removeElement.Remove(Model));
		}

		public Command Insert(SourceFragment fragment)
		{
			return _scheduler.CreateCommand(() => _insertElement.Insert(Model, fragment));
		}

		public Command InsertAfter(SourceFragment fragment)
		{
			return _scheduler.CreateCommand(() => _insertElement.InsertAfter(Model, fragment));
		}

		public Command InsertBefore(SourceFragment fragment)
		{
			return _scheduler.CreateCommand(() => _insertElement.InsertBefore(Model, fragment));
		}
	}
}
