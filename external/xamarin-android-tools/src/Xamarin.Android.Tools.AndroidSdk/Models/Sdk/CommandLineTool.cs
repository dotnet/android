// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xamarin.Android.Tools;

/// <summary>Information about an installed Android SDK command-line tool.</summary>
public sealed class CommandLineTool
{
	/// <summary>Gets the full path to the requested executable.</summary>
	public string Path { get; }

	/// <summary>
	/// Gets the installed command-line tools revision from <c>source.properties</c>,
	/// or the versioned directory name when package metadata is unavailable.
	/// </summary>
	public string? Revision { get; }

	/// <summary>Creates information for a resolved Android SDK command-line tool.</summary>
	/// <param name="path">The full path to the requested executable.</param>
	/// <param name="revision">The installed command-line tools revision, when available.</param>
	public CommandLineTool (string path, string? revision = null)
	{
		Path = path;
		Revision = revision;
	}
}
