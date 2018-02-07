using System.Collections;
using System.Collections.Generic;
using System.Reactive.Subjects;
using Outracks;
using Outracks.Simulator;

namespace Fuse.Preview
{
	public interface IPreviewProcess
	{
		IConnectableObservable<IBinaryMessage> Messages { get; }

		string Build(string projectPath, string[] defines, bool buildLibraries, bool verbose, string outputDir = "");
		void Refresh();
		void Clean();

		bool TryUpdateAttribute(ObjectIdentifier element, string attribute, string value);


		//bool IsElementOfType(ObjectIdentifier element, string typeName);
		//ObjectIdentifier GetBaseElement(ObjectIdentifier element);

		//IEnumerable<KeyValuePair<string,string>> GetAvailableAttributes(ObjectIdentifier element);
	}



}