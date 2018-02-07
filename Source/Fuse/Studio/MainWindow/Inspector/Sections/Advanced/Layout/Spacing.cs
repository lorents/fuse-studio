using System;
using System.Reactive.Linq;
using Outracks.Fuse.Inspector.Editors;
using Outracks.Fuse.Stage;

namespace Outracks.Fuse.Inspector.Sections
{
	using Fusion;
	
	class SpacingSection
	{
		public static IControl Create(IElement element, IEditorFactory editors)
		{
			var margin = element["Margin"];
			var padding = element["Padding"];

			return Layout.StackFromTop(
					ThicknessEditor(margin, editors).WithLabel("Margin"),
					Spacer.Small,
					ThicknessEditor(padding, editors).WithLabel("Padding"))
				.WithInspectorPadding();
		}

		static IEditorControl ThicknessEditor(IAttribute thickness, IEditorFactory editors)
		{
			var expressions = Decompose(thickness);
			var cells =
				expressions.Select(
					(prop, edge) =>
						TextBox.Create(prop, foregroundColor: Theme.DefaultText)
							.WithPadding(new Thickness<Points>(3,0,0,0))
							.WithOverlay(Shapes.Rectangle(fill: Theme.FieldStroke.Brush).WithSize(new Size<Points>(2, 2)).Dock(edge))
							.WithOverlay(Shapes.Rectangle(Theme.FieldStroke))
							.WithBackground(Shapes.Rectangle(fill: Theme.FieldBackground))
							.WithWidth(28)
							.WithHeight(CellLayout.DefaultCellHeight)
							.SetToolTip(edge.ToString()));

			return new EditorControl(
				editors, thickness,
				Layout.StackFromLeft(
					cells.Left, Spacer.Small, 
					cells.Top, Spacer.Small, 
					cells.Right, Spacer.Small, 
					cells.Bottom, 
					Spacer.Medim, 
					editors.ExpressionButton(thickness).WithPadding(right: new Points(1))));
		}

		static Thickness<IProperty<string>> Decompose(IAttribute attribute)
		{
			return new Thickness<IProperty<string>>(
				left: Component(0, of: attribute),
				top: Component(1, of: attribute),
				right: Component(2, of: attribute),
				bottom: Component(3, of: attribute));
		}

		static IProperty<string> Component(int index, IAttribute of)
		{
			return of.StringValue.Convert(
				convert: full =>
				{
					var parts = full.Split(",");
					var boundedIndex = index % parts.Length;
					return parts[boundedIndex];
				},
				convertBack: (full, part) =>
				{
					var parts = full.Or("").Split(",");
					var boundedIndex = index % parts.Length;
					parts[boundedIndex] = part;
					return parts.Join(", ");
				});
		}

	}
}