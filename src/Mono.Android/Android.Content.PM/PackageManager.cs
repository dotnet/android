namespace Android.Content.PM;

#if ANDROID_34
public abstract partial class PackageManager
{
	public sealed partial class PackageInfoFlags
	{
		// Create overloads that accept PackageInfoLongFlags
		[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android33.0")]
		public static unsafe Android.Content.PM.PackageManager.PackageInfoFlags Of (PackageInfoLongFlags value)
			=> Of ((long) value);

		[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android33.0")]
		public unsafe PackageInfoLongFlags ValueAsFlags
			=> (PackageInfoLongFlags) Value;
	}
}

// Manually created "long" version of "PackageInfoFlags" enum, created from documentation:
// https://developer.android.com/reference/android/content/pm/PackageManager.PackageInfoFlags#of(long)
[System.Flags]
public enum PackageInfoLongFlags : long
{
	None = 0,

	GetActivities = 1,

	GetReceivers = 2,

	GetServices = 4,

	GetProviders = 8,

	GetInstrumentation = 16,

	GetIntentFilters = 32,

	GetSignatures = 64,

	GetMetaData = 128,

	GetGids = 256,

	GetDisabledComponents = 512,

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android24.0")]
	MatchDisabledComponents = 512,

	GetSharedLibraryFiles = 1024,

	GetUriPermissionPatterns = 2048,

	GetPermissions = 4096,

	GetUninstalledPackages = 8192,

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android24.0")]
	MatchUninstalledPackages = 8192,

	GetConfigurations = 16384,

	GetDisabledUntilUsedComponents = 32768,

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android24.0")]
	MatchDisabledUntilUsedComponents = 32768,

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android24.0")]
	MatchSystemOnly = 1048576,

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android28.0")]
	GetSigningCertificates = 134217728,

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android29.0")]
	MatchApex = 1073741824,

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android34.0")]
	GetAttributionsLong = 2147483648,
}
#endif // ANDROID_34
