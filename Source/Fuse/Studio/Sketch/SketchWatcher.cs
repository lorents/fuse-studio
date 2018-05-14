﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Outracks.Fuse.Model;
using Outracks.Fusion;
using Outracks.IO;
using SketchConverter.API;
using Factory = SketchConverter.API.Factory;

namespace Outracks.Fuse.Designer
{
	internal static class SketchImportUtils
	{
		internal static readonly string OutputDirName = "SketchSymbols";
		internal static readonly string SketchFilesExtension = ".sketchFiles";
		internal static readonly string SketchExtention = ".sketch";

		internal static IEnumerable<IFilePath> SketchFilePaths(AbsoluteFilePath sketchListFilePath, ILogger logger)
		{
			try
			{
				return JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(sketchListFilePath.NativePath))
					.Select(FilePath.Parse);
			}
			catch (Exception e)
			{
				logger.Error("Failed to parse list of sketch files from " + sketchListFilePath + ". Please make sure it contains a valid json list. The error was: " + e.Message);
				return new List<IFilePath>();
			}
		}
		
		internal static AbsoluteFilePath SketchListFilePath(AbsoluteFilePath projectFilePath)
		{
			if (!projectFilePath.NativePath.EndsWith(".unoproj"))
			{
				throw new ArgumentException(projectFilePath.NativePath + " does not seem to be a .unoproj");
			}
			var regex = new Regex(@"\.unoproj$");
			return AbsoluteFilePath.Parse(regex.Replace(projectFilePath.NativePath, SketchFilesExtension));
		}
		
		internal static IEnumerable<AbsoluteFilePath> MakeAbsolute(IEnumerable<IFilePath> filePaths, AbsoluteDirectoryPath rootDir)
		{
			return filePaths.Select(file => FilePath.ParseAndMakeAbsolute(file, rootDir));
		}

		internal static bool IsSketchFile(IFilePath path)
		{
			return path.Name.HasExtension(SketchExtention);
		}

		internal static bool IsSketchFilesFile(IFilePath path)
		{
			return path.Name.HasExtension(SketchFilesExtension);
		}
		
	}

	public class SketchWatcher
	{
		public IObservable<string> LogMessages { get { return _logMessages; } }

		readonly IFileSystem _fileSystem;
		readonly ILogger _logger;
		readonly Subject<string> _logMessages = new Subject<string>();
		readonly IConverter _converter;

		public SketchWatcher(IReport log, IFileSystem fileSystem)
		{
			_fileSystem = fileSystem;
			_logger = new SketchLogWrapper(_logMessages, log);
			_converter = Factory.CreateConverterWithSymbolsUxBuilder(_logger);

		}

		public IDisposable Watch(ProjectModel project)
		{
			var queue = new SingleActionQueue();
			var subscription = Observable
				.Return(SketchImportUtils.SketchListFilePath(project.Path))
				.WhenChangedOrStarted(_fileSystem)
				.Where(path => File.Exists(path.NativePath))
				.Select(SketchFilePaths)
				.Select(paths => SketchImportUtils.MakeAbsolute(paths, project.RootDirectory))
				.WhenAnyChangedOrStarted(_fileSystem)
				.Subscribe(
					x => queue.Enqueue(() => Convert(x, project.RootDirectory / SketchImportUtils.OutputDirName)),
					e => { _logger.Error("Sketch importer error: " + e.Message); });

			var tokenSource = new CancellationTokenSource();
			new Thread(
				() =>
				{
					while (!tokenSource.Token.IsCancellationRequested)
					{
						queue.Dequeue()();
					}
					_logger.Info("Stopping thread");
				}){Name = "Sketch conversion", IsBackground = true}.Start();

			Action stopThread = () =>
			{
				queue.Enqueue(() => { tokenSource.Cancel();});
			};

			// TODO: This is very messy, should extract dialog to its own class
			CreateDialog(project);

			return Disposable.Combine(
				subscription,
				Disposable.Create(stopThread));
		}

		void Convert(IEnumerable<AbsoluteFilePath> files, AbsoluteDirectoryPath outputDir)
		{
			try
			{
				Directory.CreateDirectory(outputDir.NativePath);
				_converter.Convert(files.ToList().Select(f => f.NativePath), outputDir.NativePath);
			}
			catch (Exception e)
			{
				_logger.Error("Sketch importer error: " + e.Message);
			}
		}

		
		internal IEnumerable<IFilePath> SketchFilePaths(AbsoluteFilePath sketchListFilePath)
		{
			return SketchImportUtils.SketchFilePaths(sketchListFilePath, _logger);
		}
		


		internal void WriteSketchFilePaths(AbsoluteFilePath sketchListFilePath, IEnumerable<IFilePath> sketchFiles)
		{
			try
			{
				var settings = new JsonSerializerSettings();
				settings.Converters.Add(new PathJsonConverter());
				File.WriteAllText(
					sketchListFilePath.NativePath,
					JsonConvert.SerializeObject(sketchFiles, Formatting.Indented, settings));
			}
			catch(Exception e)
			{
				_logger.Error("Failed to write sketch list file: " + e.Message);
			}
		}
		
		// Todo Move UI-stuff to separate file??
		
		const int Width = 500;
		const int Height = 250;

		readonly ISubject<bool> _isVisible = new BehaviorSubject<bool>(false);
		public void CreateDialog(ProjectModel projectController)
		{
			Application.Desktop.CreateSingletonWindow(
				_isVisible,
				dialog => new Window
				{
					Title = Observable.Return("Sketch import"),
					Size = Optional.Some(Property.Constant(Optional.Some(new Size<Points>(Width, Height)))),
					Content = View(projectController, dialog), // todo why does the content use the title space??
					Background = Theme.PanelBackground,
					Foreground = Theme.DefaultText,
					Border = Separator.MediumStroke,
					Style = WindowStyle.Fat,
				});
		}

		public IControl View(ProjectModel project, IDialog<object> dialog)
		{
			var newFile = Property.Create("");
			var files = new ListBehaviorSubject<IFilePath>();

			var listPath = SketchImportUtils.SketchListFilePath(project.Path);
			var paths = File.Exists(listPath.NativePath) 
				? SketchFilePaths(listPath) 
				: Enumerable.Empty<IFilePath>();

			foreach (var absoluteFilePath in paths)
				files.OnAdd(absoluteFilePath);
			
			var addNewSketchFileCommand =
				Command.Create(newFile.Select((pathString) =>
						{
							if (!string.IsNullOrWhiteSpace(pathString))
							{
								var path = FilePath.Parse(pathString);

								if (_fileSystem.Exists((path as IAbsolutePath) ?? project.RootDirectory / ((RelativeFilePath)path)))
									return Optional.Some(path);
							}
							return Optional.None();
						})
					.WherePerElement(path => SketchImportUtils.IsSketchFile(path) && !files.Value.Contains(path))
					.SelectPerElement(
						path => (Action)(() =>
						{
							_logger.Info("Adding sketch file to watch: " + path);
							files.OnAdd(path);
							newFile.Write("");
						})));

			var content = Layout.Dock()
				.Top(
					Layout.StackFromLeft(
							Label.Create(
								"Add sketch files to import symbols from",
								font: Theme.DefaultFont,
								textAlignment: TextAlignment.Left,
								color: Theme.DescriptorText))
						.CenterHorizontally())
				.Top(Spacer.Medium)
				.Bottom(
					Layout.Dock()
						.Right(
							AddButton(addNewSketchFileCommand)
								.SetToolTip("Import sketch symbols from this file")
								.Center())
						.Right(Spacer.Small)
						.Fill(
							FilePathControl.Create(
									newFile,
									Observable.Return(project.RootDirectory),
									new[] { new FileFilter("Sketch files", SketchImportUtils.SketchExtention) },
									"Add sketch file",
									"Enter path of new sketch file to add here, or click browse button",
									"Open")))
				.Fill(
					files
						.ToObservableImmutableList()
						.SelectPerElement(filePath =>
							{
								var highlightSketchFileControl = new BehaviorSubject<bool>(false);
								var fileExists = File.Exists(FilePath.ParseAndMakeAbsolute(filePath, project.RootDirectory).NativePath);

								return highlightSketchFileControl
									.DistinctUntilChanged()
									.Select(highlight =>
										{
											return Layout.Dock()
													.Bottom(Spacer.Smaller)
													.Right(
														RemoveButton(Command.Enabled(() => { files.OnRemove(files.Value.IndexOf(filePath)); }))
															.SetToolTip("Remove sketch file from watch list"))
													.Right(Spacer.Small)
													.Fill(
														Label.Create(
																Observable.Return(filePath.ToString()).AsText(),
																color: fileExists ? Theme.DefaultText : Theme.ErrorColor,
																font: Theme.DefaultFont)
															.CenterVertically()).SetToolTip(fileExists ? default(Text) : "Can't find file " + filePath)
													.OnMouse(
														entered: Command.Enabled(() => { highlightSketchFileControl.OnNext(true); }),
														exited: Command.Enabled(() => highlightSketchFileControl.OnNext(false)))
													.WithPadding(new Points(2))
													.WithBackground(highlight ? Theme.FaintBackground : Theme.PanelBackground);
										})
									.Switch();
							})
						.StackFromTop()
						.MakeScrollable());

			return
				ConfirmCancelControl.Create(
					close: Command.Enabled(() => { _isVisible.OnNext(false); }),
					confirm: Command.Enabled(
						() =>
						{
							var path = SketchImportUtils.SketchListFilePath(project.Path);
							WriteSketchFilePaths(path, files.Value);
						}),
					fill: content.WithMediumPadding(), cancel: null, confirmText: null, cancelText: null, confirmTooltip: null)
					.WithMacWindowStyleCompensation() // order of window style compensation and with background is important
					.WithBackground(Theme.PanelBackground);
		}


		public static IControl RemoveButton(Command command)
		{
			var stroke = IconStroke(command.IsEnabled);

			return Button.Create(command, state =>
				Shapes.Line(
						new Point<Points>(0, IconWidth / 2),
						new Point<Points>(IconWidth, IconWidth / 2),
						stroke: stroke)
					.WithWidth(IconWidth)
					.WithHeight(IconWidth));
		}

		public static IControl AddButton(Command command)
		{
			var stroke = IconStroke(command.IsEnabled);

			return Button.Create(command, state =>
				Layout.Layer(
						Shapes.Line(
							new Point<Points>(IconWidth / 2, 0),
							new Point<Points>(IconWidth / 2, IconWidth),
							stroke: stroke),
						Shapes.Line(
							new Point<Points>(0, IconWidth / 2),
							new Point<Points>(IconWidth, IconWidth / 2),
							stroke: stroke))
					.WithWidth(IconWidth)
					.WithHeight(IconWidth));
		}

		static readonly Points IconWidth = 15.0;

		static Stroke IconStroke(IObservable<bool> isEnabled)
		{
			return Stroke.Create(
				thickness: 2,
				brush: isEnabled.Select(c => c
					? Theme.Active
					: Separator.WeakStroke.Brush).Switch());
		}


		public Action ShowDialog()
		{
			return () =>
			{
				_isVisible.OnNext(true);
			};
		}
	}
}
