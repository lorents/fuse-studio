using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Mono.Options;
using Outracks;
using Outracks.Building;
using Outracks.IO;
using Outracks.Simulator;
using Outracks.Simulator.Bytecode;
using Outracks.Simulator.CodeGeneration;
using Outracks.Simulator.Parser;
using Outracks.Simulator.Protocol;
using Outracks.Simulator.Runtime;
using Outracks.Simulator.UXIL;
using Uno.Logging;
using Uno.UX.Markup.Common;
using Uno.UX.Markup.UXIL;

namespace Fuse.Preview
{
	public interface ISimulatorBuilder
	{
		Optional<ProjectBuild> TryBuild(BuildProject args, Log log);
	}

	class ReadyProject
	{
		
		public ProjectBytecode Bytecode { get; set; }
		public Context Context { get; set; }
		public ValueParser ValueParser { get; set; }
		public ProjectObjectIdentifiers ObjectIdentifiers { get; set; }
	}

	public class Reifier : IPreviewProcess
	{
		string _projectPath;

		IUpdater _updater = new CantUpdate();
		IReifier _reifier = new CantReify();

		readonly ISimulatorBuilder _simulatorBuilder;

		readonly IObserver<IBinaryMessage> _output; 

		public Reifier(ISimulatorBuilder simulatorBuilder, IObserver<IBinaryMessage> output)
		{
			_simulatorBuilder = simulatorBuilder;
			_output = output;
		}


		public IConnectableObservable<IBinaryMessage> Messages 
		{
			get {  throw new InvalidOperationException(); }
		}

		public string Build(
			string projectPath,
			string[] defines,
			bool buildLibraries,
			bool verbose,
			string outputDir = "")
		{
			return Build(new BuildProject(projectPath, List.ToImmutableList(defines), buildLibraries, verbose, outputDir));
		}

		string Build(BuildProject a)
		{
			var logSubject = new LogSubject(a.Id);
			logSubject.Messages.Subscribe(_output);

			var maybeBuild = _simulatorBuilder.TryBuild(a, logSubject.Log);
			if (!maybeBuild.HasValue)
			{
				_reifier = new CantReify();
				// TODO: can we update? i'm not sure any more, i guess we could try
				throw new Exception("BAD");
			}

			var log = new MarkupErrorLog(_output, a.Id);
			_reifier = new CanReify(maybeBuild.Value, log);
			_projectPath = a.ProjectPath;
			_output.OnNext(new AssemblyBuilt { Assembly = AbsoluteFilePath.Parse(maybeBuild.Value.Assembly) });

			Refresh();

			return maybeBuild.Value.Assembly;
		}
		
		public bool TryUpdateAttribute(ObjectIdentifier element, string attribute, string value)
		{
			var source = new SourceReference("N/A", Optional.None());
			var args = new UpdateAttribute(element, attribute, value, source, false);
			
			var update = _updater.GenerateUpdate(args);
			if (!update.HasValue)
				return false;

			_output.OnNext(update.Value);
			return true;
		}

		public void Refresh()
		{
			if (_projectPath == null)
				return;

			var uxFilePaths = GetIncludedFiles(_projectPath);
			var project = _reifier.GenerateReify(uxFilePaths, Guid.NewGuid());
			if (!project.HasValue)
			{
				_updater = new CantUpdate();
				throw new Exception("Somewhat bad");
			}

			_output.OnNext(new BytecodeGenerated(project.Value.Bytecode));
			_updater = new CanUpdate(project.Value);
		}

		public void Clean()
		{
			
		}

		string[] GetIncludedFiles(string projectPath)
		{
			var project = Uno.ProjectFormat.Project.Load(projectPath);
			var root = Path.GetDirectoryName(projectPath);
			return project.UXFiles.Select(f => Path.Combine(root, f.UnixPath)).ToArray();
		}

	}

	interface IReifier
	{
		Optional<ReadyProject> GenerateReify(IEnumerable<string> uxFilePaths, Guid id);
	}

	class CanReify : IReifier
	{
		readonly UxParser _parser;
		readonly ProjectBuild _build;
		readonly IMarkupErrorLog _log;

		public CanReify(ProjectBuild build, IMarkupErrorLog log)
		{
			_log = log;
			_build = build;
			_parser = new UxParser(
				build.Project,
				new GhostCompilerFactory(build.TypeInfo));
		}

		public Optional<ReadyProject> GenerateReify(IEnumerable<string> uxFilePaths, Guid id)
		{
			try
			{
				var uxFileContents = ImmutableList.ToImmutableList(ReadFileContents(uxFilePaths));
				
				try
				{
					var project = _parser.Parse(uxFileContents, _log);

					try
					{
						return new ReadyProject
						{
							Bytecode = GenerateProjectBytecode(project),
							ObjectIdentifiers = ProjectObjectIdentifiers.Create(project, _build.TypeInfo, e => _log.ReportError(e.Message)),
							ValueParser = new ValueParser(_build.TypeInfo, _build.Project),
							Context = new Context(
								new UniqueNames(),
								tryGetTagHash: null,
								projectDirectory: null,
								typesDeclaredInUx: project
									.AllNodesInProject()
									.OfType<ClassNode>()
									.Select(c => c.GetTypeName())
									.ToImmutableHashSet()),
						};
					}
					catch (Exception e)
					{
						_log.ReportError(e.Message);
						
						return Optional.None();
					}
				}
				catch (Exception)
				{
					return Optional.None();
				}
			}
			catch (IOException e)
			{
				_log.ReportError(e.Message);

				return Optional.None();
			}
		}

		ProjectBytecode GenerateProjectBytecode(Project project)
		{
			var projectDirectory = Path.GetDirectoryName(_build.Project);
			var dependencies = 
				ImmutableList.ToImmutableList(
					project.RootClasses
						.SelectMany(node => ImportExpression.FindFiles(node, projectDirectory))
						.Distinct());

			var ids = ProjectObjectIdentifiers.Create(project, _build.TypeInfo, 
				onError: e => ReportFactory.FallbackReport.Exception("Failed to create identifiers for document", e));
			
			var ctx = new Context(
				names: new UniqueNames(),
				tryGetTagHash: ids.TryGetIdentifier,
				projectDirectory: projectDirectory,
				typesDeclaredInUx: project
					.AllNodesInProject()
					.OfType<ClassNode>()
					.Select(c => c.GetTypeName())
					.ToImmutableHashSet());

			var reify = project.GenerateGlobalScopeConstructor(ctx);
			var metadata = project.GenerateMetadata(ids);
			return new ProjectBytecode(reify, dependencies, metadata);
		}

		IEnumerable<UxFileContents> ReadFileContents(IEnumerable<string> files)
		{
			return files
				.AsParallel()
				.Select(path => new UxFileContents
				{
					Path = path,
					Contents = File.ReadAllText(path)
				});
		}

	}

	class CantReify : IReifier
	{
		public Optional<ReadyProject> GenerateReify(IEnumerable<string> uxFilePaths, Guid id)
		{
			return Optional.None();
		}
	}

	interface IUpdater
	{
		Optional<IBinaryMessage> GenerateUpdate(UpdateAttribute p);
	}

	class CantUpdate : IUpdater
	{
		public Optional<IBinaryMessage> GenerateUpdate(UpdateAttribute p)
		{
			return Optional.None();
		}
	}

	class CanUpdate : IUpdater
	{
		readonly ReadyProject _project;

		public CanUpdate(ReadyProject project)
		{
			_project = project;
		}

		public Optional<IBinaryMessage> GenerateUpdate(UpdateAttribute p)
		{
			if (p.Value.HasValue && ContainsUXExpression(p.Value.Value))
				return Optional.None();

			if (p.Property.StartsWith("ux:", StringComparison.InvariantCultureIgnoreCase))
				return Optional.None();

			if (p.Property.Contains("."))
				return Optional.None();

			if (p.Property == "Layer")
				return Optional.None();

			var node = _project.ObjectIdentifiers.TryGetNode(p.Object);
			if (!node.HasValue)
				return Optional.None();

			// If this attribute contained an UX expression last reify,
			// we must require reify to remove old binding objects.
			var currentValue = node.Value.RawProperties
				.Where(x => x.Name == p.Property)
				.Select(x => x.Value)
				.FirstOrDefault();

			if (ContainsUXExpression(currentValue))
				return Optional.None();

			try
			{
				return Optional.Some(Execute(p.Object, instance =>
					new SingleProperties(node.Value, _project.Context, instance)
						.UpdateValue(p.Property, p.Value, _project.ValueParser)));
			}
			catch (Exception)
			{
				return Optional.None();
			}
		}

		static IBinaryMessage Execute(ObjectIdentifier id, Func<Expression, Statement> objectTransform)
		{
			return new BytecodeUpdated(
				new Lambda(
					Signature.Action(),
					Enumerable.Empty<BindVariable>(),
					new[] { ExecuteStatement(id, objectTransform) }));
		}

		static Statement ExecuteStatement(ObjectIdentifier id, Func<Expression, Statement> objectTransform)
		{
			return new CallStaticMethod(
				TryExecuteOnObjectsWithTag,
				new StringLiteral(id.ToString()),
				new Lambda(
					Signature.Action(Variable.This),
					Enumerable.Empty<BindVariable>(),
					new[]
				{
					objectTransform(new ReadVariable(Variable.This))
				}));
		}

		static StaticMemberName TryExecuteOnObjectsWithTag
		{
			get
			{
				return new StaticMemberName(
					TypeName.Parse(typeof(ObjectTagRegistry).FullName),
					new TypeMemberName("TryExecuteOnObjectsWithTag"));
			}
		}

		// This methods checks if "str" contains an unescaped '{' followed by an unescaped '}'
		static bool ContainsUXExpression(string str)
		{
			if (str == null) return false;

			for (int i = 0; i < str.Length; i++)
			{
				switch (str[i])
				{
					case '\\':
						i++;
						continue;
					case '{':
						for (int j = i + 1; j < str.Length; j++)
						{
							switch (str[j])
							{
								case '\\':
									j++;
									continue;
								case '}':
									return true;
							}
						}

						break;
				}
			}

			return false;
		}


	}
}