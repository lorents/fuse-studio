using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Outracks.Fuse.Designer;
using Outracks.Fuse.Model;
using Outracks.Fusion;
using Outracks.IO;

namespace Outracks.Fuse
{
	class ProjectDataPathComparer : IEqualityComparer<ProjectData>
	{
		public bool Equals(ProjectData x, ProjectData y)
		{
			if (ReferenceEquals(x, y))
				return true;

			if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
				return false;

			return x.ProjectPath == y.ProjectPath;
		}

		public int GetHashCode(ProjectData obj)
		{
			return obj.ProjectPath.GetHashCode();
		}
	}

	class RecentProjects
	{
		readonly IProperty<Optional<IEnumerable<ProjectData>>> _userSetting;
		
		public RecentProjects(ISettings settings)
		{
			_userSetting = settings.List<ProjectData>("RecentProjects");

			All = _userSetting.OrEmpty()
				.Select(ProjectData.ExistingProjects)
				.Replay(1).RefCount();
		}

		public IObservable<ImmutableList<ProjectData>> All
		{
			get; private set;
		}
		
		public void Bump(ProjectModel project)
		{
			var filePath = project.Path;
			var list = All.FirstAsync().Wait();
			var name = project.Name.FirstAsync().Wait();

			var newList =
				list.Insert(0, new ProjectData(name, filePath, DateTime.Now))
					.Distinct(new ProjectDataPathComparer());

			_userSetting.Write(Optional.Some(newList), save: true);
		}

		public void Remove(IAbsolutePath path)
		{
			var list = All.FirstAsync().Wait();
			var newList =
				list.RemoveAll(item => item.ProjectPath.Equals(path))
					.Distinct(new ProjectDataPathComparer());

			_userSetting.Write(Optional.Some(newList), save: true);
		}

	}
}