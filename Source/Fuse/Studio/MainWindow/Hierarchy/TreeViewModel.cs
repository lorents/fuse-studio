using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using Outracks.Fuse.Live;
using Outracks.Fuse.Model;
using Outracks.Fusion;

namespace Outracks.Fuse.Hierarchy
{
	public partial class TreeViewModel : ITreeViewModel
	{
		readonly IObservable<IEnumerable<ITreeRowViewModel>> _visibleRows;
		readonly IObservable<int> _totalRowCount;

		readonly ContextController _context;
		readonly IObservable<bool> _highlightSelectedElement;

		readonly BehaviorSubject<int> _visibleRowCount = new BehaviorSubject<int>(1);
		readonly BehaviorSubject<int> _visibleRowOffset = new BehaviorSubject<int>(0);
		readonly Subject<int> _scrollTarget = new Subject<int>();
		readonly Func<ElementModel, Menu> _elementMenuFactory;

		readonly BehaviorSubject<Unit> _expandToggled =
			new BehaviorSubject<Unit>(Unit.Default);

		readonly BehaviorSubject<Optional<PendingDrop>> _pendingDrop =
			new BehaviorSubject<Optional<PendingDrop>>(Optional.None<PendingDrop>());

		readonly Command _navigateUpCommand;
		readonly Command _navigateDownCommand;

		public TreeViewModel(
			ContextController context,
			IObservable<bool> highlightSelectedElement,
			Func<ElementModel, Menu> elementMenuFactory)
		{
			_elementMenuFactory = elementMenuFactory;
			_context = context;
			_highlightSelectedElement = highlightSelectedElement.Replay(1).RefCount();

			var moveToSelectedElement = false;

			var inputs = Observable
				.CombineLatest(
					context.CurrentScope.Select(s => s.Changed.StartWith(new object()).Select(_ => s)).Switch(),
					context.CurrentSelection.Do(el => moveToSelectedElement = !el.IsUnknown),
					context.PreviewedSelection,
					_visibleRowCount.DistinctUntilChanged(),
					_visibleRowOffset.DistinctUntilChanged(),
					_expandToggled,
					( scopeRoot, selectedElement, previewElement, visibleRowCount, visibleRowOffset, expanded ) => new 
					{ scopeRoot, selectedElement, previewElement, visibleRowCount, visibleRowOffset, expanded });

			var state = inputs
				.Scan(new State(this), (s, x) => s.Update(x.scopeRoot, x.visibleRowCount, x.visibleRowOffset, x.selectedElement, x.previewElement))
				.Do(nextState =>
				{
					if (moveToSelectedElement)
					{
						moveToSelectedElement = false;
						if (nextState.SelectedElementIsOutsideVisibleRange)
						{
							_scrollTarget.OnNext(nextState.SelectedElementRowOffset.Value);
						}
					}
				})
				.Replay(1)
				.RefCount();

			_visibleRows = state.Select(x => x.VisibleRows).DistinctUntilChanged();
			_totalRowCount = state.Select(x => x.TotalRowCount).DistinctUntilChanged();

			_navigateUpCommand = Command.Create(
				state.Select(x => x.ElementBeforeSelected.Select(el => (Action) (() => _context.Select(el)))));
			_navigateDownCommand = Command.Create(
				state.Select(x => x.ElementAfterSelected.Select(el => (Action) (() => _context.Select(el)))));
		}

		void SetElementExpanded(ElementModel element, bool expand)
		{
			if (expand)
				element.Tags.OnAdd(Expanded);
			else
			{
				var index = element.Tags.Value.IndexOf(Expanded);
				if (index != -1)
					element.Tags.OnRemove(index);
			}

			_expandToggled.OnNext(Unit.Default);
		}

		bool GetElementExpanded(ElementModel element, bool fallback)
		{
			return element.Tags.Value.Contains(Expanded);
		}

		const string Expanded = "Expanded";

		public int VisibleRowCount
		{
			get { return _visibleRowCount.Value; }
			set { _visibleRowCount.OnNextDistinct(value); }
		}

		public int VisibleRowOffset
		{
			get { return _visibleRowOffset.Value; }
			set { _visibleRowOffset.OnNextDistinct(value); }
		}

		public IObservable<int> ScrollTarget
		{
			get { return _scrollTarget; }
		}

		public IObservable<int> TotalRowCount
		{
			get { return _totalRowCount; }
		}

		public IObservable<IEnumerable<ITreeRowViewModel>> VisibleRows
		{
			get { return _visibleRows; }
		}

		public Command PopScopeCommand
		{
			get
			{
				return Command.Create(
					_context.PreviousScope.Select(scope => 
						scope.IsUnknown 
							? Optional.None<Action>() 
							: Optional.Some<Action>(() => _context.PopScope())));
			}
		}

		public IObservable<string> PopScopeLabel
		{
			get
			{
				return _context.PreviousScope
					.Select(scope => 
						Observable.CombineLatest(
							scope.UxClass(), scope.Name, 
							(className, name) => "Return to \"" + name + "\""))
					.Switch();
			}
		}

		public IObservable<bool> HighlightSelectedElement
		{
			get { return _highlightSelectedElement; }
		}

		public IObservable<Optional<PendingDrop>> PendingDrop
		{
			get { return _pendingDrop; }
		}

		public Command NavigateUpCommand
		{
			get { return _navigateUpCommand; }
		}

		public Command NavigateDownCommand
		{
			get { return _navigateDownCommand; }
		}

		class State
		{
			readonly TreeViewModel _tree;

			readonly int _totalRowCount;
			readonly Func<IImmutableList<RowModel>, IImmutableList<RowModel>> _applyFunc;
			readonly Optional<int> _selectedElementRowOffset;

			public Optional<int> SelectedElementRowOffset
			{
				get { return _selectedElementRowOffset; }
			}

			public Optional<ElementModel> ElementAfterSelected
			{
				get
				{
					return _selectedElementRowOffset.Select(sel => _allExpandedElements.ElementAtOrDefault(sel + 1)).FirstOrNone();
				}
			}

			public Optional<ElementModel> ElementBeforeSelected
			{
				get
				{
					return _selectedElementRowOffset.Select(sel => _allExpandedElements.ElementAtOrDefault(sel - 1)).FirstOrNone();
				}
			}

			readonly int _visibleRowOffset;
			readonly int _visibleRowCount;
			readonly IList<ElementModel> _allExpandedElements;
			readonly IImmutableList<RowModel> _visibleRows;

			public int TotalRowCount
			{
				get { return _totalRowCount; }
			}

			public IImmutableList<RowModel> VisibleRows
			{
				get { return _visibleRows; }
			}

			public bool SelectedElementIsOutsideVisibleRange
			{
				get
				{
					return _selectedElementRowOffset
						.Select(sel => sel < _visibleRowOffset || sel >= (_visibleRowOffset + _visibleRowCount - 1)).Or(false);
				}
			}

			public State(TreeViewModel tree) : this(
				tree,
				ImmutableList.Create<RowModel>(),
				0,
				x => x,
				Optional.None(),
				0,
				0,
				new ElementModel[] { }) { }


			State(
				TreeViewModel tree,
				IImmutableList<RowModel> visibleRows,
				int totalRowCount,
				Func<IImmutableList<RowModel>, IImmutableList<RowModel>> applyFunc,
				Optional<int> selectedElementRowOffset,
				int visibleRowOffset,
				int visibleRowCount,
				IList<ElementModel> allExpandedElements)
			{
				_tree = tree;
				_visibleRows = visibleRows;
				_totalRowCount = totalRowCount;
				_applyFunc = applyFunc;
				_selectedElementRowOffset = selectedElementRowOffset;
				_visibleRowOffset = visibleRowOffset;
				_visibleRowCount = visibleRowCount;
				_allExpandedElements = allExpandedElements;
			}

			public State Update(
				Optional<ElementModel> root,
				int visibleRowCount,
				int visibleRowOffset,
				Optional<ElementModel> selectedElement,
				Optional<ElementModel> previewElement)
			{
				return root.Select(
						el =>
							ScanElementTree(el, visibleRowCount, visibleRowOffset, selectedElement, previewElement)
							.ApplyUpdates())
					.Or(() => new State(_tree));
			}

			State ApplyUpdates()
			{
				return new State(
					_tree,
					_applyFunc(VisibleRows),
					TotalRowCount,
					x => x,
					_selectedElementRowOffset,
					_visibleRowOffset,
					_visibleRowCount,
					_allExpandedElements);
			}

			State ScanElementTree(
				ElementModel root,
				int visibleRowCount,
				int visibleRowOffset,
				Optional<ElementModel> selectedElement,
				Optional<ElementModel> previewElement)
			{
				// Update visible rows  ake sure we have enough
				var updates = new Dictionary<ElementModel, Action<RowModel>>();
				var allExpandedElements = new List<ElementModel>(_allExpandedElements.Count * 2);

				var selectedElementRowOffset = Optional.None<int>();
				var previewElementRowOffset = Optional.None<int>();

				ScanElementTree(
					treeModel: this._tree,
					element: root,
					updates: updates,
					allExpandedElements: allExpandedElements,
					visibleRowCount: visibleRowCount,
					visibleRowOffset: visibleRowOffset,
					depth: 0,
					selectionPath: GetSelectionPath(root, selectedElement),
					previewElement: previewElement,
					isAncestorSelected: false,
					selectedElementRowOffset: ref selectedElementRowOffset,
					previewElementRowOffset: ref previewElementRowOffset);

				return new State(
					_tree,
					VisibleRows,
					allExpandedElements.Count,
					visibleRows => UpdateRows(visibleRows, updates),
					selectedElementRowOffset,
					visibleRowOffset,
					visibleRowCount,
					allExpandedElements);
			}

			IImmutableList<RowModel> UpdateRows(
				IImmutableList<RowModel> visibleRows,
				Dictionary<ElementModel, Action<RowModel>> updates)
			{

				//while (visibleRows.Count < 30)
				//	visibleRows = visibleRows.Add(new RowModel(_tree));

				// First update all rows currently attached to an element
				Queue<RowModel> freeRows = new Queue<RowModel>();
				foreach (var rowModel in visibleRows)
				{
					var rowModelLocal = rowModel; // Shouldn't be necesarry in C# 5 or higher, doing it just to be safe.
					rowModel.Element.Do(
						el =>
						{
							Action<RowModel> update;
							if (updates.TryGetValue(el, out update))
							{
								update(rowModelLocal);
								updates.Remove(el);
							}
							else
							{
								freeRows.Enqueue(rowModelLocal);
							}
						},
						() => { freeRows.Enqueue(rowModelLocal); });
				}

				// Now update all remaining elements
				foreach (var update in updates.Values)
				{
					RowModel rowModel;
					if (freeRows.Count > 0)
					{
						rowModel = freeRows.Dequeue();
					}
					else
					{
						rowModel = new RowModel(_tree);
						visibleRows = visibleRows.Add(rowModel);
					}
					update(rowModel);
				}

				// And then detach any leftover rows
				// (we don't free row templates if tree is resized smaller)
				while (freeRows.Count > 0)
				{
					freeRows.Dequeue().Detach();
				}
				return visibleRows;
			}


			/// <summary>
			/// Walks through all expanded elements of tree, adding actions to updates for pending row updates.
			/// Returns index of selected element
			/// </summary>
			static void ScanElementTree(
				TreeViewModel treeModel,
				ElementModel element,
				Dictionary<ElementModel, Action<RowModel>> updates,
				List<ElementModel> allExpandedElements,
				int visibleRowCount,
				int visibleRowOffset,
				int depth,
				ElementModel[] selectionPath,
				Optional<ElementModel> previewElement,
				bool isAncestorSelected,
				ref Optional<int> selectedElementRowOffset,
				ref Optional<int> previewElementRowOffset)
			{
				var rowOffset = allExpandedElements.Count;
				allExpandedElements.Add(element);

				// A node with children can expand, unless it has a "ux:Class" attribute
				var canExpand = CanExpand(element, depth);

				// We expand up to 2 levels by default
				var expandByDefault = depth < 2;

				// Check if this element is an ancestor of selection
				var isDescendantSelected = depth < (selectionPath.Length - 1) && selectionPath[depth].Equals(element);

				// This is the selected element when we're at the end of the selection path
				var isSelected = depth == (selectionPath.Length - 1) && selectionPath[depth].Equals(element);

				var isExpanded = canExpand && (isDescendantSelected || treeModel.GetElementExpanded(element, expandByDefault));

				if (previewElement.Select(pe => pe == element).Or(false))
					previewElementRowOffset = rowOffset;

				if (isSelected)
				{
					selectedElementRowOffset = rowOffset;
				}

				var children = element.Children.Value;


				if (isExpanded)
				{
					foreach (var child in children)
					{
						ScanElementTree(
							treeModel: treeModel,
							element: child,
							updates: updates,
							allExpandedElements: allExpandedElements,
							visibleRowCount: visibleRowCount,
							visibleRowOffset: visibleRowOffset,
							depth: depth + 1,
							selectionPath: selectionPath,
							previewElement: previewElement,
							isAncestorSelected: isSelected | isAncestorSelected,
							selectedElementRowOffset: ref selectedElementRowOffset,
							previewElementRowOffset: ref previewElementRowOffset);
					}
				}

				var lastDescendantRowOffset = allExpandedElements.Count - 1;

				if ((rowOffset >= visibleRowOffset && rowOffset < visibleRowOffset + visibleRowCount) ||
					(rowOffset < visibleRowOffset && lastDescendantRowOffset >= visibleRowOffset))
				{
					updates[element] = rowModel => rowModel.Update(
						element: element,
						depth: depth,
						rowOffset: rowOffset,
						expandedDescendantCount: lastDescendantRowOffset - rowOffset,
						expandToggleEnabled: depth > 0 && canExpand, // Don't allow collapse of root
						isExpanded: isExpanded,
						isSelected: isSelected,
						isAncestorSelected: isAncestorSelected,
						isDescendantSelected: isDescendantSelected);
				}
			}

			static bool CanExpand(ElementModel element, int depth)
			{
				return element.Children.Value.Any() && (depth == 0 || !element.Attributes.ContainsKey("ux:Class"));
			}

			static ElementModel[] GetSelectionPath(ElementModel root, Optional<ElementModel> selectedElement)
			{
				if (!selectedElement.HasValue)
					return new ElementModel[] { };

				// First we count depth and see if selection is descendant of our root
				var selected = selectedElement.Value;
				var current = selected;
				int depth = 0;
				while (current != root)
				{
					var liveParent = current.Parent.IsUnknown ? null : current.Parent;
					if (liveParent == null)
					{
						return new ElementModel[] { };
					}
					depth++;
					current = liveParent;
				}

				// Then we walk down again and build the selection array
				var selectionPath = new ElementModel[depth + 1];
				current = selected;
				do
				{
					selectionPath[depth] = current;
					current = current.Parent.IsUnknown ? null : current.Parent;
				} while (depth-- > 0);

				return selectionPath;
			}
		}
	}
}