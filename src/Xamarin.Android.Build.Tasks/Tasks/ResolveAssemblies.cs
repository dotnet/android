// Copyright (C) 2011, Xamarin Inc.
// Copyright (C) 2010, Novell Inc.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using MonoDroid.Tuner;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class ResolveAssemblies : AndroidAsyncTask
	{
		readonly static string NuGetRefDirectory = $"{Path.DirectorySeparatorChar}ref{Path.DirectorySeparatorChar}";
		readonly static string NuGetLibDirectory = $"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}";

		public override string TaskPrefix => "RSA";

		// The user's assemblies to package
		[Required]
		public ITaskItem[] Assemblies { get; set; }

		[Required]
		public string [] ReferenceAssembliesDirectory { get; set; }

		[Required]
		public string TargetFrameworkVersion { get; set; }

		[Required]
		public string ProjectFile { get; set; }

		public string ProjectAssetFile { get; set; }

		public string TargetMoniker { get; set; }

		public string I18nAssemblies { get; set; }

		// The user's assemblies, and all referenced assemblies
		[Output]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		[Output]
		public ITaskItem[] ResolvedUserAssemblies { get; set; }

		[Output]
		public ITaskItem[] ResolvedFrameworkAssemblies { get; set; }

		[Output]
		public ITaskItem[] ResolvedSymbols { get; set; }

		[Output]
		public string[] ResolvedDoNotPackageAttributes { get; set; }

		public override System.Threading.Tasks.Task RunTaskAsync ()
		{
			var assemblies = new Dictionary<string, ITaskItem> (Assemblies.Length, StringComparer.Ordinal);
			try {
				foreach (var assembly in Assemblies) {
					string assemblyFileName = Path.GetFileNameWithoutExtension (assembly.ItemSpec);
					if (assemblies.ContainsKey (assemblyFileName))
						continue;
					string assemblyPath = assembly.ItemSpec;
					if (!string.IsNullOrEmpty (assembly.GetMetadata ("NuGetPackageId")) && assemblyPath.Contains (NuGetRefDirectory)) {
						assemblyPath = assemblyPath.Replace (NuGetRefDirectory, NuGetLibDirectory);
						if (!File.Exists (assemblyPath)) {
							LogDebugMessage ($"Skipping NuGet reference assembly: {assembly.ItemSpec}");
							continue;
						}
					}
					using (var pe = new PEReader (File.OpenRead (assemblyPath))) {
						var reader = pe.GetMetadataReader ();
						var assemblyDefinition = reader.GetAssemblyDefinition ();
						if (MonoAndroidHelper.IsReferenceAssembly (reader, assemblyDefinition)) {
							LogDebugMessage ($"Skipping reference assembly: {assemblyPath}");
							continue;
						}
						var taskItem = new TaskItem (assembly) {
							ItemSpec = Path.GetFullPath (assemblyPath),
						};
						if (string.IsNullOrEmpty (taskItem.GetMetadata ("ReferenceAssembly"))) {
							taskItem.SetMetadata ("ReferenceAssembly", taskItem.ItemSpec);
						}
						foreach (var handle in reader.AssemblyReferences) {
							var reference = reader.GetAssemblyReference (handle);
							var name = reader.GetString (reference.Name);
							if (name == "Mono.Android") {
								taskItem.SetMetadata ("HasMonoAndroidReference", "True");
								break;
							}
						}
						CheckAssemblyAttributes (taskItem, assemblyDefinition, reader);
						assemblies [assemblyFileName] = taskItem;
					}
				}
			} catch (Exception ex) {
				LogError ("Exception while loading assemblies: {0}", ex);
				return Done;
			}

			// Add I18N assemblies if needed
			AddI18nAssemblies (assemblies);

			var mainapiLevel = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (TargetFrameworkVersion);
			foreach (var item in api_levels.Where (x => mainapiLevel < x.Value)) {
				var itemOSVersion = MonoAndroidHelper.SupportedVersions.GetFrameworkVersionFromApiLevel (item.Value);
				LogCodedWarning ("XA0105", ProjectFile, 0,
					"The $(TargetFrameworkVersion) for {0} ({1}) is greater than the $(TargetFrameworkVersion) for your project ({2}). " +
					"You need to increase the $(TargetFrameworkVersion) for your project.", Path.GetFileName (item.Key), itemOSVersion, TargetFrameworkVersion);
			}

			var resolvedAssemblies          = new List<ITaskItem> (assemblies.Count);
			var resolvedSymbols             = new List<ITaskItem> (assemblies.Count);
			var resolvedFrameworkAssemblies = new List<ITaskItem> (assemblies.Count);
			var resolvedUserAssemblies      = new List<ITaskItem> (assemblies.Count);
			foreach (var assembly in assemblies.Values) {
				var mdb = assembly + ".mdb";
				var pdb = Path.ChangeExtension (assembly.ItemSpec, "pdb");
				if (File.Exists (mdb))
					resolvedSymbols.Add (new TaskItem (mdb));
				if (File.Exists (pdb) && Files.IsPortablePdb (pdb))
					resolvedSymbols.Add (new TaskItem (pdb));
				resolvedAssemblies.Add (assembly);
				if (MonoAndroidHelper.IsFrameworkAssembly (assembly.ItemSpec, checkSdkPath: true)) {
					resolvedFrameworkAssemblies.Add (assembly);
				} else {
					resolvedUserAssemblies.Add (assembly);
				}
			}
			ResolvedAssemblies = resolvedAssemblies.ToArray ();
			ResolvedSymbols = resolvedSymbols.ToArray ();
			ResolvedFrameworkAssemblies = resolvedFrameworkAssemblies.ToArray ();
			ResolvedUserAssemblies = resolvedUserAssemblies.ToArray ();
			ResolvedDoNotPackageAttributes = do_not_package_atts.ToArray ();
			return Done;
		}

		readonly List<string> do_not_package_atts = new List<string> ();
		readonly Dictionary<string, int> api_levels = new Dictionary<string, int> ();

		void CheckAssemblyAttributes (ITaskItem assemblyItem, AssemblyDefinition assembly, MetadataReader reader)
		{
			foreach (var handle in assembly.GetCustomAttributes ()) {
				var attribute = reader.GetCustomAttribute (handle);
				switch (reader.GetCustomAttributeFullName (attribute)) {
					case "Java.Interop.DoNotPackageAttribute": {
							var arguments = attribute.GetCustomAttributeArguments ();
							if (arguments.FixedArguments.Length > 0) {
								string file = arguments.FixedArguments [0].Value?.ToString ();
								if (string.IsNullOrWhiteSpace (file))
									LogError ("In referenced assembly {0}, Java.Interop.DoNotPackageAttribute requires non-null file name.", assembly.GetAssemblyName ().FullName);
								do_not_package_atts.Add (Path.GetFileName (file));
							}
						}
						break;
					case "System.Runtime.Versioning.TargetFrameworkAttribute": {
							var arguments = attribute.GetCustomAttributeArguments ();
							foreach (var p in arguments.FixedArguments) {
								// Of the form "MonoAndroid,Version=v8.1"
								var value = p.Value?.ToString ();
								if (!string.IsNullOrEmpty (value)) {
									int commaIndex = value.IndexOf (",", StringComparison.Ordinal);
									if (commaIndex != -1) {
										string targetFrameworkIdentifier = value.Substring (0, commaIndex);
										if (!string.IsNullOrEmpty (targetFrameworkIdentifier) && string.IsNullOrEmpty (assemblyItem.GetMetadata ("TargetFrameworkIdentifier"))) {
											assemblyItem.SetMetadata ("TargetFrameworkIdentifier", targetFrameworkIdentifier);
										}
										if (targetFrameworkIdentifier == "MonoAndroid") {
											const string match = "Version=";
											var versionIndex = value.IndexOf (match, commaIndex, StringComparison.Ordinal);
											if (versionIndex != -1) {
												versionIndex += match.Length;
												string version = value.Substring (versionIndex, value.Length - versionIndex);
												var apiLevel = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (version);
												if (apiLevel != null) {
													var assemblyName = reader.GetString (assembly.Name);
													LogDebugMessage ("{0}={1}", assemblyName, apiLevel);
													api_levels [assemblyName] = apiLevel.Value;
												}
											}
										}
									}
								}
							}
						}
						break;
					default:
						break;
				}
			}
		}

		void AddI18nAssemblies (Dictionary<string, ITaskItem> assemblies)
		{
			var i18n = Linker.ParseI18nAssemblies (I18nAssemblies);

			// Check if we should add any I18N assemblies
			if (i18n == Mono.Linker.I18nAssemblies.None)
				return;

			ResolveI18nAssembly ("I18N", assemblies);
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.CJK))
				ResolveI18nAssembly ("I18N.CJK", assemblies);
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.MidEast))
				ResolveI18nAssembly ("I18N.MidEast", assemblies);
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.Other))
				ResolveI18nAssembly ("I18N.Other", assemblies);
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.Rare))
				ResolveI18nAssembly ("I18N.Rare", assemblies);
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.West))
				ResolveI18nAssembly ("I18N.West", assemblies);
		}

		void ResolveI18nAssembly (string name, Dictionary<string, ITaskItem> assemblies)
		{
			foreach (var directory in ReferenceAssembliesDirectory) {
				string assembly = Path.Combine (directory, name + ".dll");
				if (File.Exists (assembly)) {
					var dictionary = new Dictionary<string, string> (1) {
						{ "ReferenceAssembly", assembly },
					};
					assemblies [name] = new TaskItem (assembly, dictionary);
					return;
				}
			}
			throw new FileNotFoundException ($"Unable to locate assembly: {name}");
		}
	}
}

