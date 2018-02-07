using System.Reactive.Linq;
using System.Threading.Tasks;
using Outracks.Fuse.Model;
using Outracks.Simulator;

namespace Outracks.Fuse.Inspector.Sections
{
	using Fusion;
	
	class ImageSection
	{
		public static IControl Create(ProjectModel project, IElement element, IEditorFactory editors)
		{
			var file = element["File"];
			var stretchMode = element["StretchMode"];

			var stretchDirection = element["StretchDirection"];
			var stretchSizing = element["StretchSizing"];

			return Layout.StackFromTop(
					Spacer.Medium,
					
					editors.FilePath(
							attribute: file,
							projectRoot: Observable.Return(project.RootDirectory),
							fileFilters: new[] { new FileFilter("Image Files", ".png", ".jpg", ".jpeg")  },
							placeholderText: "Path to image file")
						.WithInspectorPadding(),
					
					Spacer.Medium,

					editors.Dropdown(stretchMode, StretchMode.Uniform)
						.WithLabel("Stretch Mode")
						.WithInspectorPadding(),

					Spacer.Medium,
					
					Layout.Dock()
						.Left(editors.Dropdown(stretchDirection, StretchDirection.Both).WithLabelAbove("Stretch Direction"))
						.Right(editors.Dropdown(stretchSizing, StretchSizing.Zero).WithLabelAbove("Stretch Sizing"))
						.Fill().WithInspectorPadding(),

					Spacer.Medium)
				.MakeCollapsable(RectangleEdge.Bottom, element.Is("Fuse.Controls.Image"));
		}
	}

	enum StretchMode
	{
		PointPrecise,
		PixelPrecise,
		PointPrefer,
		Fill,
		Scale9,
		Uniform,
		UniformToFill,
	}

	enum StretchDirection
	{
		Both,
		UpOnly,
		DownOnly,
	}

	enum StretchSizing
	{
		Zero,
		Natural,
	}
}
