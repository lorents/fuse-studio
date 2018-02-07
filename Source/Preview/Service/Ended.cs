using System.IO;

namespace Outracks.Simulator.Protocol
{
	public class Ended : IBinaryMessage
	{
		public static string MessageType = "Ended";
		public string Type { get { return MessageType; } }

		public IBinaryMessage Command { get; set; }
		public bool Success { get; set; }
		
		public void WriteDataTo(BinaryWriter writer)
		{
			Command.WriteTo(writer);
			writer.Write(Success);
		}

		public static Ended ReadDataFrom(BinaryReader reader)
		{
			return new Ended
			{
				Command = BinaryMessage.ReadFrom(reader),
				Success = reader.ReadBoolean(),
			};
		}
	}
}