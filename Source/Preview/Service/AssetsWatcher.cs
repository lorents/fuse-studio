using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Outracks;
using Outracks.Fusion;
using Outracks.IO;
using Outracks.Simulator.Bytecode;
using Uno.Build.JavaScript;
using Uno.Logging;

namespace Fuse.Preview
{
	class AssetsWatcher : IDisposable
	{
		readonly IFileSystem _fileSystem;
		readonly IScheduler _scheduler;
		readonly IOutput _output;
		readonly ReifyerLogAdapter _logAdapter;
		readonly FileSender<ProjectDependency> _dependencyFileSender;
		readonly FileSender<AbsoluteFilePath> _bundleFileSender;
		readonly Lazy<FuseJS> _fuseJs;

		public AssetsWatcher(IFileSystem fileSystem, AbsoluteDirectoryPath projectRootDirectory, IScheduler scheduler, IOutput output)
		{
			_fileSystem = fileSystem;
			_scheduler = scheduler;
			_output = output;
			var progress = new StringBuilderProgress();
			var errorList = new StringProgressErrorListAdapter(progress);
			var textWriter = new StringProgressTextWriterAdapter(progress);
			var log = new Log(errorList, textWriter);

			_logAdapter = new ReifyerLogAdapter(output, progress);

			_dependencyFileSender = FileSourceSender.Create(fileSystem);
			_bundleFileSender = BundleFileSender.Create(fileSystem, Observable.Return(projectRootDirectory));
			
			_fuseJs = new Lazy<FuseJS>(() => new FuseJS(log));
		}

		public IObservable<CoalesceEntry> UpdateChangedDependencies(IObservable<IImmutableSet<ProjectDependency>> dependencies)
		{
			return WatchSet(
				dependencies,
				onItemAdded: projDep =>
				{
					var path = AbsoluteFilePath.Parse(projDep.Path);
					return Watch(path)
						.Select(data => _dependencyFileSender.CreateMessages(data.WithMetadata(projDep)))
						.Switch();
				});
		}

		public IObservable<CoalesceEntry> UpdateChangedBundleFiles(IObservableList<AbsoluteFilePath> bundleFiles)
		{
			return WatchSet(
				bundleFiles.ToObservableImmutableList(),
				onItemAdded: bundleFile =>
				{
					return Watch(bundleFile)
						.Select(d => _bundleFileSender.CreateMessages(d))
						.Switch();
				});
		}

		public IObservable<CoalesceEntry> UpdateChangedFuseJsFiles(IObservableList<AbsoluteFilePath> fuseJsFiles)
		{
			return WatchSet(
				fuseJsFiles.ToObservableImmutableList(),
				onItemAdded: bundleFile =>
				{
					return Watch(bundleFile)
						.Select(TranspileJs)
						.NotNone()
						.Select(d => _bundleFileSender.CreateMessages(d))
						.Switch();
				});
		}

		Optional<FileDataWithMetadata<AbsoluteFilePath>> TranspileJs(FileDataWithMetadata<AbsoluteFilePath> jsFile)
		{
			string output;
			if (_fuseJs.Value.TryTranspile(jsFile.Metadata.NativePath, Encoding.UTF8.GetString(jsFile.Data), out output))
			{
				// Bundle transpiled code with the original source file metadata
				return FileDataWithMetadata.Create(jsFile.Metadata, Encoding.UTF8.GetBytes(output));
			}
			else
			{
				_logAdapter.Error(jsFile.Metadata);

				// Don't propagate result
				return Optional.None();
			}
		}

		IObservable<TOut> WatchSet<T, TOut>(IObservable<IEnumerable<T>> sets, Func<T, IObservable<TOut>> onItemAdded)
		{
			return sets
				.CachePerElement(
					data => data,
					(data) =>
					{
						var disposable = new BehaviorSubject<Optional<IDisposable>>(Optional.None());
						var proxy = new Subject<TOut>();
						var changes = Observable.Create<TOut>(
							observer =>
							{
								var dis = proxy.Subscribe(observer);

								if(!disposable.Value.HasValue)
									disposable.OnNext(Optional.Some(onItemAdded(data).Subscribe(proxy)));

								return dis;
							});

						return new
						{
							changes,
							dispose = disposable
						};
					}, v => v.dispose.Value.Do(d => d.Dispose()))
				.Select(p => p.Select(v => v.changes).Merge())
				.Switch();
		}

		IObservable<FileDataWithMetadata<AbsoluteFilePath>> Watch(AbsoluteFilePath path)
		{
			return _fileSystem
				.Watch(path)
				.CatchAndRetry(delay: TimeSpan.FromSeconds(1), scheduler: _scheduler)
				.Throttle(TimeSpan.FromSeconds(1.0 / 30.0), _scheduler)
				.StartWith(Unit.Default)
				.Select(_ => path)
				.DiffFileContent(_fileSystem)
				.CatchAndRetry(TimeSpan.FromSeconds(20),
					e =>
					{
						_output.Write("Failed to load '" + path.NativePath + "': " + (e.InnerException != null ? e.InnerException.Message : e.Message));
					});
		}

		public void Dispose()
		{
			if (_fuseJs.IsValueCreated)
				_fuseJs.Value.Dispose();
		}
	}
}