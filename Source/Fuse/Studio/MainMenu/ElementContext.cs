using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Outracks.Fuse.Editing;
using Outracks.Fuse.Model;

namespace Outracks.Fuse
{
	using Fusion;

	public class ElementContext
	{
		readonly ProjectModel  _project;
		readonly ContextController _context;
		readonly RemoveElement _removeElement;
		readonly IScheduler _scheduler;

		public ElementContext(
			ProjectModel project, 
			ContextController context, 
			RemoveElement removeElement, 
			IScheduler scheduler)
		{
			_project = project;
			_context = context;
			_removeElement = removeElement;
			_scheduler = scheduler;
		}

		public Menu CreateMenu(ElementModel element)
		{
			return //Menu.Item(
				//	name: "Locate in editor",
				//	command: FocusEditorCommand.Create(_project, _daemon),
				//	hotkey: HotKey.Create(ModifierKeys.Meta | ModifierKeys.Alt, Key.L))

				/*+*/ Menu.Separator

				// Edit class element we're currently not editing 
				+ Menu.Item(
					name: element.UxClass().Select(n => "Edit " + (string.IsNullOrWhiteSpace(n) ? "class" : n)).AsText(),
						isDefault: true,
						action: () => _context.PushScope(element))
					.ShowWhen(element.UxClass().Select(n => !string.IsNullOrEmpty(n))
				/*TODO:.And(project.Scope.IsSameAs(element).IsFalse())*/)

				// Edit base 
				//+ Menu.Item(
				//	name: element.Base.UxClass().Select(n => "Edit " + n.Or("class")).AsText(),
				//	isEnabled: element.Base.IsReadOnly.IsFalse(),
				//	action: () => { _context.PushScope(element.Base, element.Base); })

				+ Menu.Item(
					name: "Deselect",
					hotkey: HotKey.Create(ModifierKeys.Meta, Key.D),
					action: () => _context.Select(new UnknownElement()))

				+ Menu.Separator

				+ Menu.Item(
					name: "Remove element",
					command: Command.Create(
						!element.Parent.IsUnknown,
						() => _scheduler.Schedule(() => _removeElement.Remove(element))));
		}
	}
}