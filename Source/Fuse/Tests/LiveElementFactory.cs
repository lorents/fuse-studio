﻿using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Outracks.Fuse.Live;
using Outracks.IO;
using Outracks.Simulator;

namespace Outracks.Fuse.Tests
{
	internal static class LiveElementFactory
	{
		internal static ElementEditor CreateLiveElement(
			string xmlText,
			Optional<string> path = default(Optional<string>),
			Optional<ISubject<Unit>> invalidated = default(Optional<ISubject<Unit>>))
		{
			//var root = new LiveElement(
			//	AbsoluteFilePath.Parse(path.Or("/project/MainView.ux")),
			//	Observable.Never<ILookup<ObjectIdentifier, ObjectIdentifier>>(),
			//	Observable.Return(true),
			//	s => Element.Empty,
			//	null,
			//	null); // TODO

			// Panel is an example of an instance
			// Circle is an example of a class (called MyClass)
			//root.UpdateFrom(SourceFragment.FromString(xmlText).ToXml());

			//return root;
			throw new NotImplementedException();
		}
	}
}