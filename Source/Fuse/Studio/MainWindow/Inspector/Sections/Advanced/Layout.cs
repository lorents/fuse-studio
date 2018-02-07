using Outracks.Fuse.Editing;

namespace Outracks.Fuse.Inspector.Sections
{
	using Fusion;

	class LayoutSection
	{
		public static IControl Create(IElement element, IEditorFactory editors)
		{
			element = element.As("Fuse.Elements.Element");

			var alignment = element["Alignment"];
			var dock = element["Dock"];
			var layoutRole = element["LayoutRole"];
			var layer = element["Layer"];
			
			return Layout.StackFromTop(
				Separator.Weak,
				Spacer.Medium,

				AlignmentEditor.Create(alignment, editors).WithInspectorPadding(),

				Spacer.Medium, Separator.Weak, 

				Layout.StackFromTop(
						Spacer.Medium,
						DockEditor.Create(dock, editors).WithInspectorPadding(),
						Spacer.Medium, Separator.Weak)
					.MakeCollapsable(RectangleEdge.Bottom, element.IsInDockPanelContext()),

				Spacer.Medium,

				SpacingSection.Create(element, editors),
				
				Spacer.Medium, Separator.Weak, Spacer.Medium,
				
				Layout.Dock()
					.Left(editors.Dropdown(layoutRole, LayoutRole.Standard).WithLabelAbove("Layout Role"))
					.Right(editors.Dropdown(layer, Layer.Layout).WithLabelAbove("Layer"))
					.Fill()
					.WithInspectorPadding(),

				Spacer.Medium,
				Separator.Weak);
		}
	}

	enum Layer
	{
		Underlay,
		Background,
		Layout,
		Overlay,
	}

	enum LayoutRole
	{
		Standard,
		Placeholder,
		Inert,
		Independent,
	}
}
