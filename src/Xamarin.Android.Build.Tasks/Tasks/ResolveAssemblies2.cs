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
using Xamarin.Build;

namespace Xamarin.Android.Tasks
{
	public class ResolveAssemblies2 : AndroidAsyncTask
	{
		public override string TaskPrefix => "RSA";

		// The user's assemblies to package. Includes framework assemblies.
		[Required]
		public ITaskItem[] Assemblies { get; set; }

		[Required]
		public ITaskItem[] ReferenceAssembliesDirectories { get; set; }

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

		public override System.Threading.Tasks.Task RunTaskAsync ()
		{
			using (var resolver = new MetadataResolver ()) {
				Execute (resolver);
			}
			return Done;
		}

		void Execute (MetadataResolver resolver)
		{
			foreach (var dir in ReferenceAssembliesDirectories)
				resolver.AddSearchDirectory (dir.ItemSpec);

			var assemblies = new Dictionary<string, ITaskItem> (Assemblies.Length);
			var topAssemblyReferences = new List<string> (Assemblies.Length);
			try {
				foreach (var assembly in Assemblies) {
					// Ignore FrameworkReferences, we don't want to package these and should handle packaging of the runtime counterparts elsewhere?
					if (IsFrameworkReferenceAssembly (assembly)) {
						continue;
					}
					// Add each user assembly and all referenced assemblies (recursive)
					string resolved_assembly = resolver.Resolve (assembly.ItemSpec);
					bool refAssembly = !string.IsNullOrEmpty (assembly.GetMetadata ("NuGetPackageId")) && resolved_assembly.Contains ($"{Path.DirectorySeparatorChar}ref{Path.DirectorySeparatorChar}");
					if (refAssembly || MonoAndroidHelper.IsReferenceAssembly (resolved_assembly)) {
						// Resolve "runtime" library
						var lockFile = lock_file.Value;
						if (lockFile != null)
							resolved_assembly = ResolveRuntimeAssemblyForReferenceAssembly (lockFile, assembly.ItemSpec);
						if (lockFile == null || resolved_assembly == null) {
							var file  = resolved_assembly ?? assembly.ItemSpec;
							LogCodedWarning ("XA0107", file, 0, Properties.Resources.XA0107_Ignoring, file);
							continue;
						}
					}
					topAssemblyReferences.Add (resolved_assembly);
					resolver.AddSearchDirectory (Path.GetDirectoryName (resolved_assembly));
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
				if (!IsFrameworkReferenceAssembly (assembly)) {
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
		readonly Lazy<LockFile> lock_file;
		int indent = 2;

		public ResolveAssemblies2 ()
		{
			lock_file = new Lazy<LockFile> (LoadLockFile);
		}

		LockFile LoadLockFile ()
		{
			if (!string.IsNullOrEmpty (ProjectAssetFile) && File.Exists (ProjectAssetFile)) {
				LogDebugMessage ($"Loading NuGet LockFile: {ProjectAssetFile}");
				var logger = new NuGetLogger ((s) => {
					LogDebugMessage ("{0}", s);
				});
				return LockFileUtilities.GetLockFile (ProjectAssetFile, logger);
			}
			return null;
		}
		
		public static bool IsFrameworkReferenceAssembly (ITaskItem assemblyItem)
		{
			return !string.IsNullOrEmpty (assemblyItem.GetMetadata ("FrameworkReferenceName"));
		}

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
				if (path.StartsWith ($"{Path.DirectorySeparatorChar}"))
					path = path.Substring (1);
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
				// _._ means its provided by the framework. However if we get here
				// its NOT. So lets use what we got in the first place.
				if (Path.GetFileName (path) == "_._")
					return assemblyPath;
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

			CheckAssemblyAttributes (assembly, assemblyName, reader, out string targetFrameworkIdentifier);

			LogMessage ("{0}Adding assembly reference for {1}, recursively...", new string (' ', indent), assemblyName);
			resolutionPath.Add (assemblyName);
			indent += 2;

			// Add this assembly
			ITaskItem assemblyItem = null;
			if (topLevel) {
				if (assemblies.TryGetValue (assemblyName, out assemblyItem)) {
					if (!string.IsNullOrEmpty (targetFrameworkIdentifier) && string.IsNullOrEmpty (assemblyItem.GetMetadata ("TargetFrameworkIdentifier"))) {
						assemblyItem.SetMetadata ("TargetFrameworkIdentifier", targetFrameworkIdentifier);
					}
				}
			} else {
				assemblies [assemblyName] = 
					assemblyItem = CreateAssemblyTaskItem (assemblyPath, targetFrameworkIdentifier);
			}

			// Recurse into each referenced assembly
			foreach (var handle in reader.AssemblyReferences) {
				var reference = reader.GetAssemblyReference (handle);
				string reference_assembly;
				try {
					var referenceName = reader.GetString (reference.Name);
					if (assemblyItem != null && referenceName == "Mono.Android") {
						assemblyItem.SetMetadata ("HasMonoAndroidReference", "True");
					}
					reference_assembly = resolver.Resolve (referenceName);
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

		void CheckAssemblyAttributes (AssemblyDefinition assembly, string assemblyName, MetadataReader reader, out string targetFrameworkIdentifier)
		{
			targetFrameworkIdentifier = null;

			foreach (var handle in assembly.GetCustomAttributes ()) {
				var attribute = reader.GetCustomAttribute (handle);
				var attributeFullName = reader.GetCustomAttributeFullName (attribute);
				switch (attributeFullName) {
					case "Java.Interop.DoNotPackageAttribute": {
							LogCodedWarning ("XA0122", Properties.Resources.XA0122, assemblyName, attributeFullName);
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
								// Of the form "MonoAndroid,Version=v8.1" or ".NETCoreApp,Version=v5.0,Profile=Android"
								var value = p.Value?.ToString ();
								if (!string.IsNullOrEmpty (value)) {
									int commaIndex = value.IndexOf (",", StringComparison.Ordinal);
									if (commaIndex != -1) {
										targetFrameworkIdentifier = value.Substring (0, commaIndex);
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

