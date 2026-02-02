#nullable enable

using System;

namespace Java.Interop
{
	/// <summary>
	/// Exception thrown when TypeMap operations fail, such as missing types, invalid indices,
	/// or failed function pointer lookups.
	/// </summary>
	public class TypeMapException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="TypeMapException"/> class.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		public TypeMapException (string message) : base (message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TypeMapException"/> class.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception.</param>
		public TypeMapException (string message, Exception innerException) : base (message, innerException)
		{
		}
	}
}
