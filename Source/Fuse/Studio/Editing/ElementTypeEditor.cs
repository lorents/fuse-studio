using System;
using Outracks.Fuse.Model;
using Outracks.Fusion;

namespace Outracks.Fuse.Live
{
	class ElementTypeEditor : IBehaviorProperty<string>
	{
		readonly ElementModel _element;
		readonly PreviewController _preview;

		public ElementTypeEditor(
			ElementModel element, 
			PreviewController preview)
		{
			_element = element;
			_preview = preview;
		}

		public IDisposable Subscribe(IObserver<string> observer)
		{
			return _element.Name.Subscribe(observer);
		}

		public string Value
		{
			get { return _element.Name.Value; }
		}

		public IObservable<bool> IsReadOnly
		{
			get { return _element.Document.IsReadOnly; }
		}

		public void Write(string value, bool save = false)
		{
			_element.Name.OnNext(value);
			try
			{
				_element.XElement.Name = value;
			}
			catch (Exception)
			{
				// what?
			}

			_preview.ElementNameChanged(_element);

			if (save)
			{
				_element.Document.Save();
				_preview.Flush();
			}
		}
	}
}