using System;
using System.Reactive.Linq;
using Outracks.IO;

namespace Outracks.Fuse.Inspector.Editors
{
	using Fusion;

	public class Factory : IEditorFactory
	{
		readonly IObservable<object> _elementChanged;
		readonly IPopover _popover;
		public Factory(IObservable<object> elementChanged, IPopover popover)
		{
			_elementChanged = elementChanged;
			_popover = popover;
		}

		public IControl ElementList(Text name, IElement parent, SourceFragment prototype, Func<IElement, IControl> itemFactory)
		{
			return ListEditor.Create(parent, name, prototype, itemFactory);
		}

		public IControl Label(Text name, params IAttribute[] properties)
		{
			return LabelEditor.Create(name, properties);
		}


		public IControl Label(Text name, IProperty<Optional<string>> attributeData)
		{
			return LabelEditor.Create(name, attributeData.Select(e => e.HasValue), attributeData.IsReadOnly);
		}
		
		public IEditorControl Field(IAttribute attribute, Text placeholderText = default(Text), Text toolTip = default(Text), bool deferEdit = false)
		{
			return Wrap(attribute, FieldEditor.Create(this, attribute, placeholderText: placeholderText, toolTip: toolTip, deferEdit: deferEdit));
		}

		public IEditorControl Switch(IAttribute attribute)
		{
			return Wrap(attribute, Layout.StackFromLeft(
				SwitchEditor.Create(attribute)
					.CenterVertically(),
				Spacer.Medim, 
				ExpressionButton(attribute).WithPadding(right: new Points(1))
					.CenterVertically()));
		}

		public IEditorControl Color(IAttribute color)
		{
			return Wrap(color, ColorEditor.Create(color, this));
		}
		public IEditorControl Slider(IAttribute attribute, double min, double max)
		{
			return Wrap(attribute, SliderEditor.Create(attribute, min, max));
		}

		public IEditorControl FilePath(IAttribute attribute, IObservable<AbsoluteDirectoryPath> projectRoot, FileFilter[] fileFilters, Text placeholderText = default(Text), Text toolTip = default(Text))
		{
			return Wrap(attribute, FilePathEditor.Create(this, attribute, projectRoot, fileFilters, placeholderText: placeholderText, toolTip: toolTip));
		}

		public IEditorControl Dropdown<T>(IAttribute attribute, T defaultValue) where T : struct
		{
			return Wrap(attribute, DropdownEditor.Create(attribute, this, defaultValue));
		}

		IEditorControl Wrap(IAttribute property, IControl control)
		{
			return new EditorControl(this, property, control);
		}

		public IRadioButton<T> RadioButton<T>(IAttribute attribute, T defaultValue)
		{
			return new RadioButtonCellBuilder<T>(attribute, this);
		}

		public IControl ExpressionButton(IAttribute attribute)
		{
			return ExpressionEditor.CreateButton(_elementChanged, attribute, _popover);
		}
	}
}
