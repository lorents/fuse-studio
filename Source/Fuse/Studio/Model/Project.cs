using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using Outracks.Fuse.Editing;
using Outracks.Fusion;
using Outracks.IO;
using Outracks.Simulator;

namespace Outracks.Fuse.Model
{
	public class ProjectModel 
	{
		public AbsoluteFilePath Path
		{
			get; private set;
		}

		public BuildArgs BuildArgs
		{
			get; private set;
		}

		public AbsoluteDirectoryPath RootDirectory
		{
			get { return Path.ContainingDirectory; }
		}

		public BehaviorSubject<string> Name
		{
			get; private set;
		}
		
		public readonly ListBehaviorSubject<DocumentModel> Documents = new ListBehaviorSubject<DocumentModel>();
		
		public readonly BehaviorSubject<Scope> Scope = new BehaviorSubject<Scope>(new Scope(new UnknownElement()));

		public ProjectModel(AbsoluteFilePath path, IEnumerable<string> args)
		{
			Path = path;
			BuildArgs = new BuildArgs(args, Path);
			Name = new BehaviorSubject<string>(path.Name.WithoutExtension.ToString());

			var allElements = Documents.SelectMany(document => document.Root.ObserveSubtree()).Replay().RefCount();
			Classes = allElements.Where(e => e.HasProperty("ux:Class"));
			GlobalElements = allElements.Where(e => e.HasProperty("ux:Global"));
		}


		public IObservableList<ElementModel> GlobalElements { get; private set; }
		public IObservableList<ElementModel> Classes { get; private set; }

		public ElementModel FindElement(ObjectIdentifier id)
		{
			if (id == ObjectIdentifier.None)
				return new UnknownElement();

			var path = AbsoluteFilePath.TryParse(id.Document);

			foreach (var doc in Documents.Value)
			{
				if (doc.File.Path != path)
					continue;

				foreach (var element in doc.Root.GetSubtree())
					if (element.Id == id)
						return element;
			}

			return new UnknownElement();
		}
	}
}