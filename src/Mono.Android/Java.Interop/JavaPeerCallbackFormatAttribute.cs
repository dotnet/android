#nullable enable

using System;

namespace Java.Interop {

	/// <summary>
	/// Declares the version of the binding callback format used by the assembly.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Assemblies which do <em>not</em> carry this attribute use the <see cref="ConnectorDelegates" />
	/// format: every <c>[Register]</c> attribute's <c>Connector</c> names a
	/// <c>Get&lt;Method&gt;Handler()</c> method which allocates and returns a delegate wrapping a
	/// managed-callable static <c>n_*</c> callback.
	/// </para>
	/// <para>
	/// Assemblies which carry this attribute with <see cref="Version" /> set to
	/// <see cref="UnmanagedCallersOnlyCallbacks" /> emit each supported <c>n_*</c> callback as an
	/// <see cref="System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute" /> method and omit
	/// the <c>cb_*</c> delegate cache field and the <c>Get&lt;Method&gt;Handler()</c> connector
	/// method.  Such callbacks <em>must not</em> be invoked from managed code: they can only be
	/// registered with JNI via <c>RegisterNatives</c>. The <c>Connector</c> string names the
	/// native callback directly, retaining any declaring-type qualifier for a default interface
	/// method or interface adapter. Callbacks using the legacy fallback keep their connector method.
	/// The typemap generator requires implementation metadata to distinguish the two callback shapes
	/// and reports an error if a callback it needs to register cannot be resolved.
	/// </para>
	/// <para>
	/// The attribute is deliberately versioned rather than boolean so that a future callback shape
	/// can be introduced without breaking consumers which understand only earlier versions.  A
	/// consumer which does not recognize <see cref="Version" /> must treat the assembly as
	/// unsupported rather than assuming the legacy format.
	/// </para>
	/// <para>
	/// This attribute is an implementation detail of the .NET for Android bindings and the
	/// trimmable typemap generator.  It is not intended for use by application code.
	/// </para>
	/// </remarks>
	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
	public sealed class JavaPeerCallbackFormatAttribute : Attribute {

		/// <summary>
		/// The legacy format: <c>cb_*</c> delegate fields plus <c>Get*Handler()</c> connector
		/// methods, resolved reflectively or by the typemap generator.
		/// </summary>
		public const int ConnectorDelegates = 1;

		/// <summary>
		/// The experimental format: <c>n_*</c> callbacks are
		/// <see cref="System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute" /> methods and
		/// there are no <c>cb_*</c> fields or <c>Get*Handler()</c> methods.
		/// </summary>
		public const int UnmanagedCallersOnlyCallbacks = 2;

		/// <summary>
		/// Creates a new <see cref="JavaPeerCallbackFormatAttribute" />.
		/// </summary>
		/// <param name="version">
		/// One of <see cref="ConnectorDelegates" /> or <see cref="UnmanagedCallersOnlyCallbacks" />.
		/// </param>
		public JavaPeerCallbackFormatAttribute (int version)
		{
			Version = version;
		}

		/// <summary>
		/// The callback format version used by the assembly.
		/// </summary>
		public int Version { get; }
	}
}
