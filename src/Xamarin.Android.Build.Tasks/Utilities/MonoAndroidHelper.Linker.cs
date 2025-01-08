using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Xamarin.Android.Tasks
{
	public partial class MonoAndroidHelper
	{
		static readonly char [] CustomViewMapSeparator = [';'];

		public static Dictionary<string, HashSet<string>> LoadCustomViewMapFile (string mapFile)
		{
			var map = new Dictionary<string, HashSet<string>> ();
			if (!File.Exists (mapFile))
				return map;
			foreach (var s in File.ReadLines (mapFile)) {
				var items = s.Split (CustomViewMapSeparator, count: 2);
				var key = items [0];
				var value = items [1];
				HashSet<string> set;
				if (!map.TryGetValue (key, out set))
					map.Add (key, set = new HashSet<string> ());
				set.Add (value);
			}
			return map;
		}
	}
}
