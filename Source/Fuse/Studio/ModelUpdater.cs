using System;
using System.Linq;
using System.Xml.Linq;
using Outracks.Fuse.Model;
using Outracks.Fusion;

namespace Outracks.Fuse
{
	public class ModelUpdater
	{
		readonly IModelChangeListener _listener;

		public ModelUpdater(IModelChangeListener listener)
		{
			_listener = listener;
		}

		public void UpdateFrom(ElementModel element, byte[] bytes)
		{
			UpdateFrom(element, SourceFragment.FromBytes(bytes).ToXml());
		}

		public void UpdateFrom(ElementModel element, XElement newElement)
		{
			var oldElement = element.XElement;
			UpdateFrom(element, oldElement, newElement);
			element.XElement = newElement;
		}

		void UpdateFrom(ElementModel element, XElement oldElement, XElement newElement)
		{
			element.Name.OnNext(newElement.Name.LocalName);
			UpdateAttributes(element, oldElement, newElement);
			UpdateValue(element, oldElement, newElement);
			UpdateChildren(element, newElement);
		}


		void UpdateAttributes(ElementModel element, XElement oldElement, XElement newElement)
		{
			var prev = oldElement.Attributes().ToArray();
			var next = newElement.Attributes().ToArray();

			if (prev.SequenceEqual(next))
				return;

			var prevSet = oldElement.Attributes().ToDictionary(k => k.Name, k => k);
			var nextSet = newElement.Attributes().ToDictionary(k => k.Name, k => k);

			foreach (var prevAttr in prev)
			{
				var name = prevAttr.Name;
				XAttribute newAttr;
				if (!nextSet.TryGetValue(name, out newAttr))
					element[NameToKey(name)].OnNext("");
				else if (newAttr.Value != prevAttr.Value)
					element[NameToKey(name)].OnNext(newAttr.Value);

			}

			foreach (var newAttr in next)
			{
				var name = newAttr.Name;
				if (!prevSet.ContainsKey(name))
					element[NameToKey(name)].OnNext(newAttr.Value);
			}
		}

		static string NameToKey(XName name)
		{
			return (name.Namespace == XNamespace.Get("http://schemas.fusetools.com/ux") ? "ux:" : "") + name.LocalName;
		}

		void UpdateValue(ElementModel element, XElement oldElement, XElement newElement)
		{
			if (newElement.HasElements || string.IsNullOrWhiteSpace(newElement.Value))
			{
				if (oldElement.HasElements || oldElement.Value == newElement.Value)
					return;

				element.Content.OnNext("");
			}
			else
			{
				if (!oldElement.HasElements && oldElement.Value == newElement.Value)
					return;

				element.Content.OnNext(newElement.Value);
			}
		}

		void UpdateChildren(ElementModel element, XElement newElement)
		{
			var oldChildren = element.Children.Value;

			var newChildren = newElement.Elements().ToList();
			if (oldChildren.Count == 0 && newChildren.Count == 0)
				return;

			var builder = new ElementModel[newChildren.Count];

			// We'll be using newChildren as a worklist, so let's take a note of the position of all the new children
			var newChildrenLocation = newElement.Elements()
				.Select((e, i) => Tuple.Create(e, i))
				.ToDictionary(t => t.Item1, t => t.Item2);


			var needsReify = newChildren.Count != oldChildren.Count;

			// Incrementally (eagerly) find a new child with same element type
			// Performance should be "okay" since we're scanning both lists from the start and removing children from the worklist as we find them
			foreach (var oldChildImpl in oldChildren)
			{
				foreach (var newChild in newChildren)
				{
					// If one of the new XElement children we got has the same name as one of our IElements
					if (oldChildImpl.XElement.Name.LocalName == newChild.Name.LocalName)
					{
						// Update the old IElement
						builder[newChildrenLocation[newChild]] = oldChildImpl;
						UpdateFrom(oldChildImpl, newChild);

						// Remove the new child from the worklist and stop iterating over it
						newChildren.Remove(newChild);
						// Breaking the loop here is important both for correctness and to avoid iterating over a changed collection
						break;
					}

					// elements will be removed
					needsReify = true;
				}
			}

			needsReify |= newChildren.Any();

			// So, we've reused all the elements we can reuse, but we still might have some left in our newChildren worklist
			foreach (var newChild in newChildren)
			{
				// Fortunately we know where they go
				var index = newChildrenLocation[newChild];

				// Since we're iterating through them from start to end the index should not be out of bounds
				var childElement = CreateChildElement(element, SourceFragment.FromString("<" + newChild.Name.LocalName + "/>"));

				UpdateFrom(childElement, newChild);

				builder[index] = childElement;
			}

			if (needsReify)
			{
				element.Children.OnClear();
				foreach (var child in builder)
					element.Children.OnAdd(child);
				_listener.ElementChildrenChanged(element);
			}
		}
		public ElementModel CreateChildElement(ElementModel parent, SourceFragment sourceFragment)
		{
			var elm = new InnerElement(parent: parent);
			UpdateFrom(elm, sourceFragment.ToXml());
			return elm;
		}
		public void UpdateXml(ElementModel element, Action<XElement> action)
		{
			action(element.XElement);
		}

		public void UpdateChildren(ElementModel element, Action<ListBehaviorSubject<ElementModel>> edit)
		{
			edit(element.Children);
			_listener.ElementChildrenChanged(element);
		}

		public void Flush()
		{
			_listener.Flush();
		}
	}
}