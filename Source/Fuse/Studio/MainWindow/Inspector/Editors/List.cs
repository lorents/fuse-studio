using System;
using System.Linq;
using System.Reactive.Linq;
using Outracks.Fuse.Editing;

namespace Outracks.Fuse.Inspector.Editors
{
	using Fusion;
	
	public static class ListEditor
	{
		public static IControl Create(IElement parent, Text name, SourceFragment fragment, Func<IElement, IControl> content)
		{
			var type = fragment.ToXml().Name.LocalName;
			var children = parent.Children
				.Where(e => e.Name.Is(type))
				.Replay().RefCount()
				;

			var hasContent = children.IsNonEmpty()
				.StartWith(false)
				.Replay(1).RefCount();

			var selectedChild = children.LastOr(Element.Empty).Switch();

			var stackedContent = children
				.ToObservableImmutableList()
				.PoolPerElement(e => content(e.Switch()))
				.StackFromTop(separator: () => Spacer.Medium)
				;

			var textColor = parent.IsReadOnly
				.Select(ro => ro ? Theme.DisabledText : Theme.DefaultText)
				.Switch();

			return Layout.StackFromTop(
				Separator.Weak,
				Layout.Dock()
					.Left(Label.Create(name, Theme.DefaultFont, color: textColor)
						.CenterVertically())
					.Right(ListButtons.AddButton(parent.Insert(fragment))
						.CenterVertically())
					.Right(Spacer.Small)
					.Right(ListButtons.RemoveButton(selectedChild.Remove())
						.CenterVertically())
					.Fill()
					.WithHeight(30)
					.WithInspectorPadding(),
				Separator.Weak.ShowWhen(hasContent),
				Separator.Weak.ShowWhen(hasContent),
				Layout.StackFromTop(
						Spacer.Medium, 
						stackedContent, 
						Spacer.Medium,
						Separator.Weak.ShowWhen(hasContent))
					.MakeCollapsable(RectangleEdge.Bottom, hasContent));
		}
	}
}