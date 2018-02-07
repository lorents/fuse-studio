using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Outracks.Fuse
{
	using Fusion;

	/// <summary>
	/// An interface for observing and potentially editing a UX element/tag. 
	/// 
	/// You should consider instances as observable query results, rather than (wrongly) assuming 
	/// there a one-to-one mapping between IElement instances and the data it's an interface to.
	/// 
	/// (If you really care about element identity, observe SimulatorId. Don't rely on equality operators of IElement)
	/// </summary>
	public interface IElement
	{
		IObservable<bool> IsEmpty { get; }
		IObservable<bool> IsReadOnly { get;  } 
		
		IElement Parent { get; }
		IElement Base { get; }

		IProperty<string> Name { get; }
		IObservable<bool> Is(string elementType);
		IObservable<bool> IsChildOf(string elementType);
		IObservable<bool> IsSiblingOf(string elementType);
		IObservable<bool> IsDescendantOf(IElement element);

		IAttribute this[string attributeName] { get; }

		IProperty<string> Content { get; }

		IObservableList<IElement> Children { get; }
		
		Command Remove();
		Command Insert(SourceFragment fragment);
		Command InsertAfter(SourceFragment fragment);
		Command InsertBefore(SourceFragment fragment);
	}

	public class ElementIsEmpty : InvalidOperationException
	{
		public override string Message
		{
			get { return "Element is empty"; }
		}
	}

	public class ElementIsReadOnly : InvalidOperationException
	{
		public override string Message
		{
			get { return "Element is read-only"; }
		}
	}

	public class ElementIsRoot : InvalidOperationException
	{
		public override string Message
		{
			get { return "Element is root"; }
		}
	}
	
}