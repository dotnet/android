// Copyright (C) 2011, Xamarin Inc.
// Copyright (C) 2010, Novell Inc.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using MonoDroid.Tuner;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class ResolveAssemblies : AsyncTask
	{
		// The user's assemblies to package
		[Required]
		public ITaskItem[] Assemblies { get; set; }

		[Required]
		public string ReferenceAssembliesDirectory { get; set; }

		[Required]
		public string TargetFrameworkVersion { get; set; }

		[Required]
		public string ProjectFile { get; set; }

		public string ProjectAssetFile { get; set; }

		public string TargetMoniker { get; set; }

		public string I18nAssemblies { get; set; }
		public string LinkMode { get; set; }

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

		public override bool Execute ()
		{
			Yield ();
			try {
				System.Threading.Tasks.Task.Run (() => {
					using (var resolver = new MetadataResolver ()) {
						Execute (resolver);
					}
				}, Token).ContinueWith (Complete);
				return base.Execute ();
			} finally {
				Reacquire ();
			}
		}

		void Execute (MetadataResolver resolver)
		{
			foreach (var dir in ReferenceAssembliesDirectory.Split (new char [] { ';' }, StringSplitOptions.RemoveEmptyEntries))
				resolver.AddSearchDirectory (dir);

			var assemblies = new Dictionary<string, ITaskItem> (Assemblies.Length);
			var topAssemblyReferences = new List<string> (Assemblies.Length);
			var logger = new NuGetLogger((s) => {
				LogDebugMessage ("{0}", s);
			});

			LockFile lockFile = null;
			if (!string.IsNullOrEmpty (ProjectAssetFile) && File.Exists (ProjectAssetFile)) {
				lockFile = LockFileUtilities.GetLockFile (ProjectAssetFile, logger);
			}

			try {
				foreach (var assembly in Assemblies) {
					var assembly_path = Path.GetDirectoryName (assembly.ItemSpec);
					resolver.AddSearchDirectory (assembly_path);

					// Add each user assembly and all referenced assemblies (recursive)
					string resolved_assembly = resolver.Resolve (assembly.ItemSpec);
					if (MonoAndroidHelper.IsReferenceAssembly (resolved_assembly)) {
						// Resolve "runtime" library
						if (lockFile != null)
							resolved_assembly = ResolveRuntimeAssemblyForReferenceAssembly (lockFile, assembly.ItemSpec);
						if (lockFile == null || resolved_assembly == null) {
							LogCodedWarning ("XA0107", resolved_assembly, 0, "Ignoring {0} as it is a Reference Assembly", resolved_assembly);
							continue;
						}
					}
					topAssemblyReferences.Add (resolved_assembly);
					var taskItem = new TaskItem (assembly) {
						ItemSpec = Path.GetFullPath (resolved_assembly),
					};
					if (string.IsNullOrEmpty (taskItem.GetMetadata ("ReferenceAssembly"))) {
						taskItem.SetMetadata ("ReferenceAssembly", taskItem.ItemSpec);
					}
					string assemblyName = Path.GetFileNameWithoutExtension (resolved_assembly);
					assemblies [assemblyName] = taskItem;
				}
			} catch (Exception ex) {
				LogError ("Exception while loading assemblies: {0}", ex);
				return;
			}
			try {
				foreach (var assembly in topAssemblyReferences)
					AddAssemblyReferences (resolver, assemblies, assembly, null);
			} catch (Exception ex) {
				LogError ("Exception while loading assemblies: {0}", ex);
				return;
			}

			// Add I18N assemblies if needed
			AddI18nAssemblies (resolver, assemblies);

			var mainapiLevel = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (TargetFrameworkVersion);
			foreach (var item in api_levels.Where (x => mainapiLevel < x.Value)) {
				var itemOSVersion = MonoAndroidHelper.SupportedVersions.GetFrameworkVersionFromApiLevel (item.Value);
				Log.LogCodedWarning ("XA0105", ProjectFile, 0,
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
		}

		readonly List<string> do_not_package_atts = new List<string> ();
		readonly Dictionary<string, int> api_levels = new Dictionary<string, int> ();
		int indent = 2;

		string ResolveRuntimeAssemblyForReferenceAssembly (LockFile lockFile, string assemblyPath)
		{
			if (string.IsNullOrEmpty(TargetMoniker)) 
				return null;

			var framework = NuGetFramework.Parse (TargetMoniker);
			if (framework == null) {
				LogCodedWarning ("XA0118", $"Could not parse '{TargetMoniker}'");
				return null;
			}
			var target = lockFile.GetTarget (framework, string.Empty);
			if (target == null) {
				LogCodedWarning ("XA0118", $"Could not resolve target for '{TargetMoniker}'");
				return null;
			}
			foreach (var folder in lockFile.PackageFolders) {
				var path = assemblyPath.Replace (folder.Path, string.Empty);
				var libraryPath = lockFile.Libraries.FirstOrDefault (x => path.StartsWith (x.Path.Replace('/', Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
				if (libraryPath == null)
					continue;
				var library = target.Libraries.FirstOrDefault (x => String.Compare (x.Name, libraryPath.Name, StringComparison.OrdinalIgnoreCase) == 0);
				if (libraryPath == null)
					continue;
				var runtime = library.RuntimeAssemblies.FirstOrDefault ();
				if (runtime == null)
					continue;
				path = Path.Combine (folder.Path, libraryPath.Path, runtime.Path).Replace('/', Path.DirectorySeparatorChar);
				if (!File.Exists (path))
					continue;
				return path;
			}
			return null;
		}

		void AddAssemblyReferences (MetadataResolver resolver, Dictionary<string, ITaskItem> assemblies, string assemblyPath, List<string> resolutionPath)
		{
			var reader = resolver.GetAssemblyReader (assemblyPath);
			var assembly = reader.GetAssemblyDefinition ();
			var assemblyName = reader.GetString (assembly.Name);

			// Don't repeat assemblies we've already done
			bool topLevel = resolutionPath == null;
			if (!topLevel && assemblies.ContainsKey (assemblyName))
				return;

			if (resolutionPath == null)
				resolutionPath = new List<string>();

			CheckAssemblyAttributes (assembly, reader, out string targetFrameworkIdentifier);

			LogMessage ("{0}Adding assembly reference for {1}, recursively...", new string (' ', indent), assemblyName);
			resolutionPath.Add (assemblyName);
			indent += 2;

			// Add this assembly
			if (topLevel) {
				if (!string.IsNullOrEmpty (targetFrameworkIdentifier) && assemblies.TryGetValue (assemblyName, out ITaskItem taskItem)) {
					if (string.IsNullOrEmpty (taskItem.GetMetadata ("TargetFrameworkIdentifier"))) {
						taskItem.SetMetadata ("TargetFrameworkIdentifier", targetFrameworkIdentifier);
					}
				}
			} else {
				assemblies [assemblyName] = CreateAssemblyTaskItem (assemblyPath, targetFrameworkIdentifier);
			}

			// Recurse into each referenced assembly
			foreach (var handle in reader.AssemblyReferences) {
				var reference = reader.GetAssemblyReference (handle);
				string reference_assembly;
				try {
					reference_assembly = resolver.Resolve (reader.GetString (reference.Name));
				} catch (FileNotFoundException ex) {
					var references = new StringBuilder ();
					for (int i = 0; i < resolutionPath.Count; i++) {
						if (i != 0)
							references.Append (" > ");
						references.Append ('`');
						references.Append (resolutionPath [i]);
						references.Append ('`');
					}

					string missingAssembly = ex.FileName;
					if (missingAssembly.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
						missingAssembly = Path.GetFileNameWithoutExtension (missingAssembly);
					}
					string message = $"Can not resolve reference: `{missingAssembly}`, referenced by {references}.";
					if (MonoAndroidHelper.IsFrameworkAssembly (ex.FileName)) {
						LogCodedError ("XA2002", $"{message} Perhaps it doesn't exist in the Mono for Android profile?");
					} else {
						LogCodedError ("XA2002", $"{message} Please add a NuGet package or assembly reference for `{missingAssembly}`, or remove the reference to `{resolutionPath [0]}`.");
					}
					return;
				}
				AddAssemblyReferences (resolver, assemblies, reference_assembly, resolutionPath);
			}

			indent -= 2;
			resolutionPath.RemoveAt (resolutionPath.Count - 1);
		}

		void CheckAssemblyAttributes (AssemblyDefinition assembly, MetadataReader reader, out string targetFrameworkIdentifier)
		{
			targetFrameworkIdentifier = null;

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
										targetFrameworkIdentifier = value.Substring (0, commaIndex);
										if (targetFrameworkIdentifier == "MonoAndroid") {
											const string match = "Version=";
											var versionIndex = value.IndexOf (match, commaIndex, StringComparison.Ordinal);
											if (versionIndex != -1) {
												versionIndex += match.Length;
												string version = value.Substring (versionIndex, value.Length - versionIndex);
												var apiLevel = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (version);
												if (apiLevel != null) {
													var assemblyName = reader.GetString (assembly.Name);
													Log.LogDebugMessage ("{0}={1}", assemblyName, apiLevel);
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

		static LinkModes ParseLinkMode (string linkmode)
		{
			if (string.IsNullOrWhiteSpace (linkmode))
				return LinkModes.SdkOnly;

			LinkModes mode = LinkModes.SdkOnly;

			Enum.TryParse<LinkModes> (linkmode.Trim (), true, out mode);

			return mode;
		}

		void AddI18nAssemblies (MetadataResolver resolver, Dictionary<string, ITaskItem> assemblies)
		{
			var i18n = Linker.ParseI18nAssemblies (I18nAssemblies);
			var link = ParseLinkMode (LinkMode);

			// Check if we should add any I18N assemblies
			if (i18n == Mono.Linker.I18nAssemblies.None)
				return;

			ResolveI18nAssembly (resolver, "I18N", assemblies);
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.CJK))
				ResolveI18nAssembly (resolver, "I18N.CJK", assemblies);
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.MidEast))
				ResolveI18nAssembly (resolver, "I18N.MidEast", assemblies);
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.Other))
				ResolveI18nAssembly (resolver, "I18N.Other", assemblies);
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.Rare))
				ResolveI18nAssembly (resolver, "I18N.Rare", assemblies);
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.West))
				ResolveI18nAssembly (resolver, "I18N.West", assemblies);
		}

		void ResolveI18nAssembly (MetadataResolver resolver, string name, Dictionary<string, ITaskItem> assemblies)
		{
			var assembly = resolver.Resolve (name);
			assemblies [name] = CreateAssemblyTaskItem (assembly);
		}

		static ITaskItem CreateAssemblyTaskItem (string assembly, string targetFrameworkIdentifier = null)
		{
			var assemblyFullPath = Path.GetFullPath (assembly);
			var dictionary = new Dictionary<string, string> (2) {
				{ "ReferenceAssembly", assemblyFullPath },
			};
			if (!string.IsNullOrEmpty (targetFrameworkIdentifier))
				dictionary.Add ("TargetFrameworkIdentifier", targetFrameworkIdentifier);
			return new TaskItem (assemblyFullPath, dictionary);
		}
	}
}

