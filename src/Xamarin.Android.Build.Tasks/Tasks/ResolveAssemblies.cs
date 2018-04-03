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
			LogDebugMessage ("ResolveAssemblies Task");
			LogDebugMessage ("  ReferenceAssembliesDirectory: {0}", ReferenceAssembliesDirectory);
			LogDebugMessage ("  I18nAssemblies: {0}", I18nAssemblies);
			LogDebugMessage ("  LinkMode: {0}", LinkMode);
			LogDebugTaskItems ("  Assemblies:", Assemblies);
			LogDebugMessage ("  ProjectAssetFile: {0}", ProjectAssetFile);
			LogDebugMessage ("  TargetMoniker: {0}", TargetMoniker);

			foreach (var dir in ReferenceAssembliesDirectory.Split (new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
				resolver.SearchDirectories.Add (dir);

			var assemblies = new HashSet<string> ();

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
							LogWarning ($"Ignoring {asmFullPath} as it is a Reference Assembly");
							continue;
						}
					}
					topAssemblyReferences.Add (assemblyDef);
					assemblies.Add (Path.GetFullPath (assemblyDef.MainModule.FullyQualifiedName));
				}
			} catch (Exception ex) {
				LogError ("Exception while loading assemblies: {0}", ex);
				return;
			}
			try {
				foreach (var assembly in topAssemblyReferences)
					AddAssemblyReferences (resolver, assemblies, assembly, true);
			} catch (Exception ex) {
				LogError ("Exception while loading assemblies: {0}", ex);
				return;
			}

			// Add I18N assemblies if needed
			AddI18nAssemblies (resolver, assemblies);

			ResolvedAssemblies = assemblies.Select (a => new TaskItem (a)).ToArray ();
			ResolvedSymbols = assemblies.Select (a => a + ".mdb").Where (a => File.Exists (a)).Select (a => new TaskItem (a)).ToArray ();
			ResolvedSymbols = ResolvedSymbols.Concat (
					assemblies.Select (a => Path.ChangeExtension (a, "pdb"))
					.Where (a => File.Exists (a) && Files.IsPortablePdb (a))
					.Select (a => new TaskItem (a)))
				.ToArray ();
			ResolvedFrameworkAssemblies = ResolvedAssemblies.Where (p => MonoAndroidHelper.IsFrameworkAssembly (p.ItemSpec, true)).ToArray ();
			ResolvedUserAssemblies = ResolvedAssemblies.Where (p => !MonoAndroidHelper.IsFrameworkAssembly (p.ItemSpec, true)).ToArray ();
			ResolvedDoNotPackageAttributes = do_not_package_atts.ToArray ();

			LogDebugTaskItems ("  [Output] ResolvedAssemblies:", ResolvedAssemblies);
			LogDebugTaskItems ("  [Output] ResolvedUserAssemblies:", ResolvedUserAssemblies);
			LogDebugTaskItems ("  [Output] ResolvedFrameworkAssemblies:", ResolvedFrameworkAssemblies);
			LogDebugTaskItems ("  [Output] ResolvedDoNotPackageAttributes:", ResolvedDoNotPackageAttributes);
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

		void AddAssemblyReferences (DirectoryAssemblyResolver resolver, ICollection<string> assemblies, AssemblyDefinition assembly, bool topLevel)
		{
			var fqname = assembly.MainModule.FullyQualifiedName;
			var fullPath = Path.GetFullPath (fqname);

			// Don't repeat assemblies we've already done
			if (!topLevel && assemblies.Contains (fullPath))
				return;
			
			foreach (var att in assembly.CustomAttributes.Where (a => a.AttributeType.FullName == "Java.Interop.DoNotPackageAttribute")) {
				string file = (string) att.ConstructorArguments.First ().Value;
				if (string.IsNullOrWhiteSpace (file))
					LogError ("In referenced assembly {0}, Java.Interop.DoNotPackageAttribute requires non-null file name.", assembly.FullName);
				do_not_package_atts.Add (Path.GetFileName (file));
			}

			LogMessage ("{0}Adding assembly reference for {1}, recursively...", new string (' ', indent), assembly.Name);
			indent += 2;
			// Add this assembly
			if (!topLevel && assemblies.All (a => new AssemblyNameDefinition (a, null).Name != assembly.Name.Name))
				assemblies.Add (fullPath);

			// Recurse into each referenced assembly
			foreach (AssemblyNameReference reference in assembly.MainModule.AssemblyReferences) {
				var reference_assembly = resolver.Resolve (reference);
				AddAssemblyReferences (resolver, assemblies, reference_assembly, false);
			}
			indent -= 2;
		}

		static LinkModes ParseLinkMode (string linkmode)
		{
			if (string.IsNullOrWhiteSpace (linkmode))
				return LinkModes.SdkOnly;

			LinkModes mode = LinkModes.SdkOnly;

			Enum.TryParse<LinkModes> (linkmode.Trim (), true, out mode);

			return mode;
		}

		void AddI18nAssemblies (DirectoryAssemblyResolver resolver, ICollection<string> assemblies)
		{
			var i18n = Linker.ParseI18nAssemblies (I18nAssemblies);
			var link = ParseLinkMode (LinkMode);

			// Check if we should add any I18N assemblies
			if (i18n == Mono.Linker.I18nAssemblies.None)
				return;

			assemblies.Add (ResolveI18nAssembly (resolver, "I18N"));
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.CJK))
				assemblies.Add (ResolveI18nAssembly (resolver, "I18N.CJK"));
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.MidEast))
				assemblies.Add (ResolveI18nAssembly (resolver, "I18N.MidEast"));
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.Other))
				assemblies.Add (ResolveI18nAssembly (resolver, "I18N.Other"));
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.Rare))
				assemblies.Add (ResolveI18nAssembly (resolver, "I18N.Rare"));
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.West))
				assemblies.Add (ResolveI18nAssembly (resolver, "I18N.West"));
		}

		string ResolveI18nAssembly (DirectoryAssemblyResolver resolver, string name)
		{
			var assembly = resolver.Resolve (AssemblyNameReference.Parse (name));
			return Path.GetFullPath (assembly.MainModule.FullyQualifiedName);
		}
	}
}

