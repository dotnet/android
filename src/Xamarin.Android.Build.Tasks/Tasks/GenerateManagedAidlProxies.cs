// Copyright (C) 2011 Xamarin, Inc. All rights reserved.
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Monodroid;
using Xamarin.Android.Tools.Aidl;

namespace Xamarin.Android.Tasks
{
	public class GenerateManagedAidlProxies : Task
	{
		[Required]
		public ITaskItem[] References { get; set; }

		[Required]
		public ITaskItem[] SourceAidlFiles { get; set; }
		
		[Required]
		public string IntermediateOutputDirectory { get; set; }
		
		public string OutputNamespace { get; set; }

		public string ParcelableHandlingOption { get; set; }

		public GenerateManagedAidlProxies ()
		{
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("Task GenerateManagedAidlProxies");
			Log.LogDebugTaskItems ("  References:", References);
			Log.LogDebugTaskItems ("  SourceAidlFiles:", SourceAidlFiles);
			Log.LogDebugMessage ("  IntermediateOutputDirectory: {0}", IntermediateOutputDirectory);
			Log.LogDebugMessage ("  OutputNamespace: {0}", OutputNamespace);
			Log.LogDebugMessage ("  ParcelableHandlingOption: {0}", ParcelableHandlingOption);

			if (SourceAidlFiles.Length == 0) // nothing to do
				return true;

			var opts = new ConverterOptions () {
				Verbose = true,
				ParcelableHandling = ParcelableHandling.Ignore,
			};

			if (!string.IsNullOrEmpty (ParcelableHandlingOption))
				opts.ParcelableHandling = ToParcelableHandling (ParcelableHandlingOption);

			if (!string.IsNullOrEmpty (OutputNamespace))
				opts.OutputNS = OutputNamespace;

			foreach (var file in References)
				opts.AddReference (file.ItemSpec);
			foreach (var file in SourceAidlFiles)
				opts.AddFile (file.ItemSpec);

			var tool = new AidlCompiler ();
			using (var fsw = TextWriter.Null/*File.AppendText (Path.Combine (IntermediateOutputDirectory, "AidlFilesWrittenAbsolute.txt"))*/) {
				tool.FileWritten += (file, source) => Log.LogDebugMessage ("Written ... {0}", file);
				string outPath = Path.Combine (IntermediateOutputDirectory, "aidl");
				var ret = tool.Run (opts, assemblyFile => AssemblyDefinition.ReadAssembly (assemblyFile), (dir, file) => {
					var dst = Path.GetFullPath (Path.Combine (outPath, Path.ChangeExtension (file, ".cs")));
					if (!dst.StartsWith (outPath))
						dst = Path.Combine (outPath, Path.ChangeExtension (Path.GetFileName (file), ".cs"));
					string dstdir = Path.GetDirectoryName (dst);
					if (!Directory.Exists (dstdir))
						Directory.CreateDirectory (dstdir);
					fsw.WriteLine (dst);
					fsw.Flush ();
					return dst;
				});
				if (ret.LogMessages.Count > 0) {
					foreach (var p in ret.LogMessages)
						Log.LogError ("{0} {1}: {2}", Path.GetFullPath (p.Key), p.Value.Location, p.Value.ToString ());
				}
			}
			return true;
		}
		
		static ParcelableHandling ToParcelableHandling (string option)
		{
			switch (option) {
			case "ignore": return ParcelableHandling.Ignore;
			case "error": return ParcelableHandling.Error;
			case "stub": return ParcelableHandling.Stub;
			}
			throw new ArgumentException ("Invalid parcelable option: " + option);
		}
	}
	
	static class Extensions
	{
		internal static void AddFile (this ConverterOptions opts, string name)
		{
			if (File.Exists (name))
				opts.InputFiles.Add (name);
			else
				throw new InvalidOperationException (String.Format ("File {0} not exist", name));
		}
		
		internal static void AddReference (this ConverterOptions opts, string name)
		{
			if (File.Exists (name))
				opts.References.Add (name);
			else
				throw new InvalidOperationException (String.Format ("File {0} not exist", name));
		}
	}
}

