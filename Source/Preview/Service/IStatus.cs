using System;

namespace Fuse.Preview
{
	public interface IStatus
	{
		void Busy(string message, params Option[] options);
		void Error(string message, string details, params Option[] options);
		void Error(string message, params Option[] options);
		void Ready();
	}

	public class Option
	{
		public Option(string text, Action action)
		{
			Action = action;
			Text = text;
		}

		public string Text { get; private set; }
		public Action Action { get; private set; }
	}

}