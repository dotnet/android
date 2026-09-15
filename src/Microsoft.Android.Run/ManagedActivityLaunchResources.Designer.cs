#nullable enable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Resources;

namespace Microsoft.Android.Run
{
	internal static class ManagedActivityLaunchResources
	{
		static readonly ResourceManager resourceManager = new ResourceManager (
			"Microsoft.Android.Run.ManagedActivityLaunchResources", typeof (ManagedActivityLaunchResources).Assembly);

		internal static CultureInfo? Culture { get; set; }

		static string GetString (string name) =>
			resourceManager.GetString (name, Culture) ?? throw new MissingManifestResourceException (name);

		internal static string ManagedLaunchUnsupported => GetString (nameof (ManagedLaunchUnsupported));
		internal static string ManagedLaunchPackageMismatch => GetString (nameof (ManagedLaunchPackageMismatch));
		internal static string ManagedLaunchComponentUnsupported => GetString (nameof (ManagedLaunchComponentUnsupported));
		internal static string ManagedLaunchLayoutUnsupported => GetString (nameof (ManagedLaunchLayoutUnsupported));
		internal static string ManagedLaunchGateTimeout => GetString (nameof (ManagedLaunchGateTimeout));
		internal static string ManagedLaunchProcessUnsupported => GetString (nameof (ManagedLaunchProcessUnsupported));
		internal static string ManagedLaunchStateUnavailable => GetString (nameof (ManagedLaunchStateUnavailable));
		internal static string ManagedLaunchStateConflict => GetString (nameof (ManagedLaunchStateConflict));
		internal static string ManagedLaunchTimeout => GetString (nameof (ManagedLaunchTimeout));
		internal static string ManagedLaunchCleanupFailed => GetString (nameof (ManagedLaunchCleanupFailed));
		internal static string ManagedLaunchMutationTimeout => GetString (nameof (ManagedLaunchMutationTimeout));
		internal static string ManagedLaunchCleanupTimeout => GetString (nameof (ManagedLaunchCleanupTimeout));
		internal static string ManagedLaunchPidTimeout => GetString (nameof (ManagedLaunchPidTimeout));
		internal static string ManagedLaunchActivityNotFound => GetString (nameof (ManagedLaunchActivityNotFound));
		internal static string ManagedLaunchStartFailed => GetString (nameof (ManagedLaunchStartFailed));
		internal static string ManagedLaunchAdbFailed => GetString (nameof (ManagedLaunchAdbFailed));
		internal static string ManagedLaunchDeviceUnavailable => GetString (nameof (ManagedLaunchDeviceUnavailable));
	}
}
