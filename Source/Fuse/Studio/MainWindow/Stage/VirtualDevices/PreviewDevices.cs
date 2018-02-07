using System;
using System.Collections.Immutable;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Security;
using Fuse.Preview;

namespace Outracks.Fuse.Stage
{
	using Fusion;
	using IO;

	public class PreviewDevices
	{
		readonly AbsoluteDirectoryPath _projectDirectory;
		readonly IShell _fileSystem;
		readonly IOutput _output;

		public PreviewDevices(
			AbsoluteDirectoryPath projectDirectory,
			IShell fileSystem,
			IOutput output)
		{
			_projectDirectory = projectDirectory;
			_fileSystem = fileSystem;
			_output = output;
			Devicess = WatchDevicesList().StartWith(LoadDevicesForProject(_projectDirectory));
			DefaultDevice = Devicess.Select(devices => devices.FirstOrNone(info => info.IsDefault).Or(Stage.Devices.Default));
		}

		public IObservable<DeviceScreen> DefaultDevice { get; private set; }

		public IObservable<IImmutableList<DeviceScreen>> Devicess { get; private set; }

		public IObservable<IImmutableList<DeviceScreen>> WatchDevicesList()
		{
			return _fileSystem.Watch(CustomDevicesFile(_projectDirectory))
					.StartWith(Unit.Default)
					.CatchAndRetry(delay: TimeSpan.FromSeconds(1))
					.Throttle(TimeSpan.FromSeconds(1.0 / 30.0))
					.Select(_ => LoadDevicesForProject(_projectDirectory));
		}

		public IImmutableList<DeviceScreen> LoadDevicesForProject(AbsoluteDirectoryPath projDir)
		{
			return TryLoadCustomDevices(projDir).Or(() => Stage.Devices.LoadDefaultDevices());
		}

		public Command CustomizeDevices()
		{
			return Command.Enabled(() =>
				{
					if (!HasCustomDevicesFile(_projectDirectory))
						CreateCustomDevices(_projectDirectory);

					var devicesFile = CustomDevicesFile(_projectDirectory);
					try
					{
						_fileSystem.OpenWithDefaultApplication(devicesFile);
					}
					catch (Exception e)
					{
						_output.Write("Failed to open " + devicesFile + ": " + e.Message + "\n");
					}
				});
		}

		Optional<ImmutableList<DeviceScreen>> TryLoadCustomDevices(AbsoluteDirectoryPath projDir)
		{
			if (!HasCustomDevicesFile(projDir))
				return Optional.None();

			var devicesFile = CustomDevicesFile(projDir);
			try
			{
				return LoadCustomDevices(devicesFile);
			}
			catch (MalformedDeviceInfo)
			{
				_output.Write("Malformed " + devicesFile + "\n");
			}
			catch (FileNotFoundException)
			{
				_output.Write("Could not find " + devicesFile + "\n");
			}
			catch (Exception e)
			{
				_output.Write("Failed to load " + devicesFile + " : " + e.Message + "\n");
			}

			return Optional.None();
		}

		/// <param name="devicesFile"></param>
		/// <exception cref="MalformedDeviceInfo" />
		/// <exception cref="IOException" />
		/// <exception cref="UnauthorizedAccessException" />
		/// <exception cref="SecurityException" />
		ImmutableList<DeviceScreen> LoadCustomDevices(AbsoluteFilePath devicesFile)
		{
			using (var stream = _fileSystem.OpenRead(devicesFile))
			{
				return Stage.Devices.LoadDevicesFrom(stream);
			}
		}

		/// <exception cref="IOException" />
		/// <exception cref="UnauthorizedAccessException" />
		void CreateCustomDevices(AbsoluteDirectoryPath projDir)
		{
			using (var stream = _fileSystem.Create(CustomDevicesFile(projDir)))
			{
				Stage.Devices.SaveDefaultDevices(stream);
			}
		}

		bool HasCustomDevicesFile(AbsoluteDirectoryPath projDir)
		{
			return _fileSystem.Exists(CustomDevicesFile(projDir));
		}

		AbsoluteFilePath CustomDevicesFile(AbsoluteDirectoryPath projDir)
		{
			return projDir / new FileName("devices.json");
		}
	}
}