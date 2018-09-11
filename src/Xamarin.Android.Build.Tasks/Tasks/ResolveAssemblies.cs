// Copyright (C) 2011, Xamarin Inc.
// Copyright (C) 2010, Novell Inc.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using MonoDroid.Tuner;
using System.IO;
using System.Text;
using Xamarin.Android.Tools;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;

using Java.Interop.Tools.Cecil;

namespace Xamarin.Android.Tasks
{
	public class ResolveAssemblies : AsyncTask
	{
		// The user's assemblies to package
		[Required]
		public ITaskItem[] Assemblies { get; set; }

		[Required]
		public string ReferenceAssembliesDirectory { get; set; }

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
					using (var resolver = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: false)) {
						Execute (resolver);
					}
				}, Token).ContinueWith (Complete);
				return base.Execute ();
			} finally {
				Reacquire ();
			}
		}

		void Execute (DirectoryAssemblyResolver resolver)
		{
			foreach (var dir in ReferenceAssembliesDirectory.Split (new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
				resolver.SearchDirectories.Add (dir);

			var assemblies = new Dictionary<string, string> ();

			var topAssemblyReferences = new List<AssemblyDefinition> ();
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

					if (!resolver.SearchDirectories.Contains (assembly_path))
						resolver.SearchDirectories.Add (assembly_path);

					// Add each user assembly and all referenced assemblies (recursive)
					var assemblyDef = resolver.Load (assembly.ItemSpec);
					if (assemblyDef == null)
						throw new InvalidOperationException ("Failed to load assembly " + assembly.ItemSpec);
					if (MonoAndroidHelper.IsReferenceAssembly (assemblyDef)) {
						// Resolve "runtime" library
						var asmFullPath = Path.GetFullPath (assembly.ItemSpec);
						if (lockFile != null)
							assemblyDef = ResolveRuntimeAssemblyForReferenceAssembly (lockFile, resolver, asmFullPath);
						if (lockFile == null || assemblyDef == null) {
							LogCodedWarning ("XA0107", asmFullPath, 0, "Ignoring {0} as it is a Reference Assembly", asmFullPath);
							continue;
						}
					}
					topAssemblyReferences.Add (assemblyDef);
					assemblies [assemblyDef.Name.Name] = Path.GetFullPath (assemblyDef.MainModule.FileName);
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
				var pdb = Path.ChangeExtension (assembly, "pdb");
				if (File.Exists (mdb))
					resolvedSymbols.Add (new TaskItem (mdb));
				if (File.Exists (pdb) && Files.IsPortablePdb (pdb))
					resolvedSymbols.Add (new TaskItem (pdb));
				var assemblyItem = new TaskItem (assembly);
				resolvedAssemblies.Add (assemblyItem);
				if (MonoAndroidHelper.IsFrameworkAssembly (assembly, checkSdkPath: true)) {
					resolvedFrameworkAssemblies.Add (assemblyItem);
				} else {
					resolvedUserAssemblies.Add (assemblyItem);
				}
			}
			ResolvedAssemblies = resolvedAssemblies.ToArray ();
			ResolvedSymbols = resolvedSymbols.ToArray ();
			ResolvedFrameworkAssemblies = resolvedFrameworkAssemblies.ToArray ();
			ResolvedUserAssemblies = resolvedUserAssemblies.ToArray ();
			ResolvedDoNotPackageAttributes = do_not_package_atts.ToArray ();
		}

		readonly List<string> do_not_package_atts = new List<string> ();
		int indent = 2;

		AssemblyDefinition ResolveRuntimeAssemblyForReferenceAssembly (LockFile lockFile, DirectoryAssemblyResolver resolver, string assemblyPath)
		{
			if (string.IsNullOrEmpty(TargetMoniker)) 
				return null;

			var framework = NuGetFramework.Parse (TargetMoniker);
			if (framework == null) {
				LogWarning ($"Could not parse '{TargetMoniker}'");
				return null;
			}
			var target = lockFile.GetTarget (framework, string.Empty);
			if (target == null) {
				LogWarning ($"Could not resolve target for '{TargetMoniker}'");
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
				LogDebugMessage ($"Attempting to load {path}");
				return resolver.Load (path, forceLoad: true);
			}
			return null;
		}

		void AddAssemblyReferences (DirectoryAssemblyResolver resolver, Dictionary<string, string> assemblies, AssemblyDefinition assembly, List<string> resolutionPath)
		{
			var assemblyName = assembly.Name.Name;
			var fullPath = Path.GetFullPath (assembly.MainModule.FileName);

			// Don't repeat assemblies we've already done
			bool topLevel = resolutionPath == null;
			if (!topLevel && assemblies.ContainsKey (assemblyName))
				return;

			if (resolutionPath == null)
				resolutionPath = new List<string>();
			
			foreach (var att in assembly.CustomAttributes.Where (a => a.AttributeType.FullName == "Java.Interop.DoNotPackageAttribute")) {
				string file = (string) att.ConstructorArguments.First ().Value;
				if (string.IsNullOrWhiteSpace (file))
					LogError ("In referenced assembly {0}, Java.Interop.DoNotPackageAttribute requires non-null file name.", assembly.FullName);
				do_not_package_atts.Add (Path.GetFileName (file));
			}

			LogMessage ("{0}Adding assembly reference for {1}, recursively...", new string (' ', indent), assembly.Name);
			resolutionPath.Add (assembly.Name.Name);
			indent += 2;

			// Add this assembly
			if (!topLevel)
				assemblies [assemblyName] = fullPath;

			// Recurse into each referenced assembly
			foreach (AssemblyNameReference reference in assembly.MainModule.AssemblyReferences) {
				AssemblyDefinition reference_assembly;
				try {
					reference_assembly = resolver.Resolve (reference);
				} catch (FileNotFoundException ex) {
					var references = new StringBuilder ();
					for (int i = 0; i < resolutionPath.Count; i++) {
						if (i != 0)
							references.Append (" > ");
						references.Append ('`');
						references.Append (resolutionPath [i]);
						references.Append ('`');
					}

					string missingAssembly = Path.GetFileNameWithoutExtension (ex.FileName);
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

		static LinkModes ParseLinkMode (string linkmode)
		{
			if (string.IsNullOrWhiteSpace (linkmode))
				return LinkModes.SdkOnly;

			LinkModes mode = LinkModes.SdkOnly;

			Enum.TryParse<LinkModes> (linkmode.Trim (), true, out mode);

			return mode;
		}

		void AddI18nAssemblies (DirectoryAssemblyResolver resolver, Dictionary<string, string> assemblies)
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

		void ResolveI18nAssembly (DirectoryAssemblyResolver resolver, string name, Dictionary<string, string> assemblies)
		{
			var assembly = resolver.Resolve (AssemblyNameReference.Parse (name));
			assemblies [name] = Path.GetFullPath (assembly.MainModule.FileName);
		}
	}
}

