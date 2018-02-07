using System;
using System.Reactive.Linq;
using Outracks.Fuse.Model;

namespace Outracks.Fuse.Editing
{

	public static partial class Element
	{
		public static IObservable<bool> HasProperty(this ElementModel element, string property)
		{
			return element[property].Select(e => !string.IsNullOrEmpty(e));
		}

		public static IObservable<bool> IsSameAs(this IElement element, IElement other)
		{
			return Observable.Return(false);
			//return element.SimulatorId.CombineLatest(other.SimulatorId, (a, b) => a.Equals(b));
		}

		public static IElement As(this IElement element, string type)
		{
			return element.Is(type)
				.Select(isType => isType ? element : Empty)
				.Switch();
		}
	}
}