using System;
using System.Reactive.Linq;
using Outracks.Fuse.Designer;

namespace Outracks.Fuse.Inspector.Editors
{
	using Fusion;
	using System.Linq;

	public class DropdownEditor
	{
		public static IControl Create<T>(IAttribute attribute, IEditorFactory editors, T defaultValue)
			where T : struct
		{
			var values = Enum.GetValues(typeof (T));
			var placeholderText = attribute.StringValue.Select(v => v.ToString()).AsText();
			var stroke = Theme.FieldStroke;
			var arrowBrush = attribute.IsReadOnly.Select(ro => ro ? Theme.FieldStroke.Brush : Theme.Active).Switch();
			
			return Layout.Dock()
				.Right(editors.ExpressionButton(attribute))
				.Fill(DropDown.Create(
						attribute.StringValue, 
						Observable.Return(values.OfType<object>().Select(v => v.ToString())), 
						nativeLook: false)
					.WithOverlay(
						Layout.Dock()
							.Right(Arrow
								.WithoutShaft(RectangleEdge.Bottom, SymbolSize.Small, arrowBrush)
								.Center().WithWidth(21)
								.WithBackground(Theme.PanelBackground))
							.Right(Separator.Line(stroke))
							.Fill(TextBox.Create(
									attribute.StringValue,
									foregroundColor: Theme.DefaultText)
								.WithPlaceholderText(attribute.HasValue, placeholderText)
								.WithBackground(Theme.FieldBackground))
							.WithPadding(new Thickness<Points>(1))
							.WithOverlay(Shapes.Rectangle(stroke: stroke)))
						.WithHeight(CellLayout.DefaultCellHeight)
						.WithWidth(CellLayout.FullCellWidth));
		}
	}
}