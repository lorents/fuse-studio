﻿using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Outracks.Fuse.Live;
using Outracks.Fuse.Model;
using Outracks.Fusion;

namespace Outracks.Fuse.Hierarchy
{
	public partial class TreeViewModel
	{
		class RowModel : ITreeRowViewModel
		{
			readonly BehaviorSubject<bool> _expandToggleEnabled;
			readonly BehaviorSubject<int> _depth = new BehaviorSubject<int>(0);

			readonly BehaviorSubject<Optional<ElementModel>> _element =
				new BehaviorSubject<Optional<ElementModel>>(Optional.None<ElementModel>());

			readonly Command _expandToggleCommand;
			readonly IObservable<string> _headerText;

			readonly IObservable<string> _iconName;
			readonly BehaviorSubject<bool> _isAncestorSelected;

			readonly BehaviorSubject<bool> _isExpanded;
			readonly BehaviorSubject<bool> _isSelected;
			readonly BehaviorSubject<int> _rowOffset = new BehaviorSubject<int>(-10);
			readonly Command _scopeIntoClassCommand;
			readonly Command _selectCommand;
			readonly TreeViewModel _tree;
			bool _isDescendantSelected;
			readonly BehaviorSubject<int> _expandedDescendantCount = new BehaviorSubject<int>(0);
			readonly IObservable<bool> _isFaded;

			public RowModel(TreeViewModel tree)
			{
				_tree = tree;

				var elUxClass = ElementSwitch(el => el.UxClass());
				_iconName = ElementSwitch(el => el.Name, "");

				_isSelected = new BehaviorSubject<bool>(false);
				_isAncestorSelected = new BehaviorSubject<bool>(false);

				_expandToggleEnabled = new BehaviorSubject<bool>(false);
				_isExpanded = new BehaviorSubject<bool>(false);

				_selectCommand = Command.Create(
					ElementSelect(el => Optional.Some<Action>(() => _tree._context.Select(el)), Optional.None<Action>()));

				_headerText = ElementSwitch(GetName, string.Empty);

				_scopeIntoClassCommand = Command.Create(
					ElementSwitch(el => 
						elUxClass
							.CombineLatest(_depth, (uxClass, depth) => uxClass.Where(_ => depth > 0) /* disallow scope into for root */)
							.Select(uxClass => (Action) (() => _tree._context.PushScope(el)))));

				_expandToggleCommand = Command.Create(
					ElementSwitch(
						el => _expandToggleEnabled.Select(
							canExpand => canExpand
								? Optional.Some(
									(Action) (() =>
									{
										if (_isDescendantSelected)
											_tree._context.Select(el);
										_tree.SetElementExpanded(el, !_isExpanded.Value);
									}))
								: Optional.None()),
						Optional.None<Action>()));

				_isFaded = _tree.HighlightSelectedElement.CombineLatest(
					_isSelected,
					_isAncestorSelected,
					(highlightSelectedElement, isSelected, isAncestorSelected) =>
						highlightSelectedElement && !(isSelected | isAncestorSelected));
			}

			static IObservable<string> GetName(ElementModel el)
			{
				return el.Name.CombineLatest(
					el.UxGlobal(), el.UxProperty(), el.UxClass(), el.UxInnerClass(),
					(name, uxGlobal, uxProperty, uxClass, uxInnerClass) =>
					{
						var uxAttrName = 
							(uxGlobal != "") ? uxGlobal :
							(uxProperty != "") ? uxProperty :
							(uxClass != "") ? uxClass :
							(uxInnerClass != "") ? uxInnerClass 
							: "";

						return (uxAttrName != "")
							? uxAttrName + " (" + name + ")"
							: name;
					});
			}

			public Optional<ElementModel> Element
			{
				get { return _element.Value; }
			}


			public void DragEnter(DropPosition position)
			{
				_tree._pendingDrop.OnNext(new PendingDrop(_rowOffset.Value, position, _depth.Value));
			}

			public void DragExit()
			{
				_tree._pendingDrop.OnNext(Optional.None());
			}

			public bool CanDrop(DropPosition position, object dragged)
			{
				return _element.Value.Select(el => GetDropAction(el, position, dragged).HasValue).Or(false);
			}

			public void Drop(DropPosition position, object dragged)
			{
				_element.Value.Do(element => GetDropAction(element, position, dragged).Do(dropAction => dropAction()));
				DragExit();
			}

			public void Update(
				ElementModel element,
				int depth,
				int rowOffset,
				int expandedDescendantCount,
				bool expandToggleEnabled,
				bool isExpanded,
				bool isSelected,
				bool isAncestorSelected,
				bool isDescendantSelected)
			{
				_element.OnNextDistinct(Optional.Some(element));
				_rowOffset.OnNextDistinct(rowOffset);
				_expandedDescendantCount.OnNextDistinct(expandedDescendantCount);
				_depth.OnNextDistinct(depth);
				_isExpanded.OnNextDistinct(isExpanded);
				_expandToggleEnabled.OnNextDistinct(expandToggleEnabled);
				_isSelected.OnNextDistinct(isSelected);
				_isAncestorSelected.OnNextDistinct(isAncestorSelected);
				_isDescendantSelected = isDescendantSelected;
			}

			public void Detach()
			{
				_element.OnNextDistinct(Optional.None<ElementModel>());
				// Hide away outside visible area
				_rowOffset.OnNextDistinct(-10);
				_expandedDescendantCount.OnNextDistinct(0);
			}

			IObservable<Optional<T>> ElementSwitch<T>(Func<ElementModel, IObservable<T>> selector)
			{
				return _element
					.Select(maybeElement =>
						maybeElement.MatchWith(
							some: element => selector(element).Select(Optional.Some),
							none: () => Observable.Return(Optional.None<T>())))
					.Switch();
			}
				
			IObservable<T> ElementSwitch<T>(Func<ElementModel, IObservable<T>> selector, T defaultValue)
			{
				return _element.Switch(y => y.Select(selector).FirstOr(Observable.Return(defaultValue)));
			}

			IObservable<T> ElementSelect<T>(Func<ElementModel, T> selector, T defaultValue)
			{
				return _element.Select(el => el.Select(selector).Or(defaultValue));
			}

			Optional<Action> GetDropAction(ElementModel thisElement, DropPosition position, object dragged)
			{
				var draggedElement = dragged as ElementModel;
				var sourceFragment = dragged as SourceFragment;
				var bytes = dragged as byte[];

				//TODO: Func<SourceFragment> cut;
				if (draggedElement != null)
				{
					var node = thisElement;
					while (node != null)
					{
						if (node.Equals(draggedElement))
							return Optional.None();
						node = node.Parent.IsUnknown ? null : node.Parent;
					}
					
					//TODO: cut = () => draggedElement.Cut().Result;
				}
				else if (bytes != null)
				{
					//TODO: cut = () => SourceFragment.FromBytes(bytes);
				}
				else if (sourceFragment != null)
				{
					//TODO: cut = () => sourceFragment;
				}
				else
				{
					return Optional.None();
				}

				switch (position)
				{
					case DropPosition.Inside:
						if (!_isExpanded.Value)
							return (Action) (
								() =>
								{
									//TODO: var src = cut();
									//TODO: thisElement.Paste(src);
									//TODO move to paste: _tree._context.Select(element);
								});
						break;
					case DropPosition.After:
					case DropPosition.Before:
						if (_depth.Value > 0)
							return (Action) (() =>
							{
								//var src = cut();
								//if (position == DropPosition.Before)
								//	thisElement.PasteBefore(src);
								//else 
								//	thisElement.PasteAfter(src);
								//TODO move to paste: _tree._context.Select(element);
							});
						break;
				}

				return Optional.None();
			}

			#region ITreeRowViewModel implementation

			public IObservable<int> Depth { get { return _depth; } }

			public IObservable<int> ExpandedDescendantCount { get { return _expandedDescendantCount; } }

			public IObservable<bool> CanExpand { get { return _expandToggleEnabled; } }
			public IObservable<bool> IsExpanded { get { return _isExpanded; } }
			public IObservable<bool> IsSelected { get { return _isSelected; } }
			public Command ScopeIntoClassCommand { get { return _scopeIntoClassCommand; } }
			public IObservable<bool> IsAncestorSelected { get { return _isAncestorSelected; } }
			public IObservable<string> ElementName { get { return _iconName; } }
			public IObservable<string> HeaderText { get { return _headerText; } }
			public IObservable<int> RowOffset { get { return _rowOffset; } }
			public Command ExpandToggleCommand { get { return _expandToggleCommand; } }
			public Command SelectCommand { get { return _selectCommand; } }
			IObservable<bool> ITreeRowViewModel.IsFaded { get { return _isFaded; } }

			public Command EnterHoverCommand
			{
				get { return Command.Create(_element.SelectPerElement(el => (Action) (() => _tree._context.Preview(el)))); }
			}

			public Command ExitHoverCommand
			{
				get { return Command.Create(_element.SelectPerElement(el => (Action)(() => _tree._context.Preview(new UnknownElement())))); }
			}

			public IObservable<object> DraggedObject
			{
				get { return _element.Select(el => el.FirstOrDefault()); }
			}

			public Menu ContextMenu
			{
				get
				{
					return Menu.Empty; //_tree._elementMenuFactory(_element.Select(el => el.FirstOr(Editing.Element.Empty)).Switch());
				}
			}

			#endregion
		}
	}
}