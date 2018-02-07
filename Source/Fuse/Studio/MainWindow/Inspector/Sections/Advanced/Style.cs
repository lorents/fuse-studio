using Outracks.Fuse.Editing;

namespace Outracks.Fuse.Inspector.Sections
{
	using Fusion;
	
	class StyleSection
	{
		public static IControl Create(IElement element, IEditorFactory editors)
		{
			//TODO element = element.As("Fuse.Elements.Element");

			var color = element["Color"];
			var background = element["Background"];
			var opacity = element["Opacity"];
	
			return Layout.StackFromTop(
				Separator.Weak,
				Spacer.Medium,

				Layout.Dock()
					.Left(editors.Color(color).WithLabelAbove("Color"))
					.Right(editors.Color(background).WithLabelAbove("Background"))
					.Fill()
					.WithInspectorPadding(),

				Spacer.Medium, Separator.Weak, Spacer.Medium,

				Layout.Dock()
					.Left(editors.Label("Opacity", opacity))
					.Left(Spacer.Medium)
					.Right(editors.Field(opacity).WithWidth(CellLayout.HalfCellWidth))
					.Right(Spacer.Small)
					.Fill(editors.Slider(opacity, min: 0.0, max: 1.0))
					.WithInspectorPadding(),
				
				Spacer.Medium,
				Separator.Weak,
				Spacer.Medium,
				Label.Create(
					"Effects are added as \n" +
					"children of the current element",
					font: Theme.DefaultFont,
					color: Theme.DescriptorText,
					textAlignment: TextAlignment.Center),
				Spacer.Medium,
				StrokeSection.Create(element, editors),
				LinearGradientSection.Create(element, editors),
				SolidColorSection.Create(element, editors),
				DropShadowSection.Create(element, editors),
				Separator.Weak);
		}
	}
}
