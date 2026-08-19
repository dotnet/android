// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Xamarin.Android.Tools;

/// <summary>
/// Represents a port and protocol pair for adb forwarding/reverse operations.
/// </summary>
public record AdbPortSpec (AdbProtocol Protocol, int Port)
{
	/// <summary>
	/// Returns the adb socket spec string, e.g. "tcp:5000".
	/// </summary>
	public string ToSocketSpec () => Protocol switch {
		AdbProtocol.Tcp => FormattableString.Invariant ($"tcp:{Port}"),
		_ => throw new ArgumentOutOfRangeException (nameof (Protocol), Protocol, $"Unsupported ADB protocol: {Protocol}"),
	};

	/// <summary>
	/// Parses an adb socket spec string like "tcp:5000" into an <see cref="AdbPortSpec"/>.
	/// Returns null if the format is unrecognized.
	/// </summary>
	public static AdbPortSpec? TryParse (string? socketSpec)
	{
		if (socketSpec is not { Length: > 0 } value || string.IsNullOrWhiteSpace (value))
			return null;

		var colonIndex = value.IndexOf (':');
		if (colonIndex <= 0 || colonIndex >= value.Length - 1)
			return null;

		var protocolStr = value.Substring (0, colonIndex);
		var portStr = value.Substring (colonIndex + 1);

		if (!int.TryParse (portStr, out var port) || port <= 0 || port > 65535)
			return null;

		var protocol = protocolStr.ToLowerInvariant () switch {
			"tcp" => (AdbProtocol?) AdbProtocol.Tcp,
			_ => null,
		};

		return protocol.HasValue ? new AdbPortSpec (protocol.Value, port) : null;
	}

	public override string ToString () => ToSocketSpec ();
}
