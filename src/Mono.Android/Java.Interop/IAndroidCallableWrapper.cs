#nullable enable

using System.ComponentModel;

namespace Java.Interop
{
	/// <summary>
	/// Interface for proxy types that represent Android Callable Wrappers (ACW).
	/// ACW types are .NET types that have a corresponding generated Java class
	/// which calls back into .NET via JNI native methods.
	/// </summary>
	/// <remarks>
	/// This interface is not intended to be implemented or used directly outside
	/// generated trimmable typemap code.
	/// </remarks>
	[EditorBrowsable (EditorBrowsableState.Never)]
	public interface IAndroidCallableWrapper
	{
		/// <summary>
		/// Registers JNI native methods for this ACW type.
		/// Called when the Java class is first loaded and needs its native methods bound.
		/// </summary>
		/// <param name="nativeClass">The JNI type for the Java class.</param>
		void RegisterNatives (JniType nativeClass);
	}
}
