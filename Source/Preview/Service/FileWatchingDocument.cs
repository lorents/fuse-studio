using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Fuse.Preview;
using Outracks.IO;

namespace Outracks.Fuse
{
	public interface IDocument<T> : IDisposable
	{
		/// <summary>
		/// This observable emits the latest contents of the document.
		/// Upon subscription this observable replays one elements.
		/// </summary>
		IObservable<T> Contents { get; }

		/// <summary>
		/// This observable emits the latest contents read from disk, ignoring changes saved by invoking Save().
		/// Upon subscription this observable replays one elements.
		/// </summary>
		IObservable<T> ExternalChanges { get; }

		AbsoluteFilePath Path { get; }

		void Save(T contents);
	}

	public class EmptyFile : IDocument<byte[]>
	{
		static readonly AbsoluteFilePath TempFile = AbsoluteFilePath.Parse(System.IO.Path.GetTempFileName());

		public IObservable<byte[]> Contents
		{
			get { return Observable.Return(new byte[0]); }
		}

		public IObservable<byte[]> ExternalChanges
		{
			get { return Observable.Return(new byte[0]); }
		}

		public AbsoluteFilePath Path
		{
			get { return TempFile; }
		}

		public void Save(byte[] contents)
		{
			// Throw?
		}

		public void Dispose()
		{

		}
	}


	public class Repository : IDisposable
	{
		readonly List<WeakReference<FileWatchingDocument>> _openDocuments = 
			new List<WeakReference<FileWatchingDocument>>();

		readonly IFileSystem _shell;
		readonly IScheduler _scheduler;
		readonly IOutput _output;

		public IScheduler Scheduler
		{
			get { return _scheduler; }
		}

		public Repository(IFileSystem shell, IScheduler scheduler, IOutput output)
		{
			_shell = shell;
			_scheduler = scheduler;
			_output = output;
		}

		public IDocument<byte[]> OpenBinary(AbsoluteFilePath path)
		{
			var document = new FileWatchingDocument(_shell, path, _scheduler, _output);
			_openDocuments.Add(new WeakReference<FileWatchingDocument>(document));
			return document;
		}

		public void Dispose()
		{
			foreach (var weakDocument in _openDocuments)
			{
				FileWatchingDocument document;
				if (weakDocument.TryGetTarget(out document))
					document.Dispose();
			}
		}
	}

	public class FileWatchingDocument : IDocument<byte[]>
	{
		public static readonly TimeSpan PreLogRetryInterval = TimeSpan.FromMilliseconds(100);
		public static readonly TimeSpan PostLogRetryInterval = TimeSpan.FromMilliseconds(1000);
		public static readonly TimeSpan RetryErrorMessageDelay = TimeSpan.FromSeconds(3);

		readonly IDisposable _garbage;
		readonly IFileSystem _fs;
		readonly AbsoluteFilePath _path;
		readonly BehaviorSubject<byte[]> _externalChanges;
		readonly BehaviorSubject<byte[]> _contents; 
		readonly IScheduler _scheduler;
		readonly IOutput _output;

		public FileWatchingDocument(IFileSystem fs, AbsoluteFilePath path, IScheduler scheduler, IOutput output)
		{
			_fs = fs;
			_path = path;
			_scheduler = scheduler;
			_output = output;

			var initialContent = ReadAllBytesAndRetryOnError(CancellationToken.None);

			_externalChanges = new BehaviorSubject<byte[]>(initialContent);
			_contents = new BehaviorSubject<byte[]>(initialContent);
		
			_garbage = fs.Watch(path)
				.Throttle(TimeSpan.FromSeconds(1.0 / 30.0), scheduler: scheduler)
				.ObserveOn(scheduler)
				.Do(notifyTime =>
				{
					var readContent = ReadAllBytesAndRetryOnError(CancellationToken.None);
					if (readContent.SequenceEqual(_contents.Value))
						return;

					_contents.OnNext(readContent);
					_externalChanges.OnNext(readContent);
				})
				.CatchAndRetry(delay: TimeSpan.FromSeconds(1), scheduler: scheduler)
				.Subscribe();

		}

		public IScheduler Scheduler
		{
			get { return _scheduler; }
		}

		public IObservable<byte[]> Contents
		{
			get { return _contents; }
		}

		public IObservable<byte[]> ExternalChanges
		{
			get { return _externalChanges/*.ObserveOn(_scheduler)*/; }
		}

		public AbsoluteFilePath Path 
		{
			get { return _path; }
		}

		public void Save(byte[] contents)
		{
			_contents.OnNext(contents);

			try
			{
				_output.Busy("Saving " + _path.Name);
				using (var file = _fs.Open(_path, FileMode.Create, FileAccess.Write, FileShare.Read))
				{
					file.Write(contents, 0, contents.Length);
				}
				_output.Ready();
			}
			catch (Exception e)
			{
				_output.Error("Could not save " + _path.Name + ": " + e.Message);
			}
		}

		public void Dispose()
		{
			_garbage.Dispose();
		}

		private byte[] ReadAllBytesAndRetryOnError(CancellationToken token)
		{
			_output.Busy("Loading " + _path.Name);
			var startTime = _scheduler.Now;
			string lastErrorMessage = null;
			TimeSpan retryInterval = PreLogRetryInterval;
			var tokenObservable = Observable.Create<long>(observer => token.Register(() => observer.OnNext(0)));

			while (true)
			{
				token.ThrowIfCancellationRequested();
				try
				{
					using (var file = _fs.OpenRead(_path))
					{
						var bytes = file.ReadAllBytes();
						_output.Ready();
						return bytes;
					}
				}
				catch (Exception exception)
				{
					token.ThrowIfCancellationRequested();

					if (_scheduler.Now - startTime > RetryErrorMessageDelay && lastErrorMessage != exception.Message)
					{

						// After 3 seconds we only retry every second, to slow down IO hammering
						retryInterval = PostLogRetryInterval;
						try
						{
							throw new IOException(string.Format(exception.Message.TrimEnd('.') + ". Retrying in background until problem is resolved."), exception);
						}
						catch (Exception wrappedException)
						{
							_output.Error("Could not load " + _path.Name + ": " + wrappedException.Message);
						}
						lastErrorMessage = exception.Message;
					}
				}

				Thread.Sleep(retryInterval);
				// We're using an Observable.Timer here to make this work with HistoricalScehduler in test.
				//await Observable
				//	.Timer(retryInterval, _scheduler)
				//	.Merge(tokenObservable).FirstAsync();
			}
		}
	}

}