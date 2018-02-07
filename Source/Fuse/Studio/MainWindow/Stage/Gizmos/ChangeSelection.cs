using System;
using System.IO;

namespace Outracks.Fuse.Stage
{
	using Simulator;

	class ChangeSelection : IBinaryMessage
	{
		public static readonly string MessageType = "Select";
		public string Type { get { return MessageType; } }

		public readonly bool IsPreview;
		public readonly ObjectIdentifier Id;
		
		public ChangeSelection(bool isPreview, ObjectIdentifier id)
		{
			if (id == null) throw new ArgumentNullException("id");

			IsPreview = isPreview;
			Id = id;
		}


		public void WriteDataTo(BinaryWriter writer)
		{
			writer.Write(IsPreview);
			ObjectIdentifier.Write(Id, writer);
		}

		public static ChangeSelection ReadDataFrom(BinaryReader reader)
		{
			return new ChangeSelection(
				isPreview: reader.ReadBoolean(),
				id: ObjectIdentifier.Read(reader));
		}
	}
}