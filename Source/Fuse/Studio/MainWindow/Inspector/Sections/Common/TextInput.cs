using Outracks.Fuse.Live;
using Outracks.Fuse.Model;

namespace Outracks.Fuse.Inspector.Sections
{
	using Fusion;
	
	class TextInputSection
	{
		public static IControl Create(ProjectModel project, IElement element, IEditorFactory editors)
		{
			var placeholder = element["PlaceholderText"];
			var placeholdercolor = element["PlaceholderColor"];

			return Layout.StackFromTop(
					Spacer.Medium,
					
					TextSection.CreateEditors(project, element, editors)
						.WithInspectorPadding(),
					
					Spacer.Medium,
					
					editors.Field(placeholder)
						.WithLabel("Placeholder")
						.WithInspectorPadding(),
					
					Spacer.Medium,
					
					editors.Color(placeholdercolor)
						.WithLabel("Placeholder Color")
						.WithInspectorPadding(),
					
					Spacer.Medium)
				.MakeCollapsable(RectangleEdge.Bottom, element.Is("Fuse.Controls.TextInput"));
		}
	}
}
