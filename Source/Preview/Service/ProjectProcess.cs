using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using Outracks;
using Outracks.Fuse.Analytics;
using Outracks.Simulator;

namespace Fuse.Preview
{
	public class ProjectProcess : IPreviewProcess
	{
		static readonly IPlatform Platform = PlatformFactory.Create();

		static readonly Assembly EntryAssembly = typeof (Program).Assembly;
		const string MagicArgument = "start";

		public static IObservable<IPreviewProcess> SpawnAsync()
		{
			return Observable
				.Start(() => Spawn())
				.Catch((Exception e) =>
				{
					Console.WriteLine(e);
					return Observable.Never<IPreviewProcess>();
				});
		}

		public static IPreviewProcess Spawn()
		{
			return new ProjectProcess(Platform.StartProcess(EntryAssembly, MagicArgument, Guid.NewGuid().ToString()));
		}

		public static void Run(string[] args)
		{
			if (args.FirstOrDefault() != MagicArgument)
				return;

			if (args.Length < 2)
			{
				Console.Error.WriteLine("Expected second argument to be the unique identifier for the pipe.");
				return;
			}

			var systemId = SystemGuidLoader.LoadOrCreateOrEmpty();
			var sessionId = Guid.NewGuid();
			var reporter = ReportFactory.GetReporter(systemId, sessionId, "SimulatorHost");
			AppDomain.CurrentDomain.ReportUnhandledExceptions(reporter);

			var commands = Platform.CreateStream("commands");
			var messages = Platform.CreateStream("messages");

			var version = new Version();
			var outputSubject = new ReplayQueueSubject<IBinaryMessage>();
			var reifier = new Reifier(new UnoBuild(version), outputSubject);

			
			using (WriteOutput(outputSubject, messages))
			using (var writer = new BinaryWriter(commands))
			using (var reader = new BinaryReader(commands))
			{
				while (true)
				{
					try
					{
						var result = MethodCall.ReadFrom(reader).InvokeOn(reifier);

						writer.Write(false);
						writer.WriteTaggedValue(result);
					}
					catch (NotSupportedException e)
					{
						writer.Write(true);
						writer.Write(typeof(NotSupportedException).Name);
						writer.Write(e.Message);
					}
					catch (TargetInvocationException e)
					{
						writer.Write(true);
						writer.Write(typeof(TargetInvocationException).Name);
						writer.Write(e.InnerException.ToString());
					}
					catch (Exception e)
					{
						writer.Write(true);
						writer.Write(typeof(Exception).Name);
						writer.Write(e.ToString());
					}
				}
			}
		}

		static IDisposable WriteOutput(IObservable<IBinaryMessage> messages, Stream stream)
		{
			var writer = new BinaryWriter(stream);

			return Disposable.Combine(
				stream, writer,
				messages.Subscribe(message => message.WriteTo(writer)));
		}

		readonly BinaryWriter _writer;
		readonly BinaryReader _reader;
		public ProjectProcess(IProcess process)
		{
			var commands = process.OpenStream("commands");
			_writer = new BinaryWriter(commands);
			_reader = new BinaryReader(commands);

			var messages = process.OpenStream("messages");
			Messages = messages.ReadMessages("Preview Service").Publish();
		}

		public IConnectableObservable<IBinaryMessage> Messages
		{
			get; private set;
		}

		public string Build(
			string projectPath,
			string[] defines,
			bool buildLibraries,
			bool verbose,
			string outputDir = "")
		{
			return (string)IssueCommand(preview => preview.Build(projectPath, defines, buildLibraries, verbose, outputDir));
		}

		public void Refresh()
		{
			IssueCommand(preview => preview.Refresh());
		}

		public void Clean()
		{
			IssueCommand(preview => preview.Clean());
		}
		
		public bool TryUpdateAttribute(ObjectIdentifier element, string attribute, string value)
		{
			return (bool) IssueCommand(preview => preview.TryUpdateAttribute(element, attribute, value));
		}


		object IssueCommand(Expression<Action<IPreviewProcess>> command)
		{
			MethodCall.FromExpression(command).WriteTo(_writer);

			var hasError = _reader.ReadBoolean();
			if (hasError)
			{
				var error = _reader.ReadString();

				if (error == typeof(NotSupportedException).Name)
					throw new NotSupportedException(_reader.ReadString());

				if (error == typeof(TargetInvocationException).Name)
					throw new ProxyServerFailed(_reader.ReadString());

				throw new ProxyServerFailed(_reader.ReadString());
			}

			return _reader.ReadTaggedValue();
		}

		
	}
}
