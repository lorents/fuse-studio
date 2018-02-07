using System;
using System.Linq;
using Fuse.Preview;
using Outracks.Fuse.Model;
using Outracks.Fusion;
using Outracks.IO;
using Outracks.Simulator.CodeGeneration;

namespace Outracks.Fuse
{


	public class ProjectController
	{
		readonly ProjectModel _project;
		readonly Repository _repository;
		readonly IOutput _output;
		readonly ModelUpdater _modelUpdater;
		
		public ProjectController(
			ProjectModel project,
			Repository repository,
			IOutput output,
			IModelChangeListener listener)
		{
			_project = project;
			_repository = repository;
			_output = output;
			_modelUpdater = new ModelUpdater(listener);
		}

		public void OpenProject()
		{
			try
			{
				_output.Busy("Loading " + _project.Path.Name + "...");

				var projectFile = _repository.OpenBinary(_project.Path);
				var projectWatcher = ProjectWatcher.Create(projectFile, _repository.Scheduler);

				projectWatcher.UxFiles
					.Select(OpenDocument)
					.DisposeElements(doc => doc.File)
					.Subscribe(_project.Documents);

				var app = _project.Documents.Value
					.Select(doc => doc.Root)
					.FirstOrDefault(root => root.Name.Value == "App");
			
				if (app == null)
					throw new MissingAppTag();

				_project.Scope.OnNext(new Scope(app));

				_output.Ready();
			}
			catch (Exception e)
			{
				_output.Error("Failed to open project " + _project.Path.Name, e.Message, new Option("Try again", OpenProject));
			}
		}

		DocumentModel OpenDocument(AbsoluteFilePath path)
		{
			var file = _repository.OpenBinary(path);
			var document = new DocumentModel(file);
	
			document.File.ExternalChanges
				.Subscribe(contents => LoadDocument(document, contents));

			return document;
		}

		void LoadDocument(DocumentModel document, byte[] fileContents)
		{
			try
			{
				_output.Busy("Loading " + document.File.Path.Name + "...");

				_modelUpdater.UpdateFrom(document.Root, fileContents);
				_modelUpdater.Flush();

				_output.Ready();
			}
			catch (Exception e)
			{
				_output.Error("Failed to load document " + document.File.Path.Name, e.Message);
			}
		}

		public void Dispose()
		{
			_repository.Dispose();
		}
	}

}
