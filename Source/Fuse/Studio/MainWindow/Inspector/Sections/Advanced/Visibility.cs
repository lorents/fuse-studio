using Outracks.Fuse.Editing;

namespace Outracks.Fuse.Inspector.Sections
{
	using Fusion;

	class VisibilitySection
	{
		public static IControl Create(IElement element, IEditorFactory editors)
		{
			element = element.As("Fuse.Elements.Element");

			var visibility = element["Visibility"];
			var hitTestMode = element["HitTestMode"];

			return Layout.StackFromTop(
				Separator.Weak,
				Spacer.Medium,
				editors.Dropdown(visibility, Visibility.Visible).WithLabel("Visibility")
					.WithInspectorPadding(),
				Spacer.Medium,
				editors.Dropdown(hitTestMode, HitTestMode.None).WithLabel("Hit Test Mode")
					.WithInspectorPadding(),
				Spacer.Medium,
				Separator.Weak);
		}
	}

	enum HitTestMode
	{
		None,
		LocalVisual,
		LocalBounds,
		Children,
		LocalVisualAndChildren,
		LocalBoundsAndChildren,
	}

	enum Visibility
	{
		Visible, 
		Hidden, 
		Collapsed
	}
}
