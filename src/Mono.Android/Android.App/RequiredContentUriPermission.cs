namespace Android.App;

// Used by AndroidManifest.xml for the attribute 'activity.requireContentUriPermissionFromCaller'.
public enum RequiredContentUriPermission
{
	/// <summary>
	/// Default, no specific permissions are required.
	/// </summary>
	None = 0,

	/// <summary>
	/// Enforces the invoker to have read access to the passed content URIs.
	/// </summary>
	Read = 1,

	/// <summary>
	/// Enforces the invoker to have write access to the passed content URIs.
	/// </summary>
	Write = 2,

	/// <summary>
	/// Enforces the invoker to have either read or write access to the passed content URIs.
	/// </summary>
	ReadOrWrite = 3,

	/// <summary>
	/// Enforces the invoker to have write access to the passed content URIs.
	/// </summary>
	ReadAndWrite = 4,
}
