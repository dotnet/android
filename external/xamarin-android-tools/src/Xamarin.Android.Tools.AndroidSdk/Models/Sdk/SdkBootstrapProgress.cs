// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xamarin.Android.Tools;

/// <summary>Progress information for SDK bootstrap operations.</summary>
public record SdkBootstrapProgress (SdkBootstrapPhase Phase, int PercentComplete = -1, string Message = "");
