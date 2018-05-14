﻿using Outracks.Fuse.Designer.Inspector;

namespace Outracks.Fuse.Inspector.Sections
{
	using Fusion;
	
	class StackPanelSection
	{
		public static IControl Create(IElement element, IEditorFactory editors)
		{
			var orientation = element["Orientation"];
			var itemSpacing = element["ItemSpacing"];

			return Layout.StackFromTop(
					Spacer.Medium,
					
					Layout.StackFromLeft(
							editors.RadioButton(orientation, Orientation.Vertical)
								.Option(Orientation.Horizontal, (fg, bg) => StackIcon.Create(Axis2D.Horizontal, fg), "Orientation: Horizontal")
								.Option(Orientation.Vertical, (fg, bg) => StackIcon.Create(Axis2D.Vertical, fg), "Orientation: Vertical")
								.Control.WithLabel("Orientation"),
							Spacer.Medium,
							editors.Label("Item Spacing", itemSpacing),
							editors.Field(itemSpacing)
								.WithWidth(CellLayout.HalfCellWidth))
						.WithInspectorPadding(),
					
					Spacer.Medium)
				.MakeCollapsable(RectangleEdge.Bottom, element.Is("Fuse.Controls.StackPanel"));
		}
	}
	public enum Orientation
	{
		Horizontal,
		Vertical
	}

}
