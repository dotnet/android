using System;
using System.Collections.Generic;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.IO;
using System.Linq;

using Java.Interop.Tools.Cecil;
using Xamarin.Android.Tools;

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

		int ExtractApiLevel(DirectoryAssemblyResolver res, ITaskItem ass)
		{
			Log.LogDebugMessage (ass.ItemSpec);
			foreach (var ca in res.GetAssembly (ass.ItemSpec).CustomAttributes) {
				switch (ca.AttributeType.FullName) {
				case "System.Runtime.Versioning.TargetFrameworkAttribute":
					foreach (var p in ca.ConstructorArguments) {
						var value = p.Value.ToString ();
						if (value.StartsWith ("MonoAndroid")) {
							var values = value.Split ('=');
							return MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (values [1]) ?? 0;
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

			using (var res = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: false)) {
				foreach (var assembly in ResolvedAssemblies) {
					res.Load (Path.GetFullPath (assembly.ItemSpec));
					var apiLevel = ExtractApiLevel (res, assembly);
					if (apiLevel > 0) {
						Log.LogDebugMessage ("{0}={1}", Path.GetFileNameWithoutExtension (assembly.ItemSpec), apiLevel);
						apiLevels.Add (assembly, apiLevel);
					}
				}
			}

			var mainapiLevel = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (TargetFrameworkVersion);
			foreach (var item in apiLevels.Where (x => mainapiLevel < x.Value)) {
				var itemOSVersion = MonoAndroidHelper.SupportedVersions.GetFrameworkVersionFromApiLevel (item.Value);
				Log.LogWarning (null, "XA0105", null, ProjectFile, 0, 0, 0, 0,
					"The $(TargetFrameworkVersion) for {0} (v{1}) is greater than the $(TargetFrameworkVersion) for your project ({2}). " +
					"You need to increase the $(TargetFrameworkVersion) for your project.", Path.GetFileName (item.Key.ItemSpec), itemOSVersion, TargetFrameworkVersion);
			}

			return !Log.HasLoggedErrors;
		}
	}
}

