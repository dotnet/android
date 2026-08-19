// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xamarin.Android.Tools;

/// <summary>
/// Classifies the reason an emulator boot operation failed.
/// </summary>
public enum EmulatorBootErrorKind
{
	/// <summary>No error — the boot succeeded.</summary>
	None,

	/// <summary>The emulator process could not be launched (e.g., binary not found, AVD missing).</summary>
	LaunchFailed,

	/// <summary>The emulator launched but did not finish booting within the allowed timeout.</summary>
	Timeout,

	/// <summary>The boot was cancelled via <see cref="System.Threading.CancellationToken"/>.</summary>
	Cancelled,

	/// <summary>An unexpected error occurred.</summary>
	Unknown,
}

/// <summary>
/// Result of an emulator boot operation.
/// </summary>
public record EmulatorBootResult
{
	public bool Success { get; init; }
	public string? Serial { get; init; }
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Structured error classification. Consumers should switch on this value
	/// instead of parsing <see cref="ErrorMessage"/> strings.
	/// </summary>
	public EmulatorBootErrorKind ErrorKind { get; init; }
}
