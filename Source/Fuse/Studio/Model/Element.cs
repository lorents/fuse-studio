using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Xml.Linq;
using Outracks.Fusion;
using Outracks.Simulator;

namespace Outracks.Fuse.Model
{
	public interface IDocumentModel
	{
		IDocument<byte[]> File { get; }
		BehaviorSubject<bool> IsReadOnly { get; }
		ElementModel Root { get; }
	}

	public abstract class ElementModel
	{
		public ObjectIdentifier Id { get; set; }

		public XElement XElement = new XElement("Unknown");

		public readonly ListBehaviorSubject<object> Tags = new ListBehaviorSubject<object>(); 

		public readonly BehaviorSubject<string> Name = new BehaviorSubject<string>("");
		public readonly ListBehaviorSubject<ElementModel> Children = new ListBehaviorSubject<ElementModel>();
		public readonly BehaviorSubject<string> Content = new BehaviorSubject<string>("");
		public readonly ConcurrentDictionary<string, BehaviorSubject<string>> Attributes = new ConcurrentDictionary<string, BehaviorSubject<string>>(new Dictionary<string, BehaviorSubject<string>>());

		public BehaviorSubject<string> this[string attributeName]
		{
			get { return Attributes.GetOrAdd(attributeName, _ => new BehaviorSubject<string>("")); }
		}

		public abstract ElementModel Parent { get; }

		public abstract bool IsUnknown { get; }

		public abstract IDocumentModel Document { get; }

		public IObservable<object> Changed { get; private set; }

		public IEnumerable<ElementModel> GetSubtree()
		{
			return new[] { this }
				.Concat(Children.Value.SelectMany(child => child.GetSubtree()));
		}

		public IObservableList<ElementModel> ObserveSubtree()
		{
			return ObservableList.Return(new[] { this })
				.Concat(Children.SelectMany(child => child.ObserveSubtree()));
		}

		protected ElementModel()
		{
			Changed = Children.Select(child => child.Changed).Switch().Publish().RefCount();
			Id = ObjectIdentifier.None;
		}
	}


	public class InnerElement : ElementModel
	{
		readonly ElementModel _parent;

		public InnerElement(ElementModel parent)
		{
			_parent = parent;
		}

		public override bool IsUnknown
		{
			get { return false; }
		}

		public override ElementModel Parent
		{
			get { return _parent; }
		}

		public override IDocumentModel Document
		{
			get { return _parent.Document; }
		}
	}

	public class RootElement : ElementModel
	{
		readonly IDocumentModel _document;

		public RootElement(IDocumentModel document)
		{
			_document = document;
		}
		
		public override bool IsUnknown
		{
			get { return false; }
		}

		public override ElementModel Parent
		{
			get { return new UnknownElement(); }
		}

		public override IDocumentModel Document
		{
			get { return _document; }
		}
	}
	
	public class UnknownElement : ElementModel
	{
		public override bool IsUnknown
		{
			get { return true; }
		}

		public override ElementModel Parent
		{
			get { return new UnknownElement(); }
		}

		public override IDocumentModel Document
		{
			get { return new UnknownDocument(); }
		}
	}
}