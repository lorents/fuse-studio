﻿using Mono.Options;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Outracks.IO;
using Outracks.Simulator.Protocol;

namespace Outracks.Fuse
{
	public struct BuildArgs
	{
		public readonly BehaviorSubject<bool> BuildLibraries;

		public readonly BehaviorSubject<ImmutableList<string>> All;
		public readonly IObservable<IImmutableList<string>> Defines;
		public readonly IObservable<bool> Verbose;

		public readonly IObservable<BuildProject> BuildArguments;

		public BuildArgs(IEnumerable<string> initialArgs, AbsoluteFilePath projectPath)
		{
			var buildArgs = 
				GetDefines(initialArgs)
					.Select(d => "-D" + d)
					.Concat(GetVerbose(initialArgs) ? Optional.Some("-v") : Optional.None());

			BuildLibraries = new BehaviorSubject<bool>(false);
	
			All = new BehaviorSubject<ImmutableList<string>>(buildArgs.ToImmutableList());

			Defines = All.Select(GetDefines).DistinctUntilSequenceChanged().Replay(1).RefCount();
			Verbose = All.Select(GetVerbose).DistinctUntilChanged().Replay(1).RefCount();

			BuildArguments = Observable.CombineLatest(
				Defines, BuildLibraries, Verbose, 
				(d, bl, vb) => new BuildProject(projectPath.NativePath, Simulator.List.Create(d.ToArray()), bl, vb));
		}

		static ImmutableList<string> GetDefines(IEnumerable<string> args)
		{
			return args.SelectMany(arg =>
			{
				var trimmed = arg.Trim();
				return trimmed.StartsWith("-D")
					? Optional.Some(trimmed.Substring("-D".Length))
					: Optional.None();
			}).ToImmutableList();
		}

		static bool GetVerbose(IEnumerable<string> args)
		{
			return args.Any(arg => arg.Trim() == "-v");
		}

		public static ImmutableList<string> GetArgumentList(string args)
		{
			using (var tr = new StringReader(args))
				return ArgumentSource.GetArguments(tr).ToImmutableList();
		}

		public static string GetArgumentString(IEnumerable<string> args)
		{
			return args.Select(QuoteSpace).Join(" ");
		}

		static string QuoteSpace(string s)
		{
			return s.Contains(' ')
				? "\"" + s.Replace("\"", "\\\"") + "\""
				: s;
		}
	}
}
