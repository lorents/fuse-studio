using System;
using Outracks.IO;

namespace Outracks.Fuse.Inspector
{
	using Fusion;
	
	public interface IEditorFactory
	{
		IControl ElementList(Text name, IElement parent, SourceFragment prototype, Func<IElement, IControl> itemFactory);

		IControl Label(Text name, IProperty<Optional<string>> attributeData);

		IControl Label(Text name, params IAttribute[] properties);

		IEditorControl Field(IAttribute attribute, Text placeholderText = default(Text), Text toolTip = default(Text), bool deferEdit = false);

		IEditorControl Color(IAttribute color);

		IEditorControl Switch(IAttribute attribute);

		IEditorControl Dropdown<T>(IAttribute attribute, T defaultValue) where T : struct;

		IEditorControl Slider(IAttribute attribute, double min, double max);

		IEditorControl FilePath(IAttribute attribute, IObservable<AbsoluteDirectoryPath> projectRoot, FileFilter[] fileFilters, Text placeholderText = default(Text), Text toolTip = default(Text));

		IRadioButton<T> RadioButton<T>(IAttribute attribute, T defaultValue);

		IControl ExpressionButton(IAttribute attribute);
	}

	public interface IRadioButton<in T> 
	{
		IRadioButton<T> Option(T value, Func<Brush, Brush, IControl> icon, Text tooltip);
		
		IEditorControl Control { get; }
	}

	public interface IEditorControl : IControl
	{
		IControl WithIcon(Text tooltip, IControl icon);

		IControl WithLabel(Text description);

		IControl WithLabelAbove(Text description);
	}
}
