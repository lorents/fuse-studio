using System;
using System.Reactive.Concurrency;

namespace Outracks.Fuse.Live
{
	class ScheduledProperty<T> : IProperty<T>
	{
		readonly IProperty<T> _source;
		readonly IScheduler _scheduler;
		public ScheduledProperty(IProperty<T> source, IScheduler scheduler)
		{
			_source = source;
			_scheduler = scheduler;
		}

		public IDisposable Subscribe(IObserver<T> observer)
		{
			return _source.Subscribe(observer);
		}

		public IObservable<bool> IsReadOnly
		{
			get { return _source.IsReadOnly; }
		}

		public void Write(T value, bool save = false)
		{
			_scheduler.Schedule(() => _source.Write(value, save));
		}
	}
}