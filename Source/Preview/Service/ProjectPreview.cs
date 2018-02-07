using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Outracks;
using Outracks.Fuse;
using Outracks.Fusion;
using Outracks.IO;
using Outracks.Simulator;
using Outracks.Simulator.Bytecode;
using Outracks.Simulator.Protocol;
using Outracks.Simulator.Runtime;

namespace Fuse.Preview
{
	public class ProjectPreview : IPreview
	{
		readonly BuildOutputDirGenerator _buildOutputDirGenerator;
		readonly AssetsWatcher _assetsWatcher;
		readonly ProxyServer _proxy;
		readonly IOutput _output;
		readonly IPreviewProcess _preview;
		readonly CoalesceEntryCache _coalesceEntryCache;
		readonly ProjectWatcher _project;

		readonly AndroidPortReverser _portReverser = new AndroidPortReverser();

		bool _isDisposed;
		readonly IDisposable _dispose;

		public ProjectPreview(
			AbsoluteFilePath projectPath,
			IFileSystem shell,
			BuildOutputDirGenerator buildOutputDirGenerator, 
			ProxyServer proxy,
			IOutput output)
		{
			_buildOutputDirGenerator = buildOutputDirGenerator;
			_proxy = proxy;
			_output = output;

			var scheduler = Scheduler.Default;
			var repository = new Repository(shell, scheduler, output);
			var projectDir = projectPath.ContainingDirectory;
			var projectFile = repository.OpenBinary(projectPath);

			_project = ProjectWatcher.Create(projectFile, scheduler);
			_assetsWatcher = new AssetsWatcher(shell, projectDir, scheduler, output);
			
			_preview = ProjectProcess.Spawn();
			var simulatorMessages = _preview.Messages.RefCount();

			var bytecodeGenerated = simulatorMessages.TryParse(BytecodeGenerated.MessageType, BytecodeGenerated.ReadDataFrom);
			var bytecodeUpdated = simulatorMessages.TryParse(BytecodeUpdated.MessageType, BytecodeUpdated.ReadDataFrom);

			var bytecode = bytecodeGenerated.Select(msg => msg.Bytecode);

			_coalesceEntryCache = new CoalesceEntryCache();
			var assets = _project.BundleFiles.Concat(_project.FuseJsFiles)
				.ToObservableImmutableList()
				.StartWith(System.Collections.Immutable.ImmutableList<AbsoluteFilePath>.Empty);

			var reifyMessages = ReifyProject(bytecode, bytecodeUpdated, _coalesceEntryCache, assets);

			var dependencyMessages = _assetsWatcher.UpdateChangedDependencies(bytecode.Select(bc => bc.Dependencies.ToImmutableHashSet()));
			var bundleFileMessages = _assetsWatcher.UpdateChangedBundleFiles(_project.BundleFiles);
			var fuseJsFileMessages = _assetsWatcher.UpdateChangedFuseJsFiles(_project.FuseJsFiles);

			_dispose = Observable.Merge(
					bundleFileMessages,
					dependencyMessages,
					fuseJsFileMessages,
					reifyMessages)
				.Subscribe(e => _coalesceEntryCache.Add(e));
			
			var incommingMessages = new Subject<IBinaryMessage>();

			var clientAdded = new Subject<string>();
			var clientRemoved = new Subject<string>();

			var socketServer = SocketServer.Start(
				port: 0,
				clientRun: (clientStream, endPoint) =>
				{
					bool isDisconnected = false;

					var writeMessages = _coalesceEntryCache
						.ReplayFrom(-1)
						//.ObserveOn(new EventLoopScheduler())
						.Subscribe(cacheEntry =>
						{
							if (isDisconnected)
								return;

							try
							{
								using (var memoryStream = new MemoryStream())
								{
									using (var memoryStreamWriter = new BinaryWriter(memoryStream))
									{
										// ReSharper disable once AccessToDisposedClosure
										cacheEntry.Entry.BlobData.Do(message => message.WriteTo(memoryStreamWriter));
										clientStream.Write(memoryStream.ToArray(), 0, (int)memoryStream.Length);
									}
								}
							}
							catch (Exception)
							{
								isDisconnected = true;
							}
						});
					
					var clientInfo = Optional.None<RegisterName>();

					try
					{
						using (var binaryStream = new BinaryReader(clientStream))
						{
							while (true)
							{
								var msg = BinaryMessage.ReadFrom(binaryStream);

								if (!clientInfo.HasValue)
								{
									clientInfo = BinaryMessage.TryParse(msg, RegisterName.MessageType, RegisterName.ReadDataFrom);
									
									if (clientInfo.HasValue)
										clientAdded.OnNext(clientInfo.Value.DeviceId);
								}

								incommingMessages.OnNext(msg);
							}
						}
					}
					finally
					{
						if (clientInfo.HasValue)
							clientRemoved.OnNext(clientInfo.Value.DeviceId);

						writeMessages.Dispose();
					}

				});

			Port = socketServer.LocalEndPoint.Port;
			
			ClientAdded = clientAdded;
			ClientRemoved = clientRemoved;

			Messages = Observable.Merge(incommingMessages, simulatorMessages);
			
			AccessCode = CodeGenerator.CreateRandomCode(5);
		}


		public string Build(BuildProject args)
		{
			var buildDir = _project.BuildOutputDirectory.FirstAsync().Wait();
			var output = _buildOutputDirGenerator.Acquire(buildDir / new DirectoryName("Local") / new DirectoryName("Designer"));
			args.OutputDir = output.NativePath;
			args.Id = Guid.NewGuid();

			var assembly = _preview.Build(args.ProjectPath, args.Defines.ToArray(), args.BuildLibraries, args.Verbose, output.NativePath);

			_buildOutputDirGenerator.Release(output);

			_proxy.AddProject(Port, AccessCode.ToString(), args.ProjectPath, args.Defines.ToArray());

			return assembly;
		}

		public IDisposable LockBuild(string outputDir)
		{
			return _buildOutputDirGenerator.Lock(AbsoluteDirectoryPath.Parse(outputDir).ContainingDirectory);
		}

		IObservable<CoalesceEntry> ReifyProject(
			IObservable<ProjectBytecode> bytecode, 
			IObservable<BytecodeUpdated> bytecodeUpdated, 
			CoalesceEntryCache cache, 
			IObservable<IEnumerable<AbsoluteFilePath>> assets)
		{
			int idx = 0;
			var bytecodeUpdates = bytecodeUpdated.Select(m => m.ToCoalesceEntry(BytecodeUpdated.MessageType + (++idx)))
				.Publish()
				.RefCount();

			var clearOldUpdates = bytecodeUpdates
				.Buffer(bytecode)
				.Select(
					oldUpdates =>
					{
						var cachedUpdates = new List<CoalesceEntry>();
						foreach (var oldUpdate in oldUpdates)
						{
							cachedUpdates.Add(new CoalesceEntry()
							{
								BlobData = Optional.None(),
								CoalesceKey = oldUpdate.CoalesceKey
							});
						}
						return cachedUpdates;
					})
					.SelectMany(t => t);

			var reify = bytecode.WithLatestFromBuffered(assets, (bc, ass) =>
			{
				var waitForDependencies = Task.WaitAll(new [] 
				{
					bc.Dependencies.Select(d => cache.HasEntry(d.ToString()))
						.ToObservableEnumerable()
						.FirstAsync()
						.ToTask(),
					ass.Select(d => cache.HasEntry(d.ToString()))
						.ToObservableEnumerable()
						.FirstAsync()
						.ToTask()
				}, TimeSpan.FromSeconds(60));

				if (waitForDependencies == false)
				{
					throw new TimeoutException("Failed to load all assets dependencies.");
				}

				try
				{
					return new BytecodeGenerated(
							new ProjectBytecode(
								reify: new Lambda(
									Signature.Action(Variable.This),
									Enumerable.Empty<BindVariable>(),
									new[]
									{
										ExpressionConverter.BytecodeFromSimpleLambda(() => ObjectTagRegistry.Clear()),

										new CallLambda(bc.Reify, new ReadVariable(Variable.This)),
									}),
								metadata: bc.Metadata,
								dependencies: bc.Dependencies))
						.ToCoalesceEntry(BytecodeGenerated.MessageType);
				}
				catch (Exception)
				{
					return new BytecodeUpdated(
							new Lambda(
								Signature.Action(),
								Enumerable.Empty<BindVariable>(),
								Enumerable.Empty<Statement>()))
						.ToCoalesceEntry("invalid-byte-code");
				}
			})
			.CatchAndRetry(TimeSpan.FromSeconds(15),
				e =>
				{
					_output.Write("Failed to refresh because: " + e.Message);
				});

			return Observable.Merge(clearOldUpdates, reify, bytecodeUpdates);
		}

		public IObservable<CoalesceEntry> GetPreviewCache()
		{
			return _coalesceEntryCache.ReplayFrom(-1).Select(c => c.Entry);
		}

		public void Dispose()
		{
			if (_isDisposed)
				return;

			_dispose.Dispose();
			_assetsWatcher.Dispose();
			_proxy.RemoveProject(Port);

			_isDisposed = true;
		}

		public Code AccessCode { get; private set; }

		public void EnableUsbMode()
		{
			_portReverser.ReversePortOrLogErrors(ReportFactory.FallbackReport, Port, Port);
			_proxy.UpdateReversedPorts(true);
		}

		public int Port { get; private set; }

		public IObservable<string> ClientRemoved { get; set; }
		public IObservable<string> ClientAdded { get; set; }

		public IObservable<IBinaryMessage> Messages { get; private set; }
	

		public void Refresh()
		{
			_preview.Refresh();
		}

		public void Clean()
		{
			_preview.Clean();
		}

		public bool TryUpdateAttribute(ObjectIdentifier element, string attribute, string value)
		{
			return _preview.TryUpdateAttribute(element, attribute, value);
		}


	}
}