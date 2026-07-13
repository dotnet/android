// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xamarin.Android.Tools;

/// <summary>
/// Phases of the SDK bootstrap operation.
/// </summary>
public enum SdkBootstrapPhase
{
	/// <summary>Reading the manifest feed.</summary>
	ReadingManifest,
	/// <summary>Downloading the command-line tools archive.</summary>
	Downloading,
	/// <summary>Verifying the downloaded archive checksum.</summary>
	Verifying,
	/// <summary>Extracting the archive.</summary>
	Extracting,
	/// <summary>Bootstrap completed successfully.</summary>
	Complete
}
