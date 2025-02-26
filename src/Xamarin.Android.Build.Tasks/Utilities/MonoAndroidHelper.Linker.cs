#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Mono.Cecil;

namespace Xamarin.Android.Tasks
{
	public partial class MonoAndroidHelper
	{
		static readonly HashSet<string> KnownAssemblyNames = new (StringComparer.OrdinalIgnoreCase) {
			"Mono.Android",
			"Mono.Android.Export",
			"Java.Interop",
		};

		public static bool IsFrameworkAssembly (AssemblyDefinition assembly) =>
			KnownAssemblyNames.Contains (assembly.Name.Name);

		public static bool IsFrameworkAssembly (string assembly) =>
			KnownAssemblyNames.Contains (Path.GetFileNameWithoutExtension (assembly));

		// Is this assembly a .NET for Android assembly?
		public static bool IsDotNetAndroidAssembly (AssemblyDefinition assembly)
		{
			foreach (var attribute in assembly.CustomAttributes.Where (a => a.AttributeType.FullName == "System.Runtime.Versioning.TargetPlatformAttribute")) {
				foreach (var p in attribute.ConstructorArguments) {
					// Of the form "android30"
					var value = p.Value?.ToString ();

					if (value is not null && value.StartsWith ("android", StringComparison.OrdinalIgnoreCase))
						return true;
				}
			}

			return false;
		}

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
