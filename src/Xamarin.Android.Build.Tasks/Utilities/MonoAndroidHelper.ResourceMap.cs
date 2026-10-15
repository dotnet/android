#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

partial class MonoAndroidHelper
{
	public static Dictionary<string, string> LoadMapFile (IBuildEngine4 engine, string mapFile, StringComparer comparer)
	{
		var cachedMap = engine?.GetRegisteredTaskObjectAssemblyLocal<Dictionary<string, string>> (mapFile, RegisteredTaskObjectLifetime.Build);
		if (cachedMap != null)
			return cachedMap;
		var map = new Dictionary<string, string> (comparer);
		if (!File.Exists (mapFile))
			return map;
		foreach (var line in File.ReadLines (mapFile)) {
			var items = line.Split (new char [] { ';' }, count: 2);
			if (!map.ContainsKey (items [0]))
				map.Add (items [0], items [1]);
		}
		return map;
	}
}
