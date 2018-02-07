using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Fuse.Preview;
using Outracks.Fuse.Model;

namespace Outracks.Fuse.Stage
{
	using Fusion;
	using Simulator.Bytecode;

	class StageController : IStage, IDisposable
	{
		public readonly IProperty<bool> SelectionEnabled = Property.Create(false);

		readonly BehaviorSubject<IImmutableList<IViewport>> _viewports = new BehaviorSubject<IImmutableList<IViewport>>(ImmutableList<IViewport>.Empty);
		readonly BehaviorSubject<Optional<IViewport>> _focusedViewport = new BehaviorSubject<Optional<IViewport>>(Optional.None());

		readonly ContextController _context;
		readonly PreviewController _preview;
		readonly IFuse _fuse;

		readonly Subject<OpenGlVersion> _glVersion = new Subject<OpenGlVersion>();

		readonly PreviewDevices _previewDevices;
		readonly IOutput _output;
		readonly IProperty<VirtualDevice> _latestDevice;

		
		public StageController(
			ContextController context, 
			PreviewController preview, 
			IFuse fuse, 
			PreviewDevices previewDevices,
			IOutput output)
		{
			_context = context;
			_preview = preview;
			_fuse = fuse;
			_previewDevices = previewDevices;
			_output = output;

			var fallbackDevice = previewDevices.DefaultDevice
				.Select(dev => new VirtualDevice(dev, dev.DefaultOrientation))
				.AsProperty();

			var device = _focusedViewport
				.Select(mv => mv.Select(v => v.VirtualDevice.AsProperty()).Or(fallbackDevice))
				.Switch();

			_latestDevice = device;
		}

		public void Start()
		{
			OpenNewViewport();

			_preview.BuildOptions = _context.Project.BuildArgs.BuildArguments.FirstAsync().Wait();
			_preview.Build();
		}

		public IObservable<IEnumerable<IViewport>> Viewports
		{
			get { return _viewports; }
		}

		public IObservable<Optional<IViewport>> FocusedViewport
		{
			get { return _focusedViewport; }
		}

		public IObservable<Size<Points>> DefaultViewportSize
		{
			get
			{
				return _latestDevice.Select(x => x.GetOrientedSizeInPoints());
			}
		}

		public void OpenViewport(VirtualDevice virtualDevice)
		{
			var viewport = CreateViewport(virtualDevice);

			_viewports.OnNext(_viewports.Value.Add(viewport));
			_focusedViewport.OnNext(Optional.Some(viewport));
		}

		void ViewportFocused(IViewport viewport)
		{
			_focusedViewport.OnNext(Optional.Some(viewport));
		}

		void ViewportClosed(IViewport viewport)
		{
			var newViewports = _viewports.Value.Remove(viewport);
			_focusedViewport.OnNext(newViewports.LastOrNone());
			_viewports.OnNext(newViewports);
		}

		public Menu Menu
		{
			get { return CreateMenu(_focusedViewport); }
		}

		Menu CreateMenu(IObservable<Optional<IViewport>> viewport)
		{
			return Menu.Toggle("Selection", SelectionEnabled, HotKey.Create(ModifierKeys.Meta, Key.I))
				+ Menu.Separator
				+ Menu.Item("New viewport", NewViewport, hotkey: HotKey.Create(ModifierKeys.Meta, Key.T))
				+ Menu.Item("Close viewport", CloseFocusedViewport, hotkey: HotKey.Create(ModifierKeys.Meta, Key.W))
				+ Menu.Separator
				+ Menu.Item("Restart", RestartViewport(viewport))
				+ Menu.Separator
				+ DevicesMenu.Create(_latestDevice, _previewDevices)
				+ Menu.Item("Go back", GoBack, hotkey: HotKey.Create(ModifierKeys.Meta, Key.B));
		}

		Command RestartViewport(IObservable<Optional<IViewport>> viewport)
		{
			return viewport.Switch(vp => 
				Command.Create(
					isEnabled: vp.HasValue, 
					action: () =>
					{
						var index = _viewports.Value.IndexOf(vp.Value);
						if (index == -1) 
							return;

						var newViewport = CreateViewport(vp.Value.VirtualDevice.Value);
						var newViewports = _viewports.Value.RemoveAt(index).Insert(index, newViewport);

						_viewports.OnNext(newViewports);

						if (_focusedViewport.Value == vp)
							_focusedViewport.OnNext(Optional.Some(newViewport));

						vp.Value.Close();
					}));
		}

		IViewport CreateViewport(VirtualDevice virtualDevice)
		{
			_output.Busy("Launching app...");
			return new ViewportController(
				virtualDevice, ViewportFocused, ViewportClosed, self => CreateMenu(Observable.Return(Optional.Some<IViewport>(self))),
				_preview, _fuse,
				unoHost =>
				{
					Gizmos.Initialize(unoHost, SelectionEnabled, _context);
					_output.Ready(); // we should block CreateViewport until this
				},
				_glVersion);
		}

		public Command NewViewport
		{
			get
			{
				return Command.Enabled(OpenNewViewport);
			}
		}

		public void OpenNewViewport()
		{
			OpenViewport(_latestDevice.FirstAsync().Wait());
		}

		Command CloseFocusedViewport
		{
			get
			{
				return _focusedViewport.Switch(vp =>
					Command.Create(
						isEnabled: vp.HasValue,
						action: () => vp.Value.Close()));
			}
		}

		Command GoBack
		{
			get
			{
				return _focusedViewport.Switch(viewport =>
					Command.Create(
						isEnabled: viewport.HasValue,
						action: () => viewport.Value.Execute(
							new CallStaticMethod(StaticMemberName.Parse("Fuse.Input.Keyboard.EmulateBackButtonTap")))));
			}
		}

		public void Dispose()
		{
			foreach (var viewport in _viewports.Value)
			{
				viewport.Close();
			}
		}
	}
}
