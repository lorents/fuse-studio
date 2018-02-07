using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Fuse.Preview;
using Outracks.Fuse.Model;
using Outracks.IO;
using Outracks.Simulator;
using Outracks.Simulator.Bytecode;
using Outracks.Simulator.Protocol;

namespace Outracks.Fuse
{

	public class PreviewController : IModelChangeListener
	{
		readonly IPreview _preview;
		readonly IStatus _status;

		readonly HashSet<IDocumentModel> _documentsToRemap = new HashSet<IDocumentModel>();

		public PreviewController(IPreview preview, IStatus status)
		{
			_preview = preview;
			_status = status;

			NeedsFlush = true;
		}

		public void EnableUsbMode()
		{
			_preview.EnableUsbMode();
		}

		public int Port
		{
			get { return _preview.Port; }
		}


		// Build and Clean

		public BuildProject BuildOptions
		{
			get; set;
		}

		public void Build()
		{
			try
			{
				_status.Busy("Building...");

				var assembly = _preview.Build(BuildOptions);
				AvailableBuild.OnNext(AbsoluteFilePath.Parse(assembly));
				_preview.Refresh();
				
				_status.Ready();
			}
			catch (Exception e)
			{
				_status.Error("Failed to build project", e.Message, new Option("Rebuild", Build));
			}
		}

		public void Clean()
		{
			try
			{
				_status.Busy("Cleaning...");
				
				AvailableBuild.OnNext(Optional.None());

				_preview.Clean();
			}
			catch (Exception e)
			{
				_status.Error("Failed to clean project", e.Message, new Option("Try again", Clean));
			}
		}

		public IDisposable LockBuild(AbsoluteFilePath build)
		{
			return _preview.LockBuild(build.NativePath);
		}
		
		public BehaviorSubject<Optional<AbsoluteFilePath>> AvailableBuild =
			new BehaviorSubject<Optional<AbsoluteFilePath>>(Optional.None());


		// Update and Refresh

		public void Refresh()
		{
			try
			{
				_status.Busy("Refreshing...");
				_preview.Refresh();
				_status.Ready();
			}
			catch (Exception e)
			{
				_status.Error("Failed to refresh preview", e.Message, new Option("Try again", Refresh));
			}
		}

		public bool NeedsFlush
		{
			get; private set;
		}

		public Code AccessCode
		{
			get { return _preview.AccessCode; }
		}

		public IObservable<ILookup<ObjectIdentifier, ObjectIdentifier>> Metadata
		{
			get
			{
				return _preview.Messages
					.TryParse(BytecodeGenerated.MessageType, BytecodeGenerated.ReadDataFrom)
					.Select(bcg => Lookup.Empty<ObjectIdentifier, ObjectIdentifier>());
			}
		}

		public void ElementAttributeChanged(ElementModel element, string attribute)
		{
			if (NeedsFlush)
				return;

			NeedsFlush = !_preview.TryUpdateAttribute(element.Id, attribute, element[attribute].Value);
		}

		public void ElementChildrenChanged(ElementModel element)
		{
			_documentsToRemap.Add(element.Document);
			NeedsFlush = true;
		}

		public void ElementNameChanged(ElementModel element)
		{
			NeedsFlush = true;
		}

		public void Flush()
		{
			if (!NeedsFlush)
				return;

			try
			{
				_status.Busy("Reloading...");

				_preview.Refresh();
				RemapElements();
	
				NeedsFlush = false;

				_status.Ready();
			}
			catch (Exception e)
			{
				_status.Error("Auto-reload failed", e.Message);
			}
		}

		void RemapElements()
		{
			foreach (var document in _documentsToRemap)
			{
				var index = 0;
				foreach (var element in document.Root.GetSubtree())
					element.Id = new ObjectIdentifier(document.File.Path.NativePath, index++);
			}
			
			_documentsToRemap.Clear();
		}

	}
}