using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Fuse.Preview;
using Outracks.Fuse.Designer;
using Outracks.Fuse.Editing;
using Outracks.Fuse.Hierarchy;
using Outracks.Fuse.Model;
using Outracks.Fuse.Refactoring;
using Outracks.Fuse.Setup;
using Outracks.Fuse.Stage;
using Outracks.Fusion;
using Outracks.IO;
using LogView = Outracks.Fuse.Designer.LogView;

namespace Outracks.Fuse
{
	public interface IInspectorControl
	{
		IControl Create();
	}

	static class MainWindow
	{
		public static Points ResizeBorderSize = 2;
	
		public static Window Create(
			IFuse fuse, IShell shell, IScheduler scheduler, 
			ProjectModel project,
			LogView output, 
			StageController stageController, 
			PreviewController previewController,
			ContextController contextController,
			SetupGuide setupGuide,
			SketchWatcher sketchConverter,
			Action closed)
		{
			var mode = Property.Create(Mode.Normal);
			var topMost = Property.Create(false);
			
			var projectName = project.Name;

			var removeElement = new RemoveElement(new ModelUpdater(previewController));
			var elementContext = new ElementContext(project, contextController, removeElement, scheduler);
			var outline = CreateLeftPane(contextController, elementContext, shell, new ClassExtractor(project));
			
			var inspector = Fuse.Inspector.Inspector.Create(contextController, previewController, scheduler);

			var mainWindowSize = UserSettings.Size("MainWindowSize");
			var mainWindowPosition = UserSettings.Position("MainWindowPosition");
			var mainWindowState = UserSettings.Settings.Property<WindowState>("MainWindowState");

			var compactSize = UserSettings.Size("CompactWindowSize");
			var compactPosition = UserSettings.Position("CompactWindowPosition");

			//var compactContentSize =
			//	compactSize.With(value:
			//		compactSize.Merge(
			//			stage.DefaultViewportSize.Select(Optional.Some)
			//				.CombineLatest(
			//					topBar.DesiredSize.Height,
			//					(deviceSize, topBarHeight) =>
			//						deviceSize.Select(s => 
			//							Size.Create(
			//								s.Width + ResizeBorderSize * 2 /* padding */,
			//								s.Height + topBarHeight + ResizeBorderSize * 2 + LogViewHeader.HeaderHeight /* padding */)))));

			var compactContentSize = Property.Create(Optional.Some(new Size<Points>(400,300)));

			var windowSize = mode.Select(x => x == Mode.Normal ? mainWindowSize : compactContentSize).Switch();
			var windowState = mode.Select(x => x == Mode.Normal ? mainWindowState : Property.Constant(Optional.Some(WindowState.Normal))).Switch();
			var windowPosition = mode.Select(x => x == Mode.Normal ? mainWindowPosition : compactPosition).Switch();


			
			return new Window
			{
				Closed = Command.Enabled(closed),
				Title = projectName
					.Select(Optional.Some)
					.StartWith(Optional.None())
					.Select(
						maybePath => maybePath.MatchWith(
							some: name => name + " - Fuse",
							none: () => "Fuse")),
				Size = Optional.Some(windowSize),
				Position = Optional.Some(windowPosition),
				State = Optional.Some(windowState),
				Menu = MainMenu.Create(
					fuse,
					shell,
					stageController,
					contextController.CurrentSelection.Select(elementContext.CreateMenu).Switch(),
					sketchConverter,
					previewController,
					project,
					scheduler,
					setupGuide,
					WindowMenu.Create(mode, topMost),
					output),
				TopMost = Optional.Some<IObservable<bool>>(topMost),
				//Focused = mainWindowFocused,
				Foreground = Theme.DefaultText,
				Background = Theme.PanelBackground,
				Border = Separator.MediumStroke,
				Style = WindowStyle.Fat,
				Content = Popover.Host(popover =>
				{
					Points inspectorWidth = 295;

					var right = Property.Constant(Fuse.Inspector.Inspector.Width);
					var left = UserSettings.Point("LeftPanelWidth").Or(new Points(260));
			
					var inNormalMode = mode.Select(x => x == Mode.Normal);

					return Layout.Layer(
						Shapes.Rectangle(fill: Theme.WorkspaceBackground),
						Layout.Dock()
							.Top(Toolbar.Create(popover, mode, stageController, previewController))
							.Top(Separator.Medium)

							.Panel(RectangleEdge.Right, right,
								inNormalMode,
								control: inspector,
								minSize: inspectorWidth,
								resizable: false)

							.Panel(RectangleEdge.Left, left,
								inNormalMode,
								control: outline,
								minSize: 10)

							.Fill(new StageView(stageController, project, mode).CreateStage(output, output.NotifiactionBar)));
				}),
			};
		}

		static IControl CreateLeftPane(
			ContextController context,
			ElementContext elementContext,
			IShell fileSystem,
			IClassExtractor classExtractor)
		{
			return Modal.Host(modalHost =>
			{
				var makeClassViewModel = new ExtractClassButtonViewModel(
					context.Project,
					dialogModel => ExtractClassView.OpenDialog(modalHost, dialogModel),
					fileSystem,
					classExtractor);

				var hierarchy = TreeView.Create(
					new TreeViewModel(
						context,
						makeClassViewModel.HighlightSelectedElement,
						elementContext.CreateMenu));

				return Layout.Dock()
					.Bottom(Toolbox.Toolbox.Create(context.Project, elementContext))
					.Top(ExtractClassView.CreateButton(makeClassViewModel))
					.Fill(hierarchy);
			});
		}

		public static DockBuilder Panel(
			this DockBuilder dock, RectangleEdge dockEdge, IProperty<Points> size,
			IObservable<bool> isExpanded,
			IControl control, 
			Points minSize, 
			bool resizable = true)
		{
			var availableSize = new BehaviorSubject<Size<IObservable<Points>>>(
				new Size<IObservable<Points>>(Observable.Return<Points>(double.MaxValue), Observable.Return<Points>(double.MaxValue)));
			var maxWidth = availableSize.Switch()[dockEdge.NormalAxis()];

			control = control
				.WithBackground(Theme.PanelBackground)
				.WithFrame(f => f, a => a.WithAxis(dockEdge.NormalAxis(), s => size.Min(maxWidth)))
				.WithDimension(dockEdge.NormalAxis(), size.Min(maxWidth));

			control = Layout.Dock()
				.Dock(edge: dockEdge, control: control)
				.Dock(edge: dockEdge, control: Separator.Medium)
				.Fill();

			if (resizable)
				control = control.MakeResizable(dockEdge.Opposite(), size, minSize: minSize);

			control = control.MakeCollapsable(dockEdge.Opposite(), isExpanded, lazy: false);
			
			control = control.WithFrame(
				frame => frame,
				availSize =>
				{
					availableSize.OnNext(availSize);
					return availSize;
				});

			return dock.Dock(edge: dockEdge, control: control);
		}
	}
}
