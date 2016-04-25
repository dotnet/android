using System;
using System.Collections.Generic;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.IO;
using System.Linq;

using Java.Interop.Tools.Cecil;
using Xamarin.Android.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class CheckTargetFrameworks : Task
	{

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		[Required]
		public string TargetFrameworkVersion { get; set; }

		[Required]
		public string ProjectFile { get; set; } 

		Dictionary<ITaskItem, int> apiLevels = new Dictionary<ITaskItem, int> ();
		DirectoryAssemblyResolver res;

		int ExtractApiLevel(ITaskItem ass)
		{
			Log.LogDebugMessage (ass.ItemSpec);
			foreach (var ca in res.GetAssembly (ass.ItemSpec).CustomAttributes) {
				switch (ca.AttributeType.FullName) {
				case "System.Runtime.Versioning.TargetFrameworkAttribute":
					foreach (var p in ca.ConstructorArguments) {
						var value = p.Value.ToString ();
						if (value.StartsWith ("MonoAndroid")) {
							var values = value.Split ('=');
							return AndroidVersion.TryOSVersionToApiLevel (values[1]);
						}
					}
					break;
				}
			}
			return 0;
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("CheckTargetFrameworks Task");
			Log.LogDebugMessage ("  TargetFrameworkVersion: {0}", TargetFrameworkVersion);
			Log.LogDebugMessage ("  ProjectFile: {0}", ProjectFile);
			Log.LogDebugTaskItems ("  ResolvedUserAssemblies: {0}", ResolvedAssemblies);

			res = new DirectoryAssemblyResolver (Log.LogWarning, loadDebugSymbols: false);
			foreach (var ass in ResolvedAssemblies) {
				res.Load (Path.GetFullPath (ass.ItemSpec));
				var apiLevel = ExtractApiLevel (ass);
				if (apiLevel > 0) {
					Log.LogDebugMessage ("{0}={1}", Path.GetFileNameWithoutExtension (ass.ItemSpec), apiLevel);
					apiLevels.Add (ass, apiLevel);
				}
			}

			var mainapiLevel = AndroidVersion.TryOSVersionToApiLevel (TargetFrameworkVersion);
			foreach (var item in apiLevels.Where (x => mainapiLevel < x.Value)) {
				var itemOSVersion = AndroidVersion.TryApiLevelToOSVersion (item.Value);
				Log.LogWarning (null, "XA0105", null, ProjectFile, 0, 0, 0, 0,
					"The $(TargetFrameworkVersion) for {0} (v{1}) is greater than the $(TargetFrameworkVersion) for your project ({2}). " +
					"You need to increase the $(TargetFrameworkVersion) for your project.", Path.GetFileName (item.Key.ItemSpec), itemOSVersion, TargetFrameworkVersion);
			}

			return !Log.HasLoggedErrors;
		}
	}
}

