#nullable enable

using System;
using System.Threading;
using Java.Interop;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Helpers for walking the Java interface hierarchy of a class.
/// </summary>
static class JavaInterfaceHierarchy
{
	static JniMethodInfo? s_classGetInterfacesMethod;

	/// <summary>
	/// Recursively visits the interfaces declared on <paramref name="klass"/> and their
	/// super-interfaces, invoking <paramref name="resolver"/> with each interface's JNI type
	/// name. Returns the first non-null result. <c>Class.getInterfaces ()</c> only returns
	/// directly declared interfaces, so the walk recurses to cover transitive ones; directly
	/// declared interfaces are visited before their super-interfaces, so the most-derived match
	/// is preferred. The <paramref name="klass"/> reference is owned by the caller and is not
	/// disposed here.
	/// </summary>
	public static TResult? FindFirst<TResult> (JniObjectReference klass, Func<string?, TResult?> resolver)
		where TResult : class
	{
		var interfaces = JniEnvironment.InstanceMethods.CallObjectMethod (klass, GetClassGetInterfacesMethod ());
		try {
			if (!interfaces.IsValid) {
				return null;
			}

			int count = JniEnvironment.Arrays.GetArrayLength (interfaces);
			for (int i = 0; i < count; i++) {
				var iface = JniEnvironment.Arrays.GetObjectArrayElement (interfaces, i);
				try {
					var ifaceName = JniEnvironment.Types.GetJniTypeNameFromClass (iface);

					var result = resolver (ifaceName);
					if (result != null) {
						return result;
					}

					// Recurse into super-interfaces.
					var nested = FindFirst (iface, resolver);
					if (nested != null) {
						return nested;
					}
				} finally {
					JniObjectReference.Dispose (ref iface);
				}
			}
		} finally {
			JniObjectReference.Dispose (ref interfaces);
		}

		return null;
	}

	static JniMethodInfo GetClassGetInterfacesMethod ()
	{
		var method = s_classGetInterfacesMethod;
		if (method != null) {
			return method;
		}

		var classClass = JniEnvironment.Types.FindClass ("java/lang/Class");
		try {
			method = JniEnvironment.InstanceMethods.GetMethodID (classClass, "getInterfaces", "()[Ljava/lang/Class;");
		} finally {
			JniObjectReference.Dispose (ref classClass);
		}

		var previous = Interlocked.CompareExchange (ref s_classGetInterfacesMethod, method, null);
		return previous ?? method;
	}
}
