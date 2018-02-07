using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Outracks.IO;

namespace Outracks.Fuse.Model
{
	public class DocumentModel : IDocumentModel
	{
		public IDocument<byte[]> File { get; private set; }
		public BehaviorSubject<bool> IsReadOnly { get; private set; }
		public ElementModel Root { get; private set; }

		public DocumentModel(IDocument<byte[]> file)
		{
			File = file;
			IsReadOnly = new BehaviorSubject<bool>(false);
			Root = new RootElement(this);
		}
	}


	public class UnknownDocument : IDocumentModel
	{
		public BehaviorSubject<bool> IsReadOnly { get; private set; }

		public IDocument<byte[]> File
		{
			get { return new EmptyFile(); }
		}
		
		public ElementModel Root
		{
			get { return new UnknownElement(); }
		}

		public UnknownDocument()
		{
			IsReadOnly = new BehaviorSubject<bool>(true);
		}
	}

}