using System;
using System.Reactive.Linq;
using Outracks.Fusion;

namespace Outracks.Fuse.Live
{
	public class ElementAttributeEditor : IAttribute
	{
		readonly IProperty<string> _property;
		public ElementAttributeEditor(IProperty<string> property)
		{
			_property = property;
		}
		
		public Command Clear
		{
			get { return HasValue.Select(hasValue => Command.Create(hasValue, () => _property.Write("", true))).Switch(); }
		}

		public IObservable<bool> HasValue
		{
			get { return StringValue.Select(value => !string.IsNullOrEmpty(value)); }
		}

		public IProperty<string> StringValue
		{
			get { return _property; }
		}

		public IProperty<Points> ScrubValue
		{
			get { return Property.Constant(new Points(0)); }
		}

		public IObservable<bool> IsReadOnly
		{
			get { return _property.IsReadOnly; }
		}
	}
}