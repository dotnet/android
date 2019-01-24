// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Xamarin.Android.Tools;
using ThreadingTasks = System.Threading.Tasks;

namespace Xamarin.Android.Tasks {
	
	//aapt2 link -o resources.apk.bk --manifest Foo.xml --java . --custom-package com.infinitespace_studios.blankforms -R foo2 -v --auto-add-overlay
	public class Aapt2Link : Aapt2 {
		[Required]
		public ITaskItem [] ManifestFiles { get; set; }

		[Required]
		public string JavaPlatformJarPath { get; set; }

		[Required]
		public string ApplicationName { get; set; }

		public string PackageName { get; set; }

		public ITaskItem [] AdditionalResourceArchives { get; set; }

		public ITaskItem [] AdditionalAndroidResourcePaths { get; set; }

		public ITaskItem [] LibraryProjectJars { get; set; }

		public ITaskItem CompiledResourceFlatArchive { get; set; }

		public string AndroidComponentResgenFlagFile { get; set; }

		public string AssetsDirectory { get; set; }

		public string ExtraPackages { get; set; }

		public string ExtraArgs { get; set; }

		public bool CreatePackagePerAbi { get; set; }

		public string SupportedAbis { get; set; }

		public string OutputFile { get; set; }

		public string JavaDesignerOutputDirectory { get; set; }

		public string UncompressedFileExtensions { get; set; }

		public string AndroidSdkPlatform { get; set; }

		public string VersionCodePattern { get; set; }

		public string VersionCodeProperties { get; set; }

		public string AssemblyIdentityMapFile { get; set; }

		public string OutputImportDirectory { get; set; }

		public string ImportsDirectory { get; set; }

		public bool UseShortFileNames { get; set; }

		public bool NonConstantId { get; set; }

		AssemblyIdentityMap assemblyMap = new AssemblyIdentityMap ();
		List<string> tempFiles = new List<string> ();

		public override bool Execute ()
		{
			if (CreatePackagePerAbi)
				Log.LogDebugMessage ("  SupportedAbis: {0}", SupportedAbis);
			
			Yield ();
			try {
				var task = ThreadingTasks.Task.Run (() => {
					DoExecute ();
				}, Token);

				task.ContinueWith (Complete);

				base.Execute ();
			} finally {
				Reacquire ();
			}

			return !Log.HasLoggedErrors;
		}

		void DoExecute ()
		{
			try {
				LoadResourceCaseMap ();

				assemblyMap.Load (Path.Combine (WorkingDirectory, AssemblyIdentityMapFile));

				ThreadingTasks.ParallelOptions options = new ThreadingTasks.ParallelOptions {
					CancellationToken = Token,
					TaskScheduler = ThreadingTasks.TaskScheduler.Default,
				};

				ThreadingTasks.Parallel.ForEach (ManifestFiles, options, ProcessManifest);
			} finally {
				foreach (var temp in tempFiles) {
					File.Delete (temp);
				}
				tempFiles.Clear ();
			}
		}

		string GenerateCommandLineCommands (string ManifestFile, string currentAbi, string currentResourceOutputFile)
		{
			var cmd = new CommandLineBuilder ();
			cmd.AppendSwitch ("link");
			if (MonoAndroidHelper.LogInternalExceptions)
				cmd.AppendSwitch ("-v");

			string manifestDir = Path.Combine (Path.GetDirectoryName (ManifestFile), currentAbi != null ? currentAbi : "manifest");
			Directory.CreateDirectory (manifestDir);
			string manifestFile = Path.Combine (manifestDir, Path.GetFileName (ManifestFile));
			ManifestDocument manifest = new ManifestDocument (ManifestFile, this.Log);
			manifest.SdkVersion = AndroidSdkPlatform;
			if (currentAbi != null) {
				if (!string.IsNullOrEmpty (VersionCodePattern))
					manifest.CalculateVersionCode (currentAbi, VersionCodePattern, VersionCodeProperties);
				else
					manifest.SetAbi (currentAbi);
			} else if (!string.IsNullOrEmpty (VersionCodePattern)) {
				manifest.CalculateVersionCode (null, VersionCodePattern, VersionCodeProperties);
			}
			manifest.ApplicationName = ApplicationName;
			manifest.Save (manifestFile);

			cmd.AppendSwitchIfNotNull ("--manifest ", manifestFile);
			if (!string.IsNullOrEmpty (JavaDesignerOutputDirectory)) {
				var designerDirectory = Path.IsPathRooted (JavaDesignerOutputDirectory) ? JavaDesignerOutputDirectory : Path.Combine (WorkingDirectory, JavaDesignerOutputDirectory);
				Directory.CreateDirectory (designerDirectory);
				cmd.AppendSwitchIfNotNull ("--java ", JavaDesignerOutputDirectory);
			}
			if (PackageName != null)
				cmd.AppendSwitchIfNotNull ("--custom-package ", PackageName.ToLowerInvariant ());
			
			if (AdditionalResourceArchives != null) {
				foreach (var dir in AdditionalResourceArchives) {
					var flatArchive = dir.ItemSpec;
					if (!File.Exists (flatArchive))
						continue;
					cmd.AppendSwitchIfNotNull ("-R ", flatArchive);
				}
			}

			if (CompiledResourceFlatArchive != null && File.Exists (CompiledResourceFlatArchive.ItemSpec))
				cmd.AppendSwitchIfNotNull ("-R ", CompiledResourceFlatArchive.ItemSpec);
			
			cmd.AppendSwitch ("--auto-add-overlay");

			if (!string.IsNullOrWhiteSpace (UncompressedFileExtensions))
				foreach (var ext in UncompressedFileExtensions.Split (new char [] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
					cmd.AppendSwitchIfNotNull ("-0 ", ext);

			if (!string.IsNullOrEmpty (ExtraPackages))
				cmd.AppendSwitchIfNotNull ("--extra-packages ", ExtraPackages);

			cmd.AppendSwitchIfNotNull ("-I ", JavaPlatformJarPath);

			if (!string.IsNullOrEmpty (ResourceSymbolsTextFile))
				cmd.AppendSwitchIfNotNull ("--output-text-symbols ", ResourceSymbolsTextFile);

			var extraArgsExpanded = ExpandString (ExtraArgs);
			if (extraArgsExpanded != ExtraArgs)
				Log.LogDebugMessage ("  ExtraArgs expanded: {0}", extraArgsExpanded);

			if (!string.IsNullOrWhiteSpace (extraArgsExpanded))
				cmd.AppendSwitch (extraArgsExpanded);

			if (!string.IsNullOrWhiteSpace (AssetsDirectory)) {
				var assetDir = AssetsDirectory.TrimEnd ('\\');
				if (!Path.IsPathRooted (assetDir))
					assetDir = Path.Combine (WorkingDirectory, assetDir);
				if (!string.IsNullOrWhiteSpace (assetDir) && Directory.Exists (assetDir))
					cmd.AppendSwitchIfNotNull ("-A ", assetDir);
			}
			cmd.AppendSwitchIfNotNull ("-o ", currentResourceOutputFile);
			return cmd.ToString ();
		}

		string ExpandString (string s)
		{
			if (s == null)
				return null;
			int start = 0;
			int st = s.IndexOf ("${library.imports:", start, StringComparison.Ordinal);
			if (st >= 0) {
				int ed = s.IndexOf ('}', st);
				if (ed < 0)
					return s.Substring (0, st + 1) + ExpandString (s.Substring (st + 1));
				int ast = st + "${library.imports:".Length;
				string aname = s.Substring (ast, ed - ast);
				return s.Substring (0, st) + Path.Combine (OutputImportDirectory, UseShortFileNames ? assemblyMap.GetLibraryImportDirectoryNameForAssembly (aname) : aname, ImportsDirectory) + Path.DirectorySeparatorChar + ExpandString (s.Substring (ed + 1));
			}
			else
				return s;
		}

		bool ExecuteForAbi (string cmd, string currentResourceOutputFile)
		{
			var output = new List<OutputLine> ();
			var ret = RunAapt (cmd, output);
			var success = !string.IsNullOrEmpty (currentResourceOutputFile)
				? File.Exists (Path.Combine (currentResourceOutputFile + ".bk"))
				: ret;
			foreach (var line in output) {
				if (line.StdError) {
					if (!LogAapt2EventsFromOutput (line.Line, MessageImportance.Normal, success))
						break;
				} else {
					LogMessage (line.Line, MessageImportance.Normal);
				}
			}
			if (ret && !string.IsNullOrEmpty (currentResourceOutputFile)) {
				var tmpfile = currentResourceOutputFile + ".bk";
				// aapt2 might not produce an archive and we must provide
				// and -o foo even if we don't want one.
				if (File.Exists (tmpfile)) {
					MonoAndroidHelper.CopyIfZipChanged (tmpfile, currentResourceOutputFile);
					File.Delete (tmpfile);
				}
			}
			return ret;
		}

		bool ManifestIsUpToDate (string manifestFile)
		{
			return !String.IsNullOrEmpty (AndroidComponentResgenFlagFile) &&
				File.Exists (AndroidComponentResgenFlagFile) && File.Exists (manifestFile) &&
				File.GetLastWriteTime (AndroidComponentResgenFlagFile) > File.GetLastWriteTime (manifestFile);
		}

		void ProcessManifest (ITaskItem manifestFile)
		{
			var manifest = Path.IsPathRooted (manifestFile.ItemSpec) ? manifestFile.ItemSpec : Path.Combine (WorkingDirectory, manifestFile.ItemSpec);
			if (!File.Exists (manifest)) {
				LogDebugMessage ("{0} does not exists. Skipping", manifest);
				return;
			}

			bool upToDate = ManifestIsUpToDate (manifest);

			if (AdditionalAndroidResourcePaths != null)
				foreach (var dir in AdditionalAndroidResourcePaths)
					if (!string.IsNullOrEmpty (dir.ItemSpec))
						upToDate = upToDate && ManifestIsUpToDate (string.Format ("{0}{1}{2}{3}{4}", dir, Path.DirectorySeparatorChar, "manifest", Path.DirectorySeparatorChar, "AndroidManifest.xml"));

			if (upToDate) {
				LogMessage ("  Additional Android Resources manifsets files are unchanged. Skipping.");
				return;
			}

			var defaultAbi = new string [] { null };
			var abis = SupportedAbis?.Split (new char [] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			var outputFile = string.IsNullOrEmpty (OutputFile) ? GetTempFile () : OutputFile;
			foreach (var abi in (CreatePackagePerAbi && abis?.Length > 1) ? defaultAbi.Concat (abis) : defaultAbi) {
				var currentResourceOutputFile = abi != null ? string.Format ("{0}-{1}", outputFile, abi) : outputFile;
				if (!string.IsNullOrEmpty (currentResourceOutputFile) && !Path.IsPathRooted (currentResourceOutputFile))
					currentResourceOutputFile = Path.Combine (WorkingDirectory, currentResourceOutputFile);
				if (!ExecuteForAbi (GenerateCommandLineCommands (manifest, abi, currentResourceOutputFile), currentResourceOutputFile)) {
					Cancel ();
				}
			}
		}

		string GetTempFile ()
		{
			var temp = Path.GetTempFileName ();
			tempFiles.Add (temp);
			return temp;
		}
	}

}