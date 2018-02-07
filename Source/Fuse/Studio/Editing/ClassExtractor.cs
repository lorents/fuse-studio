using System;
using System.IO;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Xml.Linq;
using Outracks.Fuse.Model;
using Outracks.Fuse.Refactoring;
using Outracks.IO;
using Uno.ProjectFormat;

namespace Outracks.Fuse.Editing
{
	public class ClassExtractor : IClassExtractor
	{
		readonly ProjectModel _project;
		readonly ISubject<string> _logMessages = new Subject<string>();


		public ClassExtractor(ProjectModel project)
		{
			_project = project;
		}

		public IObservable<string> LogMessages
		{
			get { return _logMessages; }
		}

		const string uxClass = "ux:Class";
		public void ExtractClass(ElementModel element, string name, Optional<RelativeFilePath> fileName)
		{
			try
			{
				if (fileName.HasValue)
				{
					ExtractClassToFile(element, name, fileName.Value);
					return;
				}

				element[uxClass].OnNext(name);
				element.XElement.SetAttributeValue(KeyToName(uxClass), name);
				element.XElement.AddAfterSelf(SourceFragment.FromString(string.Format("<{0} />", name)));
			}
			catch (Exception ex)
			{
				_logMessages.OnNext(string.Format("Error: Unable to create class. {0}\r\n", ex.Message));
			}
		}

		void ExtractClassToFile(ElementModel element, string name, RelativeFilePath fileName)
		{
			var xml = element.XElement;
			xml.SetAttributeValue(KeyToName(uxClass), name);
			CreateDocument(fileName, SourceFragment.FromXml(xml));
			//TODO await element.Replace(_ => SourceFragment.FromString(string.Format("<{0} />", name)));
		}

		static XName KeyToName(string name)
		{
			return name.StartsWith("ux:")
				? XName.Get(name.StripPrefix("ux:"), "http://schemas.fusetools.com/ux")
				: XName.Get(name);
		}

		/// <summary>
		/// Creates a new document in the project, and adds it to unoproj file.
		/// Also creates any missing directories in path.
		/// </summary>
		public void CreateDocument(RelativeFilePath relativePath, SourceFragment contents = null)
		{
			contents = contents ?? SourceFragment.Empty;

			var rootDir = _project.RootDirectory;
			var newFilePath = rootDir / relativePath;

			var containingDir = newFilePath.ContainingDirectory;
			Directory.CreateDirectory(containingDir.NativePath);

			using (var stream = File.Create(newFilePath.NativePath)) // TODO: check with shell CreateNew
			{
				var bytes = contents.ToBytes();
				stream.Write(bytes, 0, bytes.Length);
			}

			var project = Project.Load(_project.Path.NativePath);

			if (project.AllFiles.None(item => item.UnixPath == relativePath.NativeRelativePath))
			{
				project.MutableIncludeItems.Add(new IncludeItem(relativePath.NativeRelativePath));
				project.Save();
			}
		}
	}
}