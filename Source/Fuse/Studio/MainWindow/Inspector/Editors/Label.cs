using System;
using System.Reactive.Linq;

namespace Outracks.Fuse.Inspector.Editors
{
	using Fusion;

	class LabelEditor
	{
		public static IControl Create(Text text, params IAttribute[] properties)
		{
			var hasValue = Observable.Return(false);
			var isReadOnly = Observable.Return(false);
			foreach (var property in properties)
			{
				hasValue = hasValue.Or(property.HasValue);
				isReadOnly = isReadOnly.Or(property.IsReadOnly);
			}
			return Create(text, hasValue, isReadOnly);
		}

		public static IControl Create(Text text, IObservable<bool> hasValue, IObservable<bool> isReadOnly, bool isHittable = false)
		{
			return Label.Create(
					text: text, 
					font: Theme.DefaultFont, 
					textAlignment: TextAlignment.Left,
					color: isReadOnly.Select(r => !r ? Theme.DefaultText : Theme.DisabledText).Switch())
				.CenterVertically()
				.WithHeight(CellLayout.DefaultCellHeight);
		}

		public static IControl Create(Text text, IAttribute attribute)
		{
			return Label.Create(
					text: text,
					font: Theme.DefaultFont,
					textAlignment: TextAlignment.Left,
					color: attribute.IsReadOnly.Select(r => !r ? Theme.DefaultText : Theme.DisabledText).Switch())
				.CenterVertically()
				.WithHeight(CellLayout.DefaultCellHeight)
				.WhileDraggingScrub(attribute.ScrubValue)
				.SetCursor(attribute.IsReadOnly.Select(d => d ? Cursor.Normal : Cursor.ResizeHorizontally))
				.SetContextMenu(Menu.Item("Clear", attribute.Clear));
		}
	}
}