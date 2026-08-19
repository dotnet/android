// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Xamarin.Android.Tools;

static class SourceProperties
{
	public static bool TryGetProperty (string filePath, string propertyName, out string? value)
	{
		value = null;
		if (!File.Exists (filePath))
			return false;

		foreach (var line in File.ReadLines (filePath)) {
			var separator = line.IndexOf ('=');
			if (separator < 0)
				continue;

			var name = line.Substring (0, separator).Trim ();
			if (!string.Equals (name, propertyName, StringComparison.Ordinal))
				continue;

			value = line.Substring (separator + 1).Trim ();
			return true;
		}

		return false;
	}
}
