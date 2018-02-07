using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using Fuse.Preview;
using Outracks.Diagnostics;
using Outracks.Extensions;
using Outracks.Fuse.Dashboard;
using Outracks.Fuse.Designer;
using Outracks.Fuse.Model;
using Outracks.Fuse.Protocol;
using Outracks.Fuse.Setup;
using Outracks.Fuse.Stage;
using Outracks.Fuse.Testing;
using Outracks.Fusion;
using Outracks.IO;
using LogView = Outracks.Fuse.Designer.LogView;

namespace Outracks.Fuse
{
	public static class Program
	{
		static readonly IScheduler Scheduler = new EventLoopScheduler();
		static readonly IShell Shell = new Shell();

		static IFuse _fuse;
		static Subject<Unit> _mainWindowFocused = new Subject<Unit>();
		static List<string> _argumentList;

		static PreviewService _previewService;
		static EditorIntegration _editorIntegration;
		static SketchWatcher _sketchWatcher;
		static RecentProjects _recentProjects;

		static SetupGuide _setupGuide;
		static Dashboard.Dashboard _dashboard;
		
		[STAThread]
		public static void Main(string[] argsArray)
		{
			Thread.CurrentThread.SetInvariantCulture();

			// This is required to get all required build tools in path
			// See https://github.com/fusetools/Fuse/issues/4245 for details
			if (Platform.OperatingSystem == OS.Mac)
			{
				Environment.SetEnvironmentVariable("PATH", "/usr/local/bin:" + Environment.GetEnvironmentVariable("PATH"));
			}

			_argumentList = argsArray.ToList();
			_fuse = FuseApi.Initialize("Designer", _argumentList);

			if (!Application.InitializeAsDocumentApp(_argumentList, "Fusetools.Fuse"))
				return;

			// Initialize console redirection early to show output during startup in debug window
			ConsoleOutputWindow.InitializeConsoleRedirection();

			// Load user settings
			UserSettings.Settings = PersistentSettings.Load(
				usersettingsfile: _fuse.UserDataDir / new FileName("designer-config.json"), 
				onError: e => { /* TODO: show in a log view or something */ });
			
			// Recent projects
			_recentProjects = new RecentProjects(UserSettings.Settings);


			// Preview service
			_previewService = new PreviewService();
			Application.Terminating += _previewService.Dispose;

			// Editor integration
			_editorIntegration = new EditorIntegration(_fuse.ConnectOrSpawnAsync("Designer").ToObservable().Switch(), Scheduler);
			Application.Terminating += _editorIntegration.Dispose;

			// Sketch watcher
			_sketchWatcher = new SketchWatcher(_fuse.Report, Shell);

			
			// Windows
			_setupGuide = new SetupGuide(_fuse, doneLoadingMainWindow: _mainWindowFocused.FirstAsync());
			_dashboard = new Dashboard.Dashboard(new CreateProject(_fuse), _recentProjects);
			Application.LaunchedWithoutDocuments = _dashboard.Show;
			Application.CreateDocumentWindow = OpenProject;

			Application.Run();
		}

		public static Window OpenProject(AbsoluteFilePath projectPath)
		{
			var project = new ProjectModel(projectPath, _argumentList);
			var output = new LogView(project, Scheduler);
			
			var preview = _previewService.StartPreview(projectPath, output);

			_recentProjects.Bump(project);
			_editorIntegration.Register(project, preview);
			_sketchWatcher.Watch(project);

			var repository = new Repository(Shell, Scheduler, output);
			var devices = new PreviewDevices(projectPath.ContainingDirectory, Shell, output);
			
			var previewController = new PreviewController(preview, output);
			var projectController = new ProjectController(project, repository, output, listener: previewController);
			var contextController = new ContextController(project);
			var stageController = new StageController(contextController, previewController, _fuse, devices, output);

			Scheduler.Schedule(projectController.OpenProject);
			Scheduler.Schedule(stageController.Start);
			
			return MainWindow.Create(
				_fuse, Shell, Scheduler,
				project, output,
				stageController,
				previewController,
				contextController,
				_setupGuide,
				_sketchWatcher,
				() =>
				{
					Scheduler.Schedule(projectController.Dispose);
					Scheduler.Schedule(stageController.Dispose);
				});
		}
	}

	enum Mode
	{
		Normal,
		Compact
	}

	static class DocumentTypes
	{
		public static readonly FileFilter Project = new FileFilter("Fuse Project", "unoproj");
	}
}
