namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Mirrors the constants on <c>Java.Interop.JavaPeerCallbackFormatAttribute</c>, which declares the
/// shape of an assembly's generated binding callbacks.
/// </summary>
static class JavaPeerCallbackFormat
{
	/// <summary>
	/// The legacy format: a <c>cb_*</c> delegate cache field and a <c>Get*Handler ()</c> connector
	/// method per callback, with a managed-callable static <c>n_*</c> callback.  This is also the
	/// interpretation of an assembly which carries no marker attribute at all.
	/// </summary>
	public const int ConnectorDelegates = 1;

	/// <summary>
	/// The experimental format: each supported <c>n_*</c> callback is an
	/// <c>[UnmanagedCallersOnly]</c> method and there are no <c>cb_*</c> fields or
	/// <c>Get*Handler ()</c> methods.  RegisterNatives must bind such callbacks directly, and
	/// managed code must never call them.
	/// </summary>
	public const int UnmanagedCallersOnlyCallbacks = 2;
}
