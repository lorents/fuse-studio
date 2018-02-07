using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Fuse.Preview;
using Outracks.Fusion;
using Outracks.IO;
using Uno.ProjectFormat;

namespace Outracks.Fuse
{
	public class ProjectWatcher 
	{
		public static ProjectWatcher Create(IDocument<byte[]> projectFile, IScheduler scheduler)
		{
			var name = new ReplaySubject<string>(1);
			var buildOutputDirectory = new ReplaySubject<AbsoluteDirectoryPath>(1);
			var uxFiles = new ListBehaviorSubject<AbsoluteFilePath>();
			var bundleFiles = new ListBehaviorSubject<AbsoluteFilePath>();
			var fuseJsFiles = new ListBehaviorSubject<AbsoluteFilePath>();
			var projectPath = projectFile.Path;

			var projectSnapshots =
				projectFile.Contents
					.Select(_ => Project.Load(projectPath.NativePath))
					.CombineLatest(Observable.Interval(TimeSpan.FromSeconds(2), scheduler).StartWith(0), (a, b) => a)
					.Select(project =>
					{
						var root = AbsoluteDirectoryPath.Parse(project.RootDirectory);
						project.InvalidateItems();
						return new
						{
							Name = project.Name,
							Files = project.GetFlattenedItems(),
							BuildOutputDirectory =
								string.IsNullOrEmpty(project.BuildDirectory)
									? root / new DirectoryName("build")
									: AbsoluteDirectoryPath.Parse(project.BuildDirectory)
						};
					})
					.BufferPrevious()
					.Subscribe(set =>
					{
						name.OnNext(set.Current.Name);
						buildOutputDirectory.OnNext(set.Current.BuildOutputDirectory);

						var added = set.Current.Files.Except(set.Previous.SelectMany(f => f.Files));
						var removed = set.Previous.SelectMany(f => f.Files.Except(set.Current.Files));

						foreach (var file in removed)
						{
							var index = set.Previous.Value.Files.IndexOf(file);
							switch (file.Type)
							{
								case IncludeItemType.FuseJS: fuseJsFiles.OnRemove(index); break;
								case IncludeItemType.Bundle: bundleFiles.OnRemove(index); break;
								case IncludeItemType.UXFile:
								case IncludeItemType.UX: uxFiles.OnRemove(index); break;
							}		
						}

						foreach (var file in added)
						{
							var path = projectPath.ContainingDirectory / RelativeFilePath.Parse(file.Value);
							switch (file.Type)
							{
								case IncludeItemType.FuseJS: fuseJsFiles.OnAdd(path); break;
								case IncludeItemType.Bundle: bundleFiles.OnAdd(path); break;
								case IncludeItemType.UXFile:
								case IncludeItemType.UX: uxFiles.OnAdd(path); break;
							}
						}
					});

			// TODO: handle exceptions, subscriptions

			return new ProjectWatcher
			{
				Name = name,
				BuildOutputDirectory = buildOutputDirectory,
				UxFiles = uxFiles,
				BundleFiles = bundleFiles,
				FuseJsFiles = fuseJsFiles
            };
		}

		public IObservable<string> Name { get; private set; }

		public IObservableList<AbsoluteFilePath> UxFiles { get; private set; }

		public IObservableList<AbsoluteFilePath> BundleFiles { get; private set; }

		public IObservableList<AbsoluteFilePath> FuseJsFiles { get; private set; }

		public IObservable<AbsoluteDirectoryPath> BuildOutputDirectory { get; private set; }

	}


}