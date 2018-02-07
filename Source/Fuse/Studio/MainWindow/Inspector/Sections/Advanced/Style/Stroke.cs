namespace Outracks.Fuse.Inspector.Sections
{
	using Fusion;

	static class StrokeSection
	{
		public static IControl Create(IElement element, IEditorFactory editors)
		{
			return editors.ElementList(
				"Stroke", element,
				SourceFragment.FromString("<Stroke Width=\"3\" Color=\"#f00\"/>"),
				stroke => CreateStrokeRow(stroke, editors)
					.WithInspectorPadding())
				.MakeCollapsable(RectangleEdge.Top, element.Is("Fuse.Controls.Shape"));
		}

		static IControl CreateStrokeRow(IElement stroke, IEditorFactory editors)
		{
			var name = stroke.UxName();
			var color = stroke["Color"];
			var alignment = stroke["Alignment"];
			var width = stroke["Width"];

			return Layout.StackFromTop(
				editors.NameRow("Stroke Name", name),

				Spacer.Medium,

				Layout.Dock()
					.Left(editors.Color(color).WithLabelAbove("Color"))
					.Left(Spacer.Small)
					.Right(
						editors.Field(width).WithLabelAbove("Width")
							.WithWidth(CellLayout.HalfCellWidth))
					.Right(Spacer.Small)
					.Fill(editors.Dropdown(alignment, StrokeAlignment.Inside)
						.WithLabelAbove("Alignment")));
		}
	}

	enum StrokeAlignment
	{
		Center,
		Inside,
		Outside,
	}
}