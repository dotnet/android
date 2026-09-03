#nullable enable

using System;

namespace Xamarin.Android.Tasks.JniRemapping
{
	/// <summary>
	/// Thrown when an assembly cannot be rewritten: either it uses a metadata construct this
	/// prototype does not know how to reproduce, or two conflicting JNI replacements were
	/// requested for a single shared piece of data that cannot be split without moving tokens.
	/// </summary>
	sealed class JniRewriteException : Exception
	{
		public JniRewriteException (string message) : base (message)
		{
		}

		public JniRewriteException (string message, Exception innerException) : base (message, innerException)
		{
		}
	}
}
