#nullable enable

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Tools;

internal static class SdkPackageListParser
{
	static readonly (string Header, bool IsInstalled, bool IsUpdate) [] PackageSections = {
		("Installed packages:", true, false),
		("Available Packages:", false, false),
		("Available Updates:", false, true),
	};
	static readonly Regex WhitespaceColumnSeparator = new Regex (@"\s{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	internal static (IReadOnlyList<SdkPackage> Installed, IReadOnlyList<SdkPackage> Available) Parse (string output)
	{
		var installed = new List<SdkPackage> ();
		var available = new List<SdkPackage> ();
		List<SdkPackage>? target = null;
		var parsingUpdates = false;

		lines: foreach (var line in output.Split (new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)) {
			var trimmed = line.Trim ();

			// Match section headers case-insensitively: the classic sdkmanager emits
			// "Available Packages:" (uppercase P) while the newer Android CLI replacement
			// (cmdline-tools 23+) emits "Available packages:" (lowercase p).
			foreach (var section in PackageSections) {
				if (trimmed.IndexOf (section.Header, StringComparison.OrdinalIgnoreCase) < 0)
					continue;

				target = section.IsInstalled ? installed : available;
				parsingUpdates = section.IsUpdate;
				continue lines;
			}

			if (target is null || trimmed.StartsWith ("Path", StringComparison.Ordinal) || trimmed.StartsWith ("---", StringComparison.Ordinal))
				continue;

			// The classic sdkmanager prints a pipe-delimited table
			// ("path | version | description | location"). sdkmanager is deprecated in
			// cmdline-tools 23+ and replaced by the "Android CLI", which prints
			// whitespace-aligned columns with no pipes. Fall back to splitting on runs of
			// 2+ spaces when no pipe is present; otherwise every package row is skipped
			// (pipe split yields a single column), producing an empty installed list.
			var parts = trimmed.IndexOf ('|') >= 0
				? trimmed.Split ('|')
				: WhitespaceColumnSeparator.Split (trimmed);
			if (parsingUpdates && string.Equals (parts [0].Trim (), "ID", StringComparison.Ordinal))
				continue;
			var versionIndex = parsingUpdates ? 2 : 1;
			if (parts.Length <= versionIndex)
				continue;

			var path = parts [0].Trim ();
			if (string.IsNullOrEmpty (path))
				continue;

			target.Add (new SdkPackage (
				path,
				Version: parts [versionIndex].Trim (),
				Description: !parsingUpdates && parts.Length > 2 ? parts [2].Trim () : null,
				IsInstalled: target == installed
			));
		}

		return (installed.AsReadOnly (), available.AsReadOnly ());
	}
}
