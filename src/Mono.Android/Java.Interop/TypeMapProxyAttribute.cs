using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Java.Interop
{
	/// <summary>
	/// Attribute applied to generated proxy types to indicate the Java class name they map to.
	/// Used in conjunction with <see cref="JavaPeerProxy"/> to enable AOT-safe type mapping.
	/// </summary>
	/// <remarks>
	/// This attribute is applied by the build-time code generator to proxy types.
	/// It is not intended for direct use by application developers.
	/// </remarks>
	[AttributeUsage (AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class TypeMapProxyAttribute : Attribute
	{
		/// <summary>
		/// Creates a new instance of the <see cref="TypeMapProxyAttribute"/> with the specified JNI class name.
		/// </summary>
		/// <param name="jniName">The JNI class name (e.g., "android/app/Activity").</param>
		public TypeMapProxyAttribute (string jniName)
		{
			if (string.IsNullOrEmpty (jniName))
				throw new ArgumentException ("must not be null or empty", nameof (jniName));
			JniName = jniName;
		}

		/// <summary>
		/// Gets the JNI class name this proxy maps to.
		/// </summary>
		public string JniName { get; }
	}
}
