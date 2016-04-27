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

using Java.Interop.Tools.Cecil;

namespace Xamarin.Android.Tasks
{
	public class ResolveAssemblies : Task
	{
		// The user's assemblies to package
		[Required]
		public ITaskItem[] Assemblies { get; set; }

		[Required]
		public string ReferenceAssembliesDirectory { get; set; }

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

		DirectoryAssemblyResolver     resolver;

		public override bool Execute ()
		{
			Log.LogDebugMessage ("ResolveAssemblies Task");
			Log.LogDebugMessage ("  ReferenceAssembliesDirectory: {0}", ReferenceAssembliesDirectory);
			Log.LogDebugMessage ("  I18nAssemblies: {0}", I18nAssemblies);
			Log.LogDebugMessage ("  LinkMode: {0}", LinkMode);
			Log.LogDebugTaskItems ("  Assemblies:", Assemblies);

			resolver  = new DirectoryAssemblyResolver (Log.LogWarning, loadDebugSymbols: false);
			foreach (var dir in ReferenceAssembliesDirectory.Split (new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
				resolver.SearchDirectories.Add (dir);

			var assemblies = new HashSet<string> ();

			var topAssemblyReferences = new List<AssemblyDefinition> ();

			try {
				foreach (var assembly in Assemblies) {
					var assembly_path = Path.GetDirectoryName (assembly.ItemSpec);

					if (!resolver.SearchDirectories.Contains (assembly_path))
						resolver.SearchDirectories.Add (assembly_path);

					// Add each user assembly and all referenced assemblies (recursive)
					var assemblyDef = resolver.Load (assembly.ItemSpec);
					if (assemblyDef == null)
						throw new InvalidOperationException ("Failed to load assembly " + assembly.ItemSpec);
					topAssemblyReferences.Add (assemblyDef);
					assemblies.Add (assemblyDef.MainModule.FullyQualifiedName);
				}
			} catch (Exception ex) {
				Log.LogError ("Exception while loading assemblies: {0}", ex);
				return false;
			}
			try {
				foreach (var assembly in topAssemblyReferences)
					AddAssemblyReferences (assemblies, assembly, true);
			} catch (Exception ex) {
				Log.LogError ("Exception while loading assemblies: {0}", ex);
				return false;
			}

			// Add I18N assemblies if needed
			AddI18nAssemblies (assemblies);

			ResolvedAssemblies = assemblies.Select (a => new TaskItem (a)).ToArray ();
			ResolvedSymbols = assemblies.Select (a => a + ".mdb").Where (a => File.Exists (a)).Select (a => new TaskItem (a)).ToArray ();
			ResolvedFrameworkAssemblies = ResolvedAssemblies.Where (p => MonoAndroidHelper.IsFrameworkAssembly (p.ItemSpec, true)).ToArray ();
			ResolvedUserAssemblies = ResolvedAssemblies.Where (p => !MonoAndroidHelper.IsFrameworkAssembly (p.ItemSpec, true)).ToArray ();
			ResolvedDoNotPackageAttributes = do_not_package_atts.ToArray ();

			Log.LogDebugTaskItems ("  [Output] ResolvedAssemblies:", ResolvedAssemblies);
			Log.LogDebugTaskItems ("  [Output] ResolvedUserAssemblies:", ResolvedUserAssemblies);
			Log.LogDebugTaskItems ("  [Output] ResolvedFrameworkAssemblies:", ResolvedFrameworkAssemblies);
			Log.LogDebugTaskItems ("  [Output] ResolvedDoNotPackageAttributes:", ResolvedDoNotPackageAttributes);
			
			return true;
		}

		readonly List<string> do_not_package_atts = new List<string> ();
		int indent = 2;

		private void AddAssemblyReferences (ICollection<string> assemblies, AssemblyDefinition assembly, bool topLevel)
		{
			var fqname = assembly.MainModule.FullyQualifiedName;

			// Don't repeat assemblies we've already done
			if (!topLevel && assemblies.Contains (fqname))
				return;
			
			foreach (var att in assembly.CustomAttributes.Where (a => a.AttributeType.FullName == "Java.Interop.DoNotPackageAttribute")) {
				string file = (string) att.ConstructorArguments.First ().Value;
				if (string.IsNullOrWhiteSpace (file))
					Log.LogError ("In referenced assembly {0}, Java.Interop.DoNotPackageAttribute requires non-null file name.", assembly.FullName);
				do_not_package_atts.Add (Path.GetFileName (file));
			}

			Log.LogMessage (MessageImportance.Low, "{0}Adding assembly reference for {1}, recursively...", new string (' ', indent), assembly.Name);
			indent += 2;
			// Add this assembly
			if (!topLevel && assemblies.All (a => new AssemblyNameDefinition (a, null).Name != assembly.Name.Name))
				assemblies.Add (fqname);

			// Recurse into each referenced assembly
			foreach (AssemblyNameReference reference in assembly.MainModule.AssemblyReferences) {
				var reference_assembly = resolver.Resolve (reference);
				AddAssemblyReferences (assemblies, reference_assembly, false);
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

		private void AddI18nAssemblies (ICollection<string> assemblies)
		{
			var i18n = Linker.ParseI18nAssemblies (I18nAssemblies);
			var link = ParseLinkMode (LinkMode);

			// Check if we should add any I18N assemblies
			if (i18n == Mono.Linker.I18nAssemblies.None)
				return;

			assemblies.Add (ResolveI18nAssembly ("I18N"));
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.CJK))
				assemblies.Add (ResolveI18nAssembly ("I18N.CJK"));
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.MidEast))
				assemblies.Add (ResolveI18nAssembly ("I18N.MidEast"));
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.Other))
				assemblies.Add (ResolveI18nAssembly ("I18N.Other"));
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.Rare))
				assemblies.Add (ResolveI18nAssembly ("I18N.Rare"));
	
			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.West))
				assemblies.Add (ResolveI18nAssembly ("I18N.West"));
		}

		private string ResolveI18nAssembly (string name)
		{
			var assembly = resolver.Resolve (AssemblyNameReference.Parse (name));
			return assembly.MainModule.FullyQualifiedName;
		}
	}
}

