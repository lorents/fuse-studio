using System;
using System.Reactive.Linq;
using Outracks.Fuse.Model;
using Outracks.Simulator;

namespace Outracks.Fuse
{
	public class ContextController 
	{
		readonly ProjectModel _project;

		public ProjectModel Project
		{
			get { return _project; }
		}

		public ContextController(ProjectModel project)
		{
			_project = project;
			CurrentSelection = project.Scope
				.Select(scope => scope.CurrentSelection)
				.Switch().Replay(1).RefCount();

			PreviewedSelection = project.Scope
				.Select(scope => scope.CurrentSelection)
				.Switch().Replay(1).RefCount();

			CurrentScope = project.Scope
				.Select(scope => scope.Root)
				.Replay(1).RefCount();

			PreviousScope = project.Scope
				.Select(scope => scope.Parent.Select(ps => ps.Root).Or(new UnknownElement()))
				.Replay(1).RefCount();
		}

		// Selection

		public IObservable<ElementModel> CurrentSelection { get; private set; }

		public IObservable<ElementModel> PreviewedSelection { get; private set; }

		public void Select(ObjectIdentifier id)
		{
			Select(_project.FindElement(id));
		}

		public void Preview(ObjectIdentifier id)
		{
			Preview(_project.FindElement(id));
		}

		public void Select(ElementModel element)
		{
			_project.Scope.Value.CurrentSelection.OnNext(element);
		}

		public void Preview(ElementModel element)
		{
			_project.Scope.Value.PreviewedSelection.OnNext(element);
		}

		public IObservable<bool> IsSelected(ElementModel element)
		{
			return CurrentSelection.Is(element);
		}

		public IObservable<bool> IsPreviewSelected(ElementModel element)
		{
			return PreviewedSelection.Is(element);
		}

		// Scope

		public IObservable<ElementModel> CurrentScope { get; private set; }

		public IObservable<ElementModel> PreviousScope { get; private set; }

		public void PopScope()
		{
			var parent = _project.Scope.Value.Parent;
			if (parent.HasValue)
				_project.Scope.OnNext(parent.Value);
		}

		public void PushScope(ElementModel root)
		{
			_project.Scope.OnNext(new Scope(root, _project.Scope.Value));
		}

		public void SetScope(ElementModel root)
		{
			_project.Scope.OnNext(new Scope(root));
		}
	}
}