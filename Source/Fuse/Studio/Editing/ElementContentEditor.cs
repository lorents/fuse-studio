using System;
using System.Reactive.Linq;
using Outracks.Fuse.Model;
using Outracks.Fusion;

namespace Outracks.Fuse.Live
{
	class ElementContentEditor : IBehaviorProperty<string>
	{
		readonly ElementModel _element;
		readonly PreviewController _preview;

		public ElementContentEditor(
			ElementModel element, 
			PreviewController preview)
		{
			_element = element;
			_preview = preview;
		}

		public IDisposable Subscribe(IObserver<string> observer)
		{
			return _element.Content.Subscribe(observer);
		}

		public string Value
		{
			get { return _element.Content.Value; }
		}

		public IObservable<bool> IsReadOnly
		{
			get { return _element.Children.Count().Select(c => c > 0).Or(_element.Document.IsReadOnly); }
		}

		public void Write(string value, bool save = false)
		{
			_element.Content.OnNext(value);
			if (!string.IsNullOrEmpty(value))
				_element.XElement.Value = value;
			else
				_element.XElement.RemoveNodes();

			_preview.ElementChildrenChanged(_element);

			if (save)
			{
				_element.Document.Save();
				_preview.Flush();
			}
		}
	}
}