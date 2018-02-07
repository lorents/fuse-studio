using Outracks.Fuse.Editing;

namespace Outracks.Fuse.Inspector.Sections
{
	using Fusion;

	public class TransformSection
	{
		public static IControl Create(IElement element, IEditorFactory editors)
		{
			element = element.As("Fuse.Elements.Element");

			var snapMin = element["SnapMinTransform"];
			var snapMax = element["SnapMaxTransform"];
			var origin = element["TransformOrigin"];

			return Layout.StackFromTop(
				Separator.Weak,
				Spacer.Medium,

				editors.Dropdown(origin, TransformOrigin.Center).WithLabel("Transform Origin")
					.WithInspectorPadding(),
			
				Spacer.Medium,
				
				Layout.StackFromTop(
						editors.Switch(snapMin).WithLabel("Snap to Min Transform"),
						Spacer.Medium,
						editors.Switch(snapMax).WithLabel("Snap to Max Transform"),
						Spacer.Medium)
					.MakeCollapsable(RectangleEdge.Top, element.Is("Fuse.Controls.ScrollViewBase"))
					.WithInspectorPadding(),
			
				RotationSection.Create(element, editors),
				Separator.Weak);
		}
	}

	// Not really an enum, but from UX it looks like one so this should be fine
	enum TransformOrigin
	{
		Anchor,
		Center,
		HorizontalBoxCenter,
		TopLeft,
		VerticalBoxCenter,
	}
}
