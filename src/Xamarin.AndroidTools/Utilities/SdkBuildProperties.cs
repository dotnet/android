//
// SdkBuildProperties.cs
//
// Author:
//       Greg Munn <greg.munn@xamarin.com>
//
// Copyright (c) 2015 Xamarin Inc
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Mono.AndroidTools;

namespace Xamarin.AndroidTools.Utilities
{
	/// <summary>
	/// Simple utility functions to read properties from files like 'source.properties' and 'build.prop'
	/// </summary>
	static class SdkBuildProperties
	{
		public static IEnumerable<string> LoadProperties (string propertyFile)
		{
			if (File.Exists (propertyFile)) {
				var allText = File.ReadAllText(propertyFile).Replace("\r", string.Empty);
				return allText.Split (new [] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
			}

			return null;
		}

		public static string GetPropertyValue(this IEnumerable<string> properties, string propertyName)
		{
			if (properties == null)
				return null;

			var prop = properties.FirstOrDefault (p => p.StartsWith (propertyName, StringComparison.InvariantCultureIgnoreCase));
			if (!string.IsNullOrEmpty (prop)) {
				var propValues = prop.Split (new [] { propertyName }, StringSplitOptions.RemoveEmptyEntries);
				if (propValues.Length > 0) {
					return propValues [0];
				}

				return string.Empty;
			}

			return null;
		}
	}
}

