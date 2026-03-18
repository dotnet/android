#nullable enable

using System;

namespace Java.Interop
{
	/// <summary>
	/// Exception thrown when a type mapping operation fails at runtime.
	/// </summary>
	public sealed class TypeMapException : Exception
	{
		public TypeMapException (string message) : base (message) { }
		public TypeMapException (string message, Exception innerException) : base (message, innerException) { }
	}
}
