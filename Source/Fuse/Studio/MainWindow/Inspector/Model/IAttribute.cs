
using System;
using System.Reactive.Linq;
using Outracks.Fusion;

namespace Outracks.Fuse
{
	public interface IAttribute
	{
		IObservable<bool> HasValue { get; }

		IObservable<bool> IsReadOnly { get; } 
		
		IProperty<string> StringValue { get; }
		IProperty<Points> ScrubValue { get; }

		Command Clear { get; }
	}


	public class EmptyAttribute : IAttribute
	{
		public EmptyAttribute()
		{
			HasValue = Observable.Return(false);
			IsReadOnly = Observable.Return(true);
			StringValue = Property.Constant("");
			ScrubValue = Property.Constant(new Points(0));
			Clear = Command.Disabled;
		}
		public IObservable<bool> HasValue { get; private set; }
		public IObservable<bool> IsReadOnly { get; private set; }
		public IProperty<string> StringValue { get; private set; }
		public IProperty<Points> ScrubValue { get; private set; }
		public Command Clear { get; private set; }
	}

}