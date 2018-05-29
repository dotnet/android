// Copyright (C) 2011, Xamarin Inc.
// Copyright (C) 2010, Novell Inc.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using MBF = Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using System.IO;
using MonoDroid.Tuner;
using Mono.Linker;
using ML = Mono.Linker;
using Xamarin.Android.Tools;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;

namespace Xamarin.Android.Tasks
{
	public class LinkAssemblies : Task, ML.ILogger
	{
		[Required]
		public string UseSharedRuntime { get; set; }

		[Required]
		public string MainAssembly { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		[Required]
		public ITaskItem[] LinkDescriptions { get; set; }

		public string I18nAssemblies { get; set; }
		public string LinkMode { get; set; }
		public string LinkSkip { get; set; }

		public bool EnableProguard { get; set; }
		public string ProguardConfiguration { get; set; }
		public bool DumpDependencies { get; set; }

		public string OptionalDestinationDirectory { get; set; }
		public string LinkOnlyNewerThan { get; set; }

		public string HttpClientHandlerType { get; set; }

		public string TlsProvider { get; set; }

		IEnumerable<AssemblyDefinition> GetRetainAssemblies (DirectoryAssemblyResolver res)
		{
			List<AssemblyDefinition> retainList = null;
			foreach (var assembly in ResolvedAssemblies) {
				var filename = Path.GetFileName (assembly.ItemSpec);
				if (!MonoAndroidHelper.IsForceRetainedAssembly (filename))
					continue;
				if (retainList == null)
					retainList = new List<AssemblyDefinition> ();
				retainList.Add (res.GetAssembly (assembly.ItemSpec));
			}
			return retainList;
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("LinkAssemblies Task");
			Log.LogDebugMessage ("  UseSharedRuntime: {0}", UseSharedRuntime);
			Log.LogDebugMessage ("  MainAssembly: {0}", MainAssembly);
			Log.LogDebugMessage ("  OutputDirectory: {0}", OutputDirectory);
			Log.LogDebugMessage ("  OptionalDestinationDirectory: {0}", OptionalDestinationDirectory);
			Log.LogDebugMessage ("  I18nAssemblies: {0}", I18nAssemblies);
			Log.LogDebugMessage ("  LinkMode: {0}", LinkMode);
			Log.LogDebugMessage ("  LinkSkip: {0}", LinkSkip);
			Log.LogDebugTaskItems ("  LinkDescriptions:", LinkDescriptions);
			Log.LogDebugTaskItems ("  ResolvedAssemblies:", ResolvedAssemblies);
			Log.LogDebugMessage ("  EnableProguard: {0}", EnableProguard);
			Log.LogDebugMessage ("  ProguardConfiguration: {0}", ProguardConfiguration);
			Log.LogDebugMessage ("  DumpDependencies: {0}", DumpDependencies);
			Log.LogDebugMessage ("  LinkOnlyNewerThan: {0}", LinkOnlyNewerThan);
			Log.LogDebugMessage ("  HttpClientHandlerType: {0}", HttpClientHandlerType);
			Log.LogDebugMessage ("  TlsProvider: {0}", TlsProvider);

			var rp = new ReaderParameters {
				InMemory    = true,
			};
			using (var res = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: false, loadReaderParameters: rp)) {
				return Execute (res);
			}
		}

		bool Execute (DirectoryAssemblyResolver res)
		{
			// Put every assembly we'll need in the resolver
			foreach (var assembly in ResolvedAssemblies) {
				res.Load (Path.GetFullPath (assembly.ItemSpec));
			}

			var resolver = new AssemblyResolver (res.ToResolverCache ());

			// Set up for linking
			var options = new LinkerOptions ();
			options.MainAssembly = res.GetAssembly (MainAssembly);
			options.OutputDirectory = Path.GetFullPath (OutputDirectory);
			options.LinkSdkOnly = string.Compare (LinkMode, "SdkOnly", true) == 0;
			options.LinkNone = string.Compare (LinkMode, "None", true) == 0;
			options.Resolver = resolver;
			options.LinkDescriptions = LinkDescriptions.Select (item => Path.GetFullPath (item.ItemSpec)).ToArray ();
			options.I18nAssemblies = Linker.ParseI18nAssemblies (I18nAssemblies);
			if (!options.LinkSdkOnly)
				options.RetainAssemblies = GetRetainAssemblies (res);
			options.DumpDependencies = DumpDependencies;
			options.HttpClientHandlerType = HttpClientHandlerType;
			options.TlsProvider = TlsProvider;
			
			var skiplist = new List<string> ();

			if (string.Compare (UseSharedRuntime, "true", true) == 0)
				skiplist.AddRange (Profile.SharedRuntimeAssemblies.Where (a => a.EndsWith (".dll")).Select (a => Path.GetFileNameWithoutExtension (a)));
			if (!string.IsNullOrWhiteSpace (LinkOnlyNewerThan) && File.Exists (LinkOnlyNewerThan)) {
				var newerThan = File.GetLastWriteTime (LinkOnlyNewerThan);
				var skipOldOnes = ResolvedAssemblies.Where (a => File.GetLastWriteTime (a.ItemSpec) < newerThan);
				foreach (var old in skipOldOnes)
					Log.LogMessage (MBF.MessageImportance.Low, "  Skip linking unchanged file: " + old.ItemSpec);
				skiplist = skipOldOnes.Select (a => Path.GetFileNameWithoutExtension (a.ItemSpec)).Concat (skiplist).ToList ();
			}

			// Add LinkSkip options
			if (!string.IsNullOrWhiteSpace (LinkSkip))
				foreach (var assembly in LinkSkip.Split (',', ';'))
					skiplist.Add (assembly);

			options.SkippedAssemblies = skiplist;

			if (EnableProguard)
				options.ProguardConfiguration = ProguardConfiguration;

			// Link!
			try {
				LinkContext link_context;
				Linker.Process (options, this, out link_context);

				var copydst = OptionalDestinationDirectory ?? OutputDirectory;

				foreach (var assembly in ResolvedAssemblies) {
					var copysrc = assembly.ItemSpec;
					var filename = Path.GetFileName (assembly.ItemSpec);

					if (options.LinkNone) {
						if (skiplist.Any (s => Path.GetFileNameWithoutExtension (filename) == s)) {
							// For skipped assemblies, skip if there is existing file in the destination.
							// We cannot just copy the linker output from *current* run output, because
							// it always renew the assemblies, in *different* binary values, whereas
							// the dll in the OptionalDestinationDirectory must retain old and unchanged.
							if (File.Exists (Path.Combine (copydst, filename)))
								continue;
							copysrc = assembly.ItemSpec;
						} else {
							// Prefer fixup assemblies if exists, otherwise just copy the original.
							copysrc = Path.Combine (OutputDirectory, filename);
							copysrc = File.Exists (copysrc) ? copysrc : assembly.ItemSpec;
						}
					}
					else if (!MonoAndroidHelper.IsForceRetainedAssembly (filename))
						continue;

					var assemblyDestination = Path.Combine (copydst, filename);
					if (MonoAndroidHelper.CopyIfChanged (copysrc, assemblyDestination)) {
						MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (assemblyDestination, DateTime.UtcNow, Log);
					}
					try {
						var mdbDestination = assemblyDestination + ".mdb";
						if (MonoAndroidHelper.CopyIfChanged (assembly.ItemSpec + ".mdb", mdbDestination)) {
							MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (mdbDestination, DateTime.UtcNow, Log);
						}
					} catch (Exception) { // skip it, mdb sometimes fails to read and it's optional
					}
					var pdb = Path.ChangeExtension (copysrc, "pdb");
					if (File.Exists (pdb) && Files.IsPortablePdb (pdb)) {
						var pdbDestination = Path.ChangeExtension (Path.Combine (copydst, filename), "pdb");
						if (MonoAndroidHelper.CopyIfChanged (pdb, pdbDestination)) {
							MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (pdbDestination, DateTime.UtcNow, Log);
						}
					}
				}
			} catch (ResolutionException ex) {
				Diagnostic.Error (2006, ex, "Could not resolve reference to '{0}' (defined in assembly '{1}') with scope '{2}'. When the scope is different from the defining assembly, it usually means that the type is forwarded.", ex.Member, ex.Member.Module.Assembly, ex.Scope);
			}

			return true;
		}

		public void LogMessage (ML.MessageImportance importance, string message, params object [] values)
		{
			var mbfImportance = MBF.MessageImportance.Low;

			if (importance == ML.MessageImportance.High)
				mbfImportance = MBF.MessageImportance.High;

			Log.LogMessage (mbfImportance, message, values);
		}
	}
}

