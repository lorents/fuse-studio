using System;
using Outracks;
using Outracks.Simulator;
using Outracks.Simulator.Protocol;

namespace Fuse.Preview
{
	public interface IPreview : IDisposable
	{
		void EnableUsbMode();
		int Port { get; }

		IObservable<IBinaryMessage> Messages { get; }
		IObservable<string> ClientRemoved { get; set; }
		Code AccessCode { get; }

		IDisposable LockBuild(string build);
		string Build(BuildProject args);
		void Refresh();
		void Clean();

		bool TryUpdateAttribute(ObjectIdentifier element, string attribute, string value);
	}
}