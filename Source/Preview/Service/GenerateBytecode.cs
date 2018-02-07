using System;
using System.IO;

namespace Outracks.Simulator.Protocol
{
	public class GenerateBytecode : IBinaryMessage
	{
		public static readonly string MessageType = "GenerateBytecode";
		public string Type { get { return MessageType; } }

		public Guid Id { get; private set; }
		public ImmutableList<string> UxFilePaths { get; private set; }

		public GenerateBytecode(Guid id, ImmutableList<string> uxFiles)
		{
			Id = id;
			UxFilePaths = uxFiles;
		}
			
		public void WriteDataTo(BinaryWriter writer)
		{
			writer.WriteGuid(Id);
			List.Write(writer, UxFilePaths, (Action<string, BinaryWriter>)WriteUxFile);
		}

		public static GenerateBytecode ReadDataFrom(BinaryReader reader)
		{
			var id = reader.ReadGuid();
			var uxFiles = List.Read(reader, (Func<BinaryReader, string>)ReadUxFile);
			return new GenerateBytecode(id, uxFiles);
		}

		static void WriteUxFile(string str, BinaryWriter writer)
		{
			writer.Write(str);
		}

		static string ReadUxFile(BinaryReader reader)
		{
			return reader.ReadString();
		}

	}
}