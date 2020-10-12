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

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task is for Release builds, LinkMode=None now uses LinkAssembliesNoShrink
	/// </summary>
	public class LinkAssemblies : AndroidTask, ML.ILogger
	{
		public override string TaskPrefix => "LNK";

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

		public string HttpClientHandlerType { get; set; }

		public string TlsProvider { get; set; }

		public bool PreserveJniMarshalMethods { get; set; }

		public bool Deterministic { get; set; }

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

		public override bool RunTask ()
		{
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
			options.LinkNone = false;
			options.Resolver = resolver;
			options.LinkDescriptions = LinkDescriptions.Select (item => Path.GetFullPath (item.ItemSpec)).ToArray ();
			options.I18nAssemblies = Linker.ParseI18nAssemblies (I18nAssemblies);
			if (!options.LinkSdkOnly)
				options.RetainAssemblies = GetRetainAssemblies (res);
			options.DumpDependencies = DumpDependencies;
			options.HttpClientHandlerType = HttpClientHandlerType;
			options.TlsProvider = TlsProvider;
			options.PreserveJniMarshalMethods = PreserveJniMarshalMethods;
			options.DeterministicOutput = Deterministic;

			var skiplist = new List<string> ();

			// Add LinkSkip options
			if (!string.IsNullOrWhiteSpace (LinkSkip))
				skiplist.AddRange (LinkSkip.Split (',', ';'));

			options.SkippedAssemblies = skiplist;

			if (EnableProguard)
				options.ProguardConfiguration = ProguardConfiguration;

			// Link!
			try {
				LinkContext link_context;
				Linker.Process (options, this, out link_context);

				foreach (var assembly in ResolvedAssemblies) {
					var copysrc = assembly.ItemSpec;
					var filename = Path.GetFileName (assembly.ItemSpec);
					var assemblyDestination = Path.Combine (OutputDirectory, filename);

					if (!MonoAndroidHelper.IsForceRetainedAssembly (filename))
						continue;

					MonoAndroidHelper.CopyAssemblyAndSymbols (copysrc, assemblyDestination);
				}
			} catch (ResolutionException ex) {
				Diagnostic.Error (2006, ex, Properties.Resources.XA2006, ex.Member, ex.Member.Module.Assembly, ex.Scope);
			}

			return true;
		}

		public void LogMessage (ML.MessageImportance importance, string message, params object [] values)
		{
			var mbfImportance = MBF.MessageImportance.Low;

			if (importance == ML.MessageImportance.High)
				mbfImportance = MBF.MessageImportance.High;

			Log.LogMessageFromText (string.Format (message, values), mbfImportance);
		}
	}
}

