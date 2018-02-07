using Outracks;
using Outracks.Fuse.Protocol;

namespace Fuse.Preview
{
	public interface IOutput : IStatus
	{
		void Write(string message);
		void Write(IBinaryMessage message);
		void Write(IEventData weirdEvent);
	}
}