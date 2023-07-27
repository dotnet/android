namespace Android.Content.PM;

#if ANDROID_34
public abstract partial class PackageManager
{
	public sealed partial class PackageInfoFlags
	{
		// Create overloads that accept PackageInfoFlagsLong
		[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android33.0")]
		public static Android.Content.PM.PackageManager.PackageInfoFlags Of (PackageInfoFlagsLong value)
			=> Of ((long) value);

		[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android33.0")]
		public PackageInfoFlagsLong ValueAsFlags
			=> (PackageInfoFlagsLong) Value;
	}
}

// Manually created "long" version of "PackageInfoFlags" enum, created from documentation:
// https://developer.android.com/reference/android/content/pm/PackageManager.PackageInfoFlags#of(long)
[System.Flags]
public enum PackageInfoFlagsLong : long
{
	None = 0,

	GetActivities = PackageInfoFlags.Activities,

	GetReceivers = PackageInfoFlags.Receivers,

	GetServices = PackageInfoFlags.Services,

	GetProviders = PackageInfoFlags.Providers,

	GetInstrumentation = PackageInfoFlags.Instrumentation,

	[global::System.Runtime.Versioning.ObsoletedOSPlatformAttribute ("android31.0", "The platform does not support getting IntentFilters for the package.")]
	GetIntentFilters = PackageInfoFlags.IntentFilters,

	[global::System.Runtime.Versioning.ObsoletedOSPlatformAttribute ("android28.0", "Use GetSigningCertificates instead.")]
	GetSignatures = PackageInfoFlags.Signatures,

	GetMetaData = PackageInfoFlags.MetaData,

	GetGids = PackageInfoFlags.Gids,

	[global::System.Runtime.Versioning.ObsoletedOSPlatformAttribute ("android24.0", "Replaced with MatchDisabledComponents.")]
	GetDisabledComponents = PackageInfoFlags.DisabledComponents,

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android24.0")]
	MatchDisabledComponents = PackageInfoFlags.MatchDisabledComponents,

	GetSharedLibraryFiles = PackageInfoFlags.SharedLibraryFiles,

	GetUriPermissionPatterns = PackageInfoFlags.UriPermissionPatterns,

	GetPermissions = PackageInfoFlags.Permissions,

	[global::System.Runtime.Versioning.ObsoletedOSPlatformAttribute ("android24.0", "Replaced with MatchUninstalledPackages.")]
	GetUninstalledPackages = PackageInfoFlags.UninstalledPackages,

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android24.0")]
	MatchUninstalledPackages = PackageInfoFlags.MatchUninstalledPackages,

	GetConfigurations = PackageInfoFlags.Configurations,

	[global::System.Runtime.Versioning.ObsoletedOSPlatformAttribute ("android24.0", "Replaced with MatchDisabledUntilUsedComponents.")]
	GetDisabledUntilUsedComponents = PackageInfoFlags.DisabledUntilUsedComponents,

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android24.0")]
	MatchDisabledUntilUsedComponents = PackageInfoFlags.MatchDisabledUntilUsedComponents,

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android24.0")]
	MatchSystemOnly = PackageInfoFlags.MatchSystemOnly,

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android28.0")]
	GetSigningCertificates = PackageInfoFlags.SigningCertificates,

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android29.0")]
	MatchApex = 1073741824,

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android31.0")]
	[global::System.Runtime.Versioning.ObsoletedOSPlatformAttribute ("android34.0", "Use GetAttributionsLong to avoid unintended sign extension.")]
	GetAttributions = PackageInfoFlags.Attributions,

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android34.0")]
	GetAttributionsLong = 2147483648,
}
#endif // ANDROID_34
