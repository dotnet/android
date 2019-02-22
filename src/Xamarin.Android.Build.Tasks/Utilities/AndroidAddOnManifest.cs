using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Monodroid
{
	class AndroidAddOn {
		internal AndroidAddOn (string apiLevel, string name, string jarPath)
		{
			ApiLevel = apiLevel;
			Name     = name;
			JarPath  = jarPath;
		}
		
		public string ApiLevel {get; private set;}		
		public string Name {get; private set;}
		public string JarPath {get; private set;}
	}
	
	class AndroidAddOnManifest
	{
		public AndroidAddOnManifest (string addOnDirectory)
		{
			string manifestFile = Path.Combine (addOnDirectory, "manifest.ini");
			if (!File.Exists (manifestFile)) {
				Console.Error.WriteLine ("monodroid.exe : uses-library warning 1 : Could not find manifest.ini for add-on {0}.", addOnDirectory);
				return;
			}
			using (var manifest = File.OpenText (manifestFile)) {
				List<string> libs = new List<string> ();
				string line;
				while ((line = manifest.ReadLine ()) != null) {
					if (string.IsNullOrEmpty (line) || line.StartsWith ("#"))
						continue;
					string[] keyValue = line.Split (new char[]{'='}, 2);
					if (keyValue.Length == 1)
						continue;
					string key;
					switch ((key = keyValue [0].Trim ())) {
					case "api":
						ApiLevel = keyValue [1].Trim ();
						break;
					case "libraries":
						libs.AddRange (keyValue [1].Split (';').Select (lib => lib.Trim ()));
						break;
					default:
						// library name?
						if (libs.Contains (key)) {
							// example: com.google.android.maps=maps.jar;API for Google Maps
							// keyValue[1] before ';' is filename
							string filename = keyValue [1].Split (new []{';'}, 2) [0];
							string libPath = Path.Combine (Path.Combine (addOnDirectory, "libs"), filename);
							if (File.Exists (libPath))
								libraries.Add (new AndroidAddOn (ApiLevel, key, libPath));
							else {
								Console.Error.WriteLine ("monodroid.exe : manifest.ini warning 1: Could not find source for library '{0}'; tried file '{1}'.",
										key, libPath);
							}
						}
						break;
					}
				}
			}
		}
		
		public string ApiLevel {get; private set;}
		
		List<AndroidAddOn> libraries = new List<AndroidAddOn>();
		public IEnumerable<AndroidAddOn> Libraries {
			get {
				return libraries;
			}
		}
		
		public static IEnumerable<AndroidAddOnManifest> GetAddOnManifests (string androidSdkPath)
		{
			string addOnsDir = Path.Combine (androidSdkPath, "add-ons");
			if (!Directory.Exists (addOnsDir))
				yield break;
			foreach (var addon in Directory.GetDirectories (addOnsDir)) {
				yield return new AndroidAddOnManifest (addon);
			}
		}
	}
}

