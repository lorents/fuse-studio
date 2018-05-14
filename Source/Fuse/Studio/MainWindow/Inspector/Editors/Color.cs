using System;
using System.Reactive.Linq;

namespace Outracks.Fuse.Inspector.Editors
{
	using Fusion;

	static class ColorEditor
	{
		public static IControl Create(IAttribute property, IEditorFactory editors)
		{
			return Layout.Dock()
				.Right(editors.ExpressionButton(property))
				.Fill(
					Layout.Dock()
					.Left(CreateWell(property))
					.Left(Separator.Line(Theme.FieldStroke))
					.Fill(TextBox.Create(
							property.StringValue,
							foregroundColor: Theme.DefaultText)
						.WithPlaceholderText(property.HasValue)
						.WithBackground(Theme.FieldBackground))
					.WithPadding(new Thickness<Points>(1))
					.WithOverlay(Shapes.Rectangle(Theme.FieldStroke))
					.WithWidth(CellLayout.FullCellWidth)
					.WithHeight(CellLayout.DefaultCellHeight));
		}

		static IControl CreateWell(IAttribute color)
		{
			var length = CellLayout.DefaultCellHeight - 2;

			var diagonalBrush = color.IsReadOnly
				.Select(ro => ro ? Theme.FieldStroke.Brush : Color.FromRgb(0xDB6868))
				.Switch();

			var diagonal = Shapes.Line(
				Point.Create(new Points(0), new Points(length)),
				Point.Create(new Points(length), new Points(0)),
				Stroke.Create(1, diagonalBrush));

			return CreateWell(color.StringValue.AsColor(Color.White), color.HasValue)
				.WithPadding(new Thickness<Points>(1))
				.WithBackground(diagonal.HideWhen(color.HasValue))
				.WithSize(new Size<Points>(length, length))
				.Center();
		}

		static IControl CreateWell(
			IProperty<Color> value = null,
			IObservable<bool> hasValue = null,
			Stroke stroke = null)
		{
			var color = value ?? Property.Create(Color.Transparent);
			return Shapes
				.Rectangle(fill: hasValue
					.Select(hasSetValue => hasSetValue ? color.AsBrush() : Color.Transparent)
					.Switch())
				.OnMouse(released: color.IsReadOnly.Switch(
					isReadOnly =>
					isReadOnly
					? Command.Disabled
					: ColorPicker.Open(color)));
		}
	}
}
