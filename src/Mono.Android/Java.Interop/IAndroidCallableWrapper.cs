using System;

namespace Java.Interop
{
	/// <summary>
	/// Interface for proxy types that represent Android Callable Wrappers (ACW).
	/// ACW types are .NET types that have a corresponding generated Java class which calls back into .NET via JNI.
	/// Only types with DoNotGenerateAcw=false implement this interface.
	/// </summary>
	/// <remarks>
	/// This interface provides access to function pointers for [UnmanagedCallersOnly] methods
	/// that are called from Java via JNI native method registration.
	/// 
	/// MCW (Managed Callable Wrapper) types - which wrap existing Java classes - do NOT
	/// implement this interface since Java never calls back into them.
	/// </remarks>
	public interface IAndroidCallableWrapper
	{
		/// <summary>
		/// Gets a function pointer for a marshal method at the specified index.
		/// This is used to resolve [UnmanagedCallersOnly] method pointers for JNI callbacks.
		/// </summary>
		/// <param name="methodIndex">The index of the marshal method within this type's method table.</param>
		/// <returns>A function pointer to the UCO method, or <see cref="IntPtr.Zero"/> if the index is invalid.</returns>
		IntPtr GetFunctionPointer (int methodIndex);
	}
}
