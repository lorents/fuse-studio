namespace Outracks.Fuse.Inspector.Sections
{
	using Fusion;
	
	class EachSection
	{
		public static IControl Create(IElement element, IEditorFactory editors)
		{
			var items = element["Items"];
			var count = element["Count"];

			return Layout.StackFromTop(
					Spacer.Medium,
					
					Layout.StackFromLeft(
							editors.Field(items).WithLabelAbove("Items"),
							Spacer.Medium,
							editors.Field(count).WithLabelAbove("Count"))
						.WithInspectorPadding(),
					
					Spacer.Medium)
				.MakeCollapsable(RectangleEdge.Bottom, element.Is("Fuse.Reactive.Each"));
		}
	}
}
