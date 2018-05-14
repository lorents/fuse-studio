﻿
namespace Outracks.Fuse.Inspector.Sections
{
	using Fusion;
	
	class GridSection
	{
		public static IControl Create(IElement element, IEditorFactory editors)
		{
			var spacing = element["CellSpacing"];
			var order = element["ChildOrder"];

			return Layout.StackFromTop(
					Spacer.Medium,

					Layout.StackFromLeft(
							editors.Field(spacing).WithLabelAbove("Cell Spacing"),
							Spacer.Medium,
							editors.Dropdown(order, GridChildOrder.RowMajor).WithLabelAbove("Child Order"))
						.WithInspectorPadding(),

					Spacer.Medium, Separator.Weak, Spacer.Medium,
					
					Dimension(element, editors, "Row"),
					
					Spacer.Medium, Separator.Weak, Spacer.Medium,

					Dimension(element, editors, "Column"),
					
					Spacer.Medium)
				.MakeCollapsable(RectangleEdge.Bottom, element.Is("Fuse.Controls.Grid"));
		}

		static IControl Dimension(IElement element, IEditorFactory editors, string name)
		{
			var plural = name + "s";
			var p = element[plural];
			var count = element[name + "Count"];
			var def = element["Default" + name];
			return Layout.StackFromTop(
				Layout.Dock()
					.Right(editors.Field(def)
						.WithLabelAbove("Default")
						.WithWidth(CellLayout.HalfCellWidth))
					.Right(Spacer.Medium)
					.Right(editors.Field(count)
						.WithLabelAbove("Count")
						.WithWidth(CellLayout.HalfCellWidth))
					.Right(Spacer.Medium)
					.Fill(editors.Field(p).WithLabelAbove(plural))
					.WithInspectorPadding());
		}
	}

	enum GridChildOrder
	{
		RowMajor,
		ColumnMajor,
	}
}
