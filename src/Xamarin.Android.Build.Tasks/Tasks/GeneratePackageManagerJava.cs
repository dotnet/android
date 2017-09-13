// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class GeneratePackageManagerJava : Task
	{
		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		[Required]
		public ITaskItem[] ResolvedUserAssemblies { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		[Required]
		public string UseSharedRuntime { get; set; }

		[Required]
		public string MainAssembly { get; set; }

		[Required]
		public string TargetFrameworkVersion { get; set; }

		[Required]
		public string Manifest { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("GeneratePackageManagerJava Task");
			Log.LogDebugMessage ("  OutputDirectory: {0}", OutputDirectory);
			Log.LogDebugMessage ("  TargetFrameworkVersion: {0}", TargetFrameworkVersion);
			Log.LogDebugMessage ("  Manifest: {0}", Manifest);
			Log.LogDebugMessage ("  UseSharedRuntime: {0}", UseSharedRuntime);
			Log.LogDebugMessage ("  MainAssembly: {0}", MainAssembly);
			Log.LogDebugTaskItems ("  ResolvedAssemblies:", ResolvedAssemblies);
			Log.LogDebugTaskItems ("  ResolvedUserAssemblies:", ResolvedUserAssemblies);

			var shared_runtime = string.Compare (UseSharedRuntime, "true", true) == 0;
			var doc = AndroidAppManifest.Load (Manifest, MonoAndroidHelper.SupportedVersions);
			int minApiVersion = doc.MinSdkVersion == null ? 4 : (int) doc.MinSdkVersion;
			// We need to include any special assemblies in the Assemblies list
			var assemblies = ResolvedUserAssemblies.Select (p => p.ItemSpec)
				.Concat (MonoAndroidHelper.GetFrameworkAssembliesToTreatAsUserAssemblies (ResolvedAssemblies))						
				.ToList ();
			var mainFileName = Path.GetFileName (MainAssembly);
			Func<string,string,bool> fileNameEq = (a,b) => a.Equals (b, StringComparison.OrdinalIgnoreCase);
			assemblies = assemblies.Where (a => fileNameEq (a, mainFileName)).Concat (assemblies.Where (a => !fileNameEq (a, mainFileName))).ToList ();

			// Write first to a temporary file
			var temp = Path.GetTempFileName ();

			using (var pkgmgr = File.CreateText (temp)) {
				// Write the boilerplate from the MonoPackageManager.java resource
				var packageManagerResource = minApiVersion < 9 ? "MonoPackageManager.api4.java" : "MonoPackageManager.java";
				using (var template = new StreamReader (Assembly.GetExecutingAssembly ().GetManifestResourceStream (packageManagerResource))) {
					string line;
					while ((line = template.ReadLine ()) != null) {
						pkgmgr.WriteLine (line);
					}
				}

				// Write all the user assemblies
				pkgmgr.WriteLine ("class MonoPackageManager_Resources {");
				pkgmgr.WriteLine ("\tpublic static final String[] Assemblies = new String[]{");

				pkgmgr.WriteLine ("\t\t/* We need to ensure that \"{0}\" comes first in this list. */", mainFileName);
				foreach (var assembly in assemblies) {
					pkgmgr.WriteLine ("\t\t\"" + Path.GetFileName (assembly) + "\",");
				}

				// Write the assembly dependencies
				pkgmgr.WriteLine ("\t};");
				pkgmgr.WriteLine ("\tpublic static final String[] Dependencies = new String[]{");

				//foreach (var assembly in assemblies.Except (args.Assemblies)) {
				//        if (args.SharedRuntime && !Toolbox.IsInSharedRuntime (assembly))
				//                pkgmgr.WriteLine ("\t\t\"" + Path.GetFileName (assembly) + "\",");
				//}

				pkgmgr.WriteLine ("\t};");

				// Write the platform api apk we need
				pkgmgr.WriteLine ("\tpublic static final String ApiPackageName = {0};", shared_runtime
						? string.Format ("\"Mono.Android.Platform.ApiLevel_{0}\"",
							MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (TargetFrameworkVersion))
						: "null");
				pkgmgr.WriteLine ("}");
			}

			// Only copy to the real location if the contents actually changed
			var dest = Path.GetFullPath (Path.Combine (OutputDirectory, "MonoPackageManager.java"));

			MonoAndroidHelper.CopyIfChanged (temp, dest);

			try { File.Delete (temp); } catch (Exception) { }
			
			try { File.Delete (temp); } catch (Exception) { }

			return !Log.HasLoggedErrors;
		}
	}
}
