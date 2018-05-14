using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Uno.Configuration;

namespace Outracks.Fuse.Dashboard
{
	using Fusion;
	using IO;
	using Templates;
	using Designer;
	
	class ProjectList
	{
		readonly RecentProjects _recentProjects;
		readonly IShell _shell;
		readonly ISubject<ProjectListItem> _selectedItem;
		readonly IObservable<IImmutableList<ProjectListItem>> _allProjectItems;



		public ProjectList(IShell shell, CreateProject createProject, IDialog<object> parent, RecentProjects recentProjects)
		{
			_shell = shell;
			_recentProjects = recentProjects;

			var recentProjectItems = _recentProjects.All
				.CachePerElement(project =>
					new ProjectListItem(
						menuItemName: "Open",
						title: project.Name,
						descriptionTitle: "Last opened:",
						description: ProjectLastOpenedString(project),
						locationTitle: "Location:",
						location: project.ProjectPath.NativePath,
						command: Command.Enabled(async () =>
						{
							await Application.OpenDocument(project.ProjectPath, showWindow: true);
							parent.Close();
						}),
						thumbnail: Layout.Dock()
								.Fill(
								Icons.ProjectIcon()
								.WithOverlay(Shapes.Rectangle(
										stroke: Stroke.Create(0.5, Theme.DescriptorText),
										cornerRadius: Observable.Return(new CornerRadius(2))
										))
								),
						projectFile: project.ProjectPath));

			var newProjectItems = TemplateLoader
				.LoadTemplatesFrom(UnoConfig.Current.GetTemplatesDir() / "Projects", _shell)
				.Select(template =>
					new ProjectListItem(
						menuItemName: "Create",
						title: "New " + template.Name,
						descriptionTitle: "Description:",
						description: template.Description,
						locationTitle: String.Empty,
						location: String.Empty,
						command: Command.Enabled(async () =>
						{
							if (await createProject.ShowDialog(template, parent))
								parent.Close();
						}),
						thumbnail: Icon(Color.Transparent, fg => MainWindowIcons.AddIcon(Stroke.Create(1, fg)))))
				.ToImmutableList();
			
			_selectedItem = new BehaviorSubject<ProjectListItem>(newProjectItems.First());

			_allProjectItems = recentProjectItems
				.Select(newProjectItems.AddRange)
				.Replay(1).RefCount();
		}

		static string ProjectLastOpenedString(ProjectData project)
		{
			return project.LastOpened.HasValue ? DateFormat.PeriodBetween(DateTime.Now, project.LastOpened.Value) : "N/A";
		}

		static IControl Icon(Brush fill, Func<Brush, IControl> content)
		{
			Brush foreground = Theme.DisabledText;
			return Shapes
				.Rectangle(
					fill: fill,
					stroke: Stroke.Create(0.5, foreground),
					cornerRadius: Observable.Return(new CornerRadius(2)))
				.WithOverlay(
					content(foreground)
						.Center());
		}

		public Command DefaultCommand
		{
			get { return _selectedItem.Switch(item => item.Command); }
		}

		public IObservable<string> DefaultMenuItemName
		{
			get { return _selectedItem.Select(item => item.MenuItemName); }
		}

		public IControl Page
		{
			get
			{
				return _allProjectItems
					.PoolPerElement(item =>
						Selectable.Create(
							selection: _selectedItem,
							menu: CreateProjectMenu(item),
							data: item,
							defaultCommand: item.Switch(i => i.Command),
							control: state => ProjectListItemControl.Create(item, state),
							toolTip: item.Select(i => "Open " + i.Title).AsText()))
					.SelectPerElement(elm => elm.WithPadding(new Thickness<Points>(0, 0, 32, 0)))
					.DirectionalGrid(Axis2D.Horizontal, 2)
					.WithPadding(left: new Points(40), right: new Points(20))
					.MakeScrollable(darkTheme: Observable.Return(false))
					.OnKeyPressed(ModifierKeys.None, Key.Left, MoveSelection(-1), isGlobal: true)
					.OnKeyPressed(ModifierKeys.None, Key.Right, MoveSelection(+1), isGlobal: true)
					.OnKeyPressed(ModifierKeys.None, Key.Up, MoveSelection(-3), isGlobal: true)
					.OnKeyPressed(ModifierKeys.None, Key.Down, MoveSelection(+3), isGlobal: true)
					;
			}
		}

		Menu CreateProjectMenu(IObservable<ProjectListItem> item)
		{
			var project = item.Select(i => i.ProjectFile);

			return Menu.Item(item.Select(i => i.MenuItemName).AsText(), item.Switch(i => i.Command), isDefault: true)
				+ Menu.Separator
				+ ProjectMenu.CommandItems(project, _shell)
				+ Menu.Separator
				+ Menu.Item("Remove from list", RemoveRecentProject(project));
		}

		Command RemoveRecentProject(IObservable<Optional<AbsoluteFilePath>> path)
		{
			return path.Switch(p => 
				Command.Create(
					isEnabled: p.HasValue,
					action: () => _recentProjects.Remove(p.Value)));
		}
	
		Command MoveSelection(int delta)
		{
			return _allProjectItems.Switch(currentItems =>
				_selectedItem.Switch(item =>
				{
					var index = currentItems.IndexOf(item) + delta;
					while (index < 0)
						index += currentItems.Count;
					while (index >= currentItems.Count)
						index -= currentItems.Count;

					return _selectedItem.Update(currentItems.TryGetAt(index).Or(currentItems[0]));
				}));
		}
	}
}