using System;
using System.Xml.Linq;
using Outracks.Fuse.Model;

namespace Outracks.Fuse.Live
{
	class ElementAttributeProperty : IProperty<string>
	{
		readonly ElementModel _element;
		readonly PreviewController _preview;
		readonly string _attribute;

		public ElementAttributeProperty(
			ElementModel element,
			string attribute,
			PreviewController preview)
		{
			_element = element;
			_attribute = attribute;
			_preview = preview;
		}


		public IDisposable Subscribe(IObserver<string> observer)
		{
			return _element[_attribute].Subscribe(observer);
		}

		public IObservable<bool> IsReadOnly
		{
			get { return _element.Document.IsReadOnly; }
		}

		public void Write(string value, bool save)
		{
			_element[_attribute].OnNext(value);
			_element.XElement.SetAttributeValue(KeyToName(_attribute), value == "" ? null : value);
			_preview.ElementAttributeChanged(_element, _attribute);

			if (save)
			{
				_element.Document.Save();
				_preview.Flush();
			}
		}

		static XName KeyToName(string name)
		{
			return name.StartsWith("ux:")
				? XName.Get(name.StripPrefix("ux:"), "http://schemas.fusetools.com/ux")
				: XName.Get(name);
		}
	
	}
}