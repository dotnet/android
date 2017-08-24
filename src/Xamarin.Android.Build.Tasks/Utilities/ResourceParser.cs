using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	class ResourceParser
	{
		public TaskLoggingHelper Log { get; set; }

		internal int ToInt32 (string value, int @base)
		{
			try {
				return Convert.ToInt32 (value, @base);
			} catch (Exception e) {
				throw new NotSupportedException (
						string.Format ("Could not convert value '{0}' (base '{1}') into an Int32.",
							value, @base),
						e);
			}
		}

		internal static string GetNestedTypeName (string name)
		{
			switch (name) {
				case "anim": return "Animation";
				case "attr": return "Attribute";
				case "bool": return "Boolean";
				case "dimen": return "Dimension";
				default: return char.ToUpperInvariant (name[0]) + name.Substring (1);
			}
		}

		internal string GetResourceName (string type, string name, Dictionary<string, string> map)
		{
			string mappedValue;
			string key = string.Format ("{0}{1}{2}", type, Path.DirectorySeparatorChar, name).ToLowerInvariant ();

			if (map.TryGetValue (key, out mappedValue)) {
				Log.LogDebugMessage ("  - Remapping resource: {0}.{1} -> {2}", type, name, mappedValue);
				return mappedValue.Substring (mappedValue.LastIndexOf (Path.DirectorySeparatorChar) + 1);
			}

			Log.LogDebugMessage ("  - Not remapping resource: {0}.{1}", type, name);

			return name;
		}

	}
}
