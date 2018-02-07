using System;
using System.Text;
using Outracks.Fuse.Live;
using Outracks.Fuse.Model;

namespace Outracks.Fuse.Editing
{
	public class CutCopyPaste
	{
		readonly RemoveElement _removeElement;

		public CutCopyPaste(RemoveElement removeElement)
		{
			_removeElement = removeElement;
		}

		public SourceFragment Cut(ElementModel element)
		{
			var fragment = Copy(element);

			_removeElement.Remove(element);

			return fragment;
		}

		public SourceFragment Copy(ElementModel element)
		{
			var fragment = SourceFragment.FromXml(element.XElement);
			string elementIndent;
			if (element.XElement.TryGetElementIndent(out elementIndent))
			{
				fragment = RemoveIndentFromDescendantNodes(fragment, elementIndent);
			}
			return fragment;
		}

		static SourceFragment RemoveIndentFromDescendantNodes(SourceFragment fragment, string elementIndent)
		{
			// If all subsequent lines start with the indent specified, remove it
			var stringBuilder = new StringBuilder();
			var isFirst = true;
			var indentFixSuccess = true;
			foreach (var line in fragment.ToString().Split('\n'))
			{
				if (isFirst)
				{
					isFirst = false;
					stringBuilder.Append(line);
				}
				else if (!line.StartsWith(elementIndent))
				{
					indentFixSuccess = false;
					break;
				}
				else
				{
					stringBuilder.Append('\n');
					stringBuilder.Append(line.Substring(elementIndent.Length));
				}
			}
			return indentFixSuccess ? SourceFragment.FromString(stringBuilder.ToString()) : fragment;
		}

	

	}
}