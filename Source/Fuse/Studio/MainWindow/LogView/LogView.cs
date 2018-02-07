using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Fuse.Preview;
using Outracks.Fuse.Model;
using Outracks.Fuse.Protocol;

namespace Outracks.Fuse.Designer
{
	using Fusion;
	using Simulator;
	using Simulator.Protocol;

	static class LogMessages
	{
		public static IObservable<string> ToLogMessages(this IObservable<IBinaryMessage> messages)
		{
			return Observable.Merge(
				messages.TryParse(DebugLog.MessageType, DebugLog.ReadDataFrom)
					.Select(o => { return CreateDebugLogPrefix(o) + o.Message + "\n"; }),
				// We won't get fuselibs to not debug_log exceptions, so remove this to avoid duplicates. 
				// See https://github.com/fusetools/Fuse/issues/2515#issuecomment-259435178
				//messages.TryParse(UnhandledException.MessageType, UnhandledException.ReadDataFrom)
				//	.SelectMany(o => o.Message.ConcatOne('\n')),
				messages.TryParse(BuildLogged.MessageType, BuildLogged.ReadDataFrom)
					.Select(log => log.Text),
				messages.TryParse(BuildIssueDetected.MessageType, BuildIssueDetected.ReadDataFrom)
					.Select(issue => issue.ToString()));
		}

		static string CreateDebugLogPrefix(DebugLog o)
		{
			return "[" + o.DeviceName + "]" + ": ";
		}
	}

	public class LogView : IOutput, IStatus
	{
		readonly NotificationBar _status;
		readonly ReplayQueueSubject<string> _mainLogStream = new ReplayQueueSubject<string>();
		readonly ReplayQueueSubject<IBinaryMessage> _deviceMessages = new ReplayQueueSubject<IBinaryMessage>();
		readonly Subject<string> _clientRemoved = new Subject<string>();
		public void Write(string message)
		{
			_mainLogStream.OnNext(message);
		}

		public void Write(IBinaryMessage message)
		{
			_deviceMessages.OnNext(message);
		}

		public void Write(IEventData weirdEvent)
		{
			Console.WriteLine("What to do with " + weirdEvent + "?");
		}

		public void OnClientDisconnected(string client)
		{
			_clientRemoved.OnNext(client);
		}


		public LogView(ProjectModel project, IScheduler scheduler)
		{
			var showLog = UserSettings.Bool("ShowLog").OrTrue();

			IsExpanded = showLog;

			_status = new NotificationBar(Property.Constant(false), scheduler);
			var errorView = new ErrorView(project, _deviceMessages, _clientRemoved, (proj, source) => { /* TODO */ });
			var logChanged = new Subject<Unit>();
			var logTab = new LogViewTab("Log", CreateLogView(_mainLogStream, _deviceMessages, logChanged), logChanged.Select(_ => true));
			var problemsTab = new LogViewTab("Problems", errorView.Create(), errorView.NotifyUser);

			var activeTab = new BehaviorSubject<LogViewTab>(logTab);
			TabHeader = LogViewHeader.CreateHeader(new[] { logTab, problemsTab }, activeTab, showLog);
			TabContent = activeTab
				.Select(tab => tab.Content)
				.Switch();
		}

		public IControl NotifiactionBar
		{
			get { return _status.Control; }
		}

		static IControl CreateLogView(IObservable<string> mainLogStream, IObservable<IBinaryMessage> deviceMessages, ISubject<Unit> changed)
		{
// Keep a buffer of 10 tweets from the time before this control was mounted for the first time
			var bufferedStream = mainLogStream.Replay(10);//10 * 140);
			bufferedStream.Connect();

			bufferedStream.Select(_ => Unit.Default).Subscribe(changed);

			var clearLog = new Subject<Unit>();
			var doClear = Command.Enabled(() => clearLog.OnNext(Unit.Default));
			var mainLogView = Fusion.LogView.Create(stream: bufferedStream, color: Theme.DefaultText, clear: clearLog, darkTheme: Theme.IsDark)
				.WithPadding(new Thickness<Points>(5));
			var deviceLogs = new List<DeviceLog>();
			var activeLogView = new BehaviorSubject<IControl>(mainLogView);
			var activeChanged = new Subject<Unit>();
			var mainLogButton = CreateLogSelectButton("All Output", mainLogView, activeLogView, activeChanged, true);
			var clearButton = Label.Create("Clear", color: Theme.Active)
				.OnMouse(pressed: doClear);

			var buttons = new BehaviorSubject<IEnumerable<IControl>>(CreateButtonList(mainLogButton, deviceLogs));

			deviceMessages.TryParse(DebugLog.MessageType, DebugLog.ReadDataFrom).Subscribe(
				l =>
				{
					var device = deviceLogs.FirstOrDefault(d 
						=> (d.DeviceId == l.DeviceId) 
						|| (d.DeviceName == l.DeviceName && l.DeviceName == "Viewport"));

					if (device == null)
					{
						device = new DeviceLog(l.DeviceId, l.DeviceName, activeLogView, activeChanged, clearLog);
						deviceLogs.Add(device);
						buttons.OnNext(CreateButtonList(mainLogButton, deviceLogs));
					}
					device.Write(l.Message);
				});

			return Layout.Dock()
				.Top(Layout.Dock()
					.Right(clearButton)
					.Left(buttons.StackFromLeft())
					.Fill()
					.WithPadding(new Thickness<Points>(10)))
				.Fill(activeLogView.Switch());
		}

		public static IControl CreateLogSelectButton(string caption, IControl thisLogView, IObserver<IControl> activeLogView, ISubject<Unit> activeChanged, bool startActive)
		{
			var iGotActivated = new Subject<Unit>();
			var isActive = activeChanged.Select(_ => false).Merge(iGotActivated.Select(_ => true)).StartWith(startActive);
			var font = Font.SystemDefault(Observable.Return(13.0), isActive);
			var color = isActive.Select(a => a ? Theme.DefaultText : Theme.DisabledText).Switch();
			var activateMainLog = Command.Enabled(() =>
				{
					activeChanged.OnNext(Unit.Default);
					iGotActivated.OnNext(Unit.Default);
					activeLogView.OnNext(thisLogView);
				});

			return Label.Create(caption, font: font, color: color)
				.OnMouse(pressed: activateMainLog)
				.WithPadding(new Thickness<Points>(0, 0, 10, 0));
		}

		static IEnumerable<IControl> CreateButtonList(IControl mainLogButton, IEnumerable<DeviceLog> deviceLogs)
		{
			var buttons = new List<IControl> {mainLogButton};
			buttons.AddRange(deviceLogs.Select(d => d.Activate));
			return buttons;
		}

		public IProperty<bool> IsExpanded { get; set; }
		public IControl TabHeader { get; set; }
		public IControl TabContent { get; set; }

		public void Busy(string message, params Option[] options)
		{
			_status.Busy(message, options);
		}

		public void Error(string message, string details, params Option[] options)
		{
			Write(details);
			_status.Error(message, details, options);
		}

		public void Error(string message, params Option[] options)
		{
			_status.Error(message, options);
		}

		public void Ready()
		{
			_status.Ready();
		}
	}

	class DeviceLog
	{
		public readonly string DeviceId;
		public readonly string DeviceName;
		public readonly IControl Content;
		public readonly IControl Activate;

		readonly ISubject<string> _stream = new ReplayQueueSubject<string>();
		public DeviceLog(string deviceId, string deviceName, IObserver<IControl> activeLogView, ISubject<Unit> activeChanged, IObservable<Unit> clearLog)
		{
			DeviceId = deviceId;
			DeviceName = deviceName;
			Content = Fusion.LogView.Create(stream: _stream, color: Theme.DefaultText, clear: clearLog, darkTheme: Theme.IsDark)
				.WithPadding(new Thickness<Points>(5));
			Activate = LogView.CreateLogSelectButton(DeviceName, Content, activeLogView, activeChanged, false);
		}

		public void Write(string message)
		{
			_stream.OnNext(message + "\n");
		}
	}
}
