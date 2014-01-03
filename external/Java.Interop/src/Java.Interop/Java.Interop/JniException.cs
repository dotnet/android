using System;

namespace Java.Interop
{
	public class JniException : Exception
	{
		public JniException (string message)
			: base (message)
		{
		}

		public JniException (string message, Exception innerException)
			: base (message, innerException)
		{
		}
	}
}

