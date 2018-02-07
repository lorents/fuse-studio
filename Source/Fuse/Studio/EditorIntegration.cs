using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Fuse.Preview;
using ICSharpCode.NRefactory.Editor;
using ICSharpCode.NRefactory.Xml;
using Outracks.Fuse.Model;
using Outracks.Fuse.Protocol;
using Outracks.Fuse.Protocol.Messages;
using Outracks.Fuse.Protocol.Preview;
using Outracks.Fusion;
using Outracks.IO;
using Outracks.Simulator;
using Outracks.Simulator.Protocol;

namespace Outracks.Fuse
{
	// TODO: Merge with EditorIntegration
	public static class FocusEditorCommand
	{
		[DllImport("user32.dll")]
		public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

		public static Command Create(ProjectModel project, IMessagingService daemon)
		{
			return Command.Disabled;

			//context.CurrentSelection
			//.SourceReference
			//.CombineLatest(project.FilePath, (maybeSrc, projectFile) =>
			//	maybeSrc.SelectMany(src =>
			//		src.Location.Select(location =>
			//			new FocusEditorRequest
			//			{
			//				File = src.File,
			//				Column = location.Character,
			//				Line = location.Line,
			//				Project = projectFile.NativePath
			//			})))
			//.Switch(request =>
			//	Command.Create(
			//		isEnabled: request.HasValue,
			//		action: () => SendRequest(daemon, request.Value)));
		}

		public static void SendRequest(IMessagingService daemon, FocusEditorRequest request)
		{
			Task.Run(
				async () =>
				{
					try
					{
						var response = await daemon.Request(request);
						if (response.FocusHwnd.HasValue && Environment.OSVersion.Platform == PlatformID.Win32NT)
							SwitchToThisWindow(new IntPtr(response.FocusHwnd.Value), true);
					}
					catch (RequestFailed)
					{
						// Just ignore for now..
					}
				});
		}
	}

	public class EditorIntegration : IDisposable
	{
		readonly List<ProjectModel> _openProjects = new List<ProjectModel>();
		readonly IMessagingService _daemon;
		readonly IScheduler _scheduler;
		readonly IDisposable _disposable;

		public EditorIntegration(IMessagingService daemon, IScheduler scheduler)
		{
			_daemon = daemon;
			_scheduler = scheduler;
			_disposable = Start();
		}

		IDisposable Start()
		{
			return _daemon.ProvideOptionally<FocusDesignerRequest, FocusDesignerResponse>(x =>
			{
				var projectPath = _openProjects.FirstOrDefault(openProject => 
					string.Equals(openProject.Path.NativePath, x.File, StringComparison.OrdinalIgnoreCase));

				if (projectPath != null)
				{
					Application.OpenDocument(projectPath.Path, true);
					return new FocusDesignerResponse();
				}
				return new Optional<FocusDesignerResponse>();
			});
		}

		public IDisposable Register(ProjectModel project, IPreview preview)
		{
			_openProjects.Add(project);

			return Disposable.Combine(
				PushEventsToDaemon(project, preview),
				UpdateProjectContext(project),
				Disposable.Create(() =>
				{
					_openProjects.Remove(project); 
					_daemon.Broadcast(new ProjectClosed { ProjectId = ProjectIdComputer.IdFor(project.Path) });
				}));
		}

		IDisposable UpdateProjectContext(ProjectModel project)
		{
			var includedFiles = project.Documents
				.Select(doc => doc.File.Path)
				.ToObservableImmutableList();

			return _daemon
				.BroadcastedEvents<SelectionChanged>(false)

				// BEGIN HACK: bug in atom plugin is giving invalid selection when reloading sometimes
				.Where(s => s.Text.Length > 0 && s.CaretPosition.Character != 1)
				.Throttle(TimeSpan.FromMilliseconds(50), _scheduler)
				// END HACK

				.WithLatestFromBuffered(includedFiles, (args, files) =>
					files.Contains(AbsoluteFilePath.Parse(args.Path))
						? Optional.Some(new ObjectIdentifier(args.Path, TryGetElementIndex(args.Text, args.CaretPosition)))
						: Optional.None())
				.NotNone()

				.Select(project.FindElement)
				.Subscribe(project.Scope.Value.CurrentSelection.OnNext);
		}

		internal static int TryGetElementIndex(string xml, Protocol.Messages.TextPosition caretPosition)
		{
			return TryGetElementIndex(xml, new TextPosition(new LineNumber(caretPosition.Line), new CharacterNumber(caretPosition.Character)));
		}

		static int TryGetElementIndex(string xml, TextPosition caretPosition)
		{
			try
			{
				var normalizedXml = xml.NormalizeLineEndings();
				var offset = caretPosition.ToOffset(normalizedXml);
				var parser = new AXmlParser();
				var tagsoup = parser.ParseTagSoup(new StringTextSource(normalizedXml));
				var elements = RemoveJavaScript(tagsoup.OfType<AXmlTag>());

				return elements
					.Where(e => !e.IsEndTag && !e.IsComment && e.ClosingBracket != "")
					.IndexOfFirst(obj => obj.StartOffset <= offset && offset < obj.EndOffset);
			}
			catch (Exception)
			{
				return -1;
			}
		}

		static IEnumerable<AXmlTag> RemoveJavaScript(IEnumerable<AXmlTag> elements)
		{
			var skip = false;

			foreach (var element in elements)
			{
				if (element.Name == "JavaScript" && element.ClosingBracket == ">")
				{
					skip = !skip;
					yield return element;
				}
				else if (!skip)
				{
					yield return element;
				}
			}
		}

		public IDisposable PushEventsToDaemon(ProjectModel project, IPreview preview)
		{
			var projectPath = project.Path;
			var projectId = ProjectIdComputer.IdFor(project.Path);
			var target = BuildTarget.DotNetDll;
			var messages = preview.Messages.Replay(TimeSpan.FromSeconds(2)).RefCount();
			return PushEventsToDaemon(messages, _daemon, projectPath, projectId, target);
		}

		public static IDisposable PushEventsToDaemon(
			IObservable<IBinaryMessage> messages,
			IMessagingService daemon, 
			AbsoluteFilePath projectPath, 
			Guid projectId,
			BuildTarget target)
		{
			// TODO: do something with reset from the daemon
			//var reset = daemon.BroadcastedEvents<ResetPreviewEvent>(false);

			var daemonEvents = Observable.Merge<IEventData>(
				messages.TryParse(Started.MessageType, Started.ReadDataFrom).SelectSome(started => 
					BinaryMessage.TryParse(started.Command, BuildProject.MessageType, BuildProject.ReadDataFrom).Select(build => 
						new BuildStartedData
						{
							BuildId = build.Id,
							ProjectPath = projectPath.NativePath,
							BuildType = BuildTypeData.FullCompile,
							ProjectId = projectId,
							Target = target,
						}).Or(
					BinaryMessage.TryParse(started.Command, GenerateBytecode.MessageType, GenerateBytecode.ReadDataFrom).Select(reify => 
						new BuildStartedData
						{
							BuildId = reify.Id,
							ProjectPath = projectPath.NativePath,
							BuildType = BuildTypeData.LoadMarkup,
							ProjectId = projectId,
							Target = target,
						}))),

				messages.TryParse(Ended.MessageType, Ended.ReadDataFrom).SelectSome(ended => 
					BinaryMessage.TryParse(ended.Command, BuildProject.MessageType, BuildProject.ReadDataFrom).Select(build => 
						new BuildEndedData
						{
							BuildId = build.Id,
							Status = ended.Success ? BuildStatus.Success : BuildStatus.Error,
						}).Or(
					BinaryMessage.TryParse(ended.Command, GenerateBytecode.MessageType, GenerateBytecode.ReadDataFrom).Select(reify =>
						new BuildEndedData
						{
							BuildId = reify.Id,
							Status = ended.Success ? BuildStatus.Success : BuildStatus.Error,
						}))),

				messages
					.TryParse(BuildLogged.MessageType, BuildLogged.ReadDataFrom)
					.Select(e => new BuildLoggedData
					{
						BuildId = e.BuildId,
						Message = e.Text,
					}),

				messages
					.TryParse(BuildIssueDetected.MessageType, BuildIssueDetected.ReadDataFrom)
					.Select(e => new BuildIssueDetectedData
					{
						BuildId = e.BuildId,
						Path = e.Source.HasValue ? e.Source.Value.File : "",
						IssueType = ToPluginBuildEventType(e.Severity),
						Message = e.Message.Replace("\r", "\0"),
						ErrorCode = e.Code,
						StartPosition = TryGetStartPosition(e.Source).OrDefault(),
						EndPosition = TryGetEndPosition(e.Source).OrDefault(),
					}),

				messages
					.TryParse(RegisterName.MessageType, RegisterName.ReadDataFrom)
					.Select(r => new RegisterClientEvent
					{
						DeviceId = r.DeviceId,
						DeviceName = r.DeviceName,
						ProjectId = projectId.ToString()
					}),

				messages
					.TryParse(DebugLog.MessageType, DebugLog.ReadDataFrom)
					.Select(l => new LogEvent
					{
						DeviceId = l.DeviceId,
						DeviceName = l.DeviceName,
						ProjectId = projectId.ToString(),
						ConsoleType = ConsoleType.DebugLog,
						Message = l.Message,
						Timestamp = DateTime.Now
					}),

				messages
					.TryParse(UnhandledException.MessageType, UnhandledException.ReadDataFrom)
					.Select(e => new ExceptionEvent
					{
						DeviceId = e.DeviceId,
						DeviceName = e.DeviceName,
						ProjectId = projectId.ToString(),
						Type = e.Type,
						Message = e.Message,
						StackTrace = e.StackTrace,
						Timestamp = DateTime.Now
					}));

			return daemonEvents.Subscribe(daemon.Broadcast);
		}

		public static Protocol.BuildIssueTypeData ToPluginBuildEventType(BuildIssueType type)
		{
			switch (type)
			{
				case BuildIssueType.Error:
					return Protocol.BuildIssueTypeData.Error;
				case BuildIssueType.FatalError:
					return Protocol.BuildIssueTypeData.FatalError;
				case BuildIssueType.Message:
					return Protocol.BuildIssueTypeData.Message;
				case BuildIssueType.Warning:
					return Protocol.BuildIssueTypeData.Warning;
				default:
					return Protocol.BuildIssueTypeData.Unknown;
			}

		}

		static Optional<Protocol.Messages.TextPosition> TryGetStartPosition(Optional<SourceReference> source)
		{
			return source
				.SelectMany(s => s.Location)
				.Select(s => new Protocol.Messages.TextPosition
				{
					Line = s.Line,
					Character = s.Character,
				});
		}

		static Optional<Protocol.Messages.TextPosition> TryGetEndPosition(Optional<SourceReference> source)
		{
			return Optional.None();
		}

		public void Dispose()
		{
			_disposable.Dispose();
		}
	}
}