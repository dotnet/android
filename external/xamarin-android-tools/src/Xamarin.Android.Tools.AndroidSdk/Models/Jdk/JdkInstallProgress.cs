// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xamarin.Android.Tools
{
	/// <summary>
	/// Progress information for JDK installation.
	/// </summary>
	public record JdkInstallProgress (JdkInstallPhase Phase, double PercentComplete, string? Message = null);
}
