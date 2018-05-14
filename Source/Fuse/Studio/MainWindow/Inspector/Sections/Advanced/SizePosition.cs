﻿using Outracks.Fuse.Editing;

namespace Outracks.Fuse.Inspector.Sections
{
	using Fusion;
	
	class SizePositionSection
	{
		public static IControl Create(IElement element, IEditorFactory editors)
		{
			element = element.As("Fuse.Elements.Element");

			var x = element["X"];
			var y = element["Y"];

			//var offset = element.GetSize2("Offset", Size.Create(UxSize.Points(0.0), UxSize.Points(0.0))).Transpose(UxSize.Points(0.0));

			var width = element["Width"];
			var height = element["Height"];
			var maxWidth = element["MaxWidth"];
			var maxHeight = element["MaxHeight"];
			var minWidth = element["MinWidth"];
			var minHeight = element["MinHeight"];

			return Layout.StackFromTop(
				
				Spacer.Medium,
				Layout.Dock()
					.Left(Layout.StackFromTop(
							editors.Label(" ", width, height),
							Spacer.Smaller,
							editors.Label("Width", width),
							Spacer.Small,
							editors.Label("Height", height))
						.WithWidth(CellLayout.HalfCellWidth))
					.Left(Spacer.Small)
					.Left(Layout.StackFromTop(
							editors.Label(" ", width, height),
							Spacer.Smaller,
							editors.Field(width, toolTip: "Width"),
							Spacer.Small,
							editors.Field(height, toolTip: "Height"))
						.WithWidth(CellLayout.HalfCellWidth))
					.Right(Layout.StackFromTop(
							editors.Label("Max", maxWidth, maxHeight),
							Spacer.Smaller,
							editors.Field(maxWidth, toolTip: "Maximum width"),
							Spacer.Small,
							editors.Field(maxHeight, toolTip: "Maximum height"))
						.WithWidth(CellLayout.HalfCellWidth))
					.Right(Spacer.Small)
					.Right(Layout.StackFromTop(
							editors.Label("Min", minWidth, minHeight),
							Spacer.Smaller,
							editors.Field(minWidth, toolTip: "Minimum width"),
							Spacer.Small,
							editors.Field(minHeight, toolTip: "Minimum height"))
						.WithWidth(CellLayout.HalfCellWidth))
					.Fill(Spacer.Small)
					.WithInspectorPadding(),
				Spacer.Medium,
				Separator.Weak,
				Spacer.Medium,

				Layout.Dock()
					.Fill(
						Layout.Dock()
						.Left(editors.Label("X", x)
							.WithWidth(26)
							.DockLeft(editors.Field(x)).WithWidth(CellLayout.FullCellWidth))
						.Right(editors.Label("Y", y)
							.WithWidth(26)
							.DockLeft(editors.Field(y)).WithWidth(CellLayout.FullCellWidth))
						.Fill(Spacer.Medium))

					//.Right(Layout.StackFromTop(
					//		editors.Label("\u2194", offset.Width)
					//			.CenterHorizontally().WithWidth(26)
					//			.DockLeft(editors.Field(offset.Width)),
					//		Spacer.Small,
					//		editors.Label("\u2195", offset.Height)
					//			.CenterHorizontally().WithWidth(26)
					//			.DockLeft(editors.Field(offset.Height)))
					//	.WithWidth(CellLayout.FullCellWidth))

					
					.WithInspectorPadding(),

				Spacer.Medium, Separator.Weak);
		}
	}
}
