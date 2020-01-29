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

namespace Xamarin.Android.Tasks {
	
	//aapt2 link -o resources.apk.bk --manifest Foo.xml --java . --custom-package com.infinitespace_studios.blankforms -R foo2 -v --auto-add-overlay
	public class Aapt2Link : Aapt2 {
		static Regex exraArgSplitRegEx = new Regex (@"[\""].+?[\""]|[\''].+?[\'']|[^ ]+", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
		public override string TaskPrefix => "A2L";

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

		public ITaskItem [] CompiledResourceFlatFiles { get; set; }

		public string AndroidComponentResgenFlagFile { get; set; }

		public string AssetsDirectory { get; set; }

		public string ExtraPackages { get; set; }

		public string ExtraArgs { get; set; }

		public bool CreatePackagePerAbi { get; set; }

		public string [] SupportedAbis { get; set; }

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

		public bool ProtobufFormat { get; set; }

		public string ProguardRuleOutput { get; set; }

		AssemblyIdentityMap assemblyMap = new AssemblyIdentityMap ();
		List<string> tempFiles = new List<string> ();
		Dictionary<string, long> apks = new Dictionary<string, long> ();
		string proguardRuleOutputTemp;

		protected override int GetRequiredDaemonInstances ()
		{
			return Math.Min (CreatePackagePerAbi ? (SupportedAbis?.Length ?? 1) : 1, DaemonMaxInstanceCount);
		}

		public async override System.Threading.Tasks.Task RunTaskAsync ()
		{
			try {
				LoadResourceCaseMap ();

				assemblyMap.Load (Path.Combine (WorkingDirectory, AssemblyIdentityMapFile));

				proguardRuleOutputTemp = GetTempFile ();

				await this.WhenAll (ManifestFiles, ProcessManifest);

				ProcessOutput ();
				// now check for
				foreach (var kvp in apks) {
					string currentResourceOutputFile = kvp.Key;
					bool aaptResult = Daemon.JobSucceded (kvp.Value);
					LogDebugMessage ($"Processing {currentResourceOutputFile} JobId: {kvp.Value} Exists: {File.Exists (currentResourceOutputFile)} JobWorked: {aaptResult}");
					if (!string.IsNullOrEmpty (currentResourceOutputFile)) {
						var tmpfile = currentResourceOutputFile + ".bk";
						// aapt2 might not produce an archive and we must provide
						// and -o foo even if we don't want one.
						if (File.Exists (tmpfile)) {
							if (aaptResult) {
								LogDebugMessage ($"Copying {tmpfile} to {currentResourceOutputFile}");
								MonoAndroidHelper.CopyIfZipChanged (tmpfile, currentResourceOutputFile);
							}
							File.Delete (tmpfile);
						}
						// Delete the archive on failure
						if (!aaptResult && File.Exists (currentResourceOutputFile)) {
							LogDebugMessage ($"Link did not succeed. Deleting {currentResourceOutputFile}");
							File.Delete (currentResourceOutputFile);
						}
					}
				}
				if (!string.IsNullOrEmpty (ProguardRuleOutput))
					MonoAndroidHelper.CopyIfChanged (proguardRuleOutputTemp, ProguardRuleOutput);
			} finally {
				lock (tempFiles) {
					foreach (var temp in tempFiles) {
						File.Delete (temp);
					}
					tempFiles.Clear ();
				}
			}
		}

		string [] GenerateCommandLineCommands (string ManifestFile, string currentAbi, string currentResourceOutputFile)
		{
			List<string> cmd = new List<string> ();
			string manifestDir = Path.Combine (Path.GetDirectoryName (ManifestFile), currentAbi != null ? currentAbi : "manifest");
			Directory.CreateDirectory (manifestDir);
			string manifestFile = Path.Combine (manifestDir, Path.GetFileName (ManifestFile));
			ManifestDocument manifest = new ManifestDocument (ManifestFile);
			manifest.SdkVersion = AndroidSdkPlatform;
			if (!string.IsNullOrEmpty (VersionCodePattern)) {
				try {
					manifest.CalculateVersionCode (currentAbi, VersionCodePattern, VersionCodeProperties);
				} catch (ArgumentOutOfRangeException ex) {
					LogCodedError ("XA0003", ManifestFile, 0, ex.Message);
					return cmd.ToArray ();
				}
			}
			if (currentAbi != null && string.IsNullOrEmpty (VersionCodePattern)) {
				manifest.SetAbi (currentAbi);
			}
			if (!manifest.ValidateVersionCode (out string error, out string errorCode)) {
				LogCodedError (errorCode, ManifestFile, 0, error);
				return cmd.ToArray ();
			}
			manifest.ApplicationName = ApplicationName;
			manifest.Save (LogCodedWarning, manifestFile);

			cmd.Add ("link");
			if (MonoAndroidHelper.LogInternalExceptions)
				cmd.Add ("-v");
			cmd.Add ($"--manifest");
			cmd.Add (GetFullPath (manifestFile));
			if (!string.IsNullOrEmpty (JavaDesignerOutputDirectory)) {
				var designerDirectory = Path.IsPathRooted (JavaDesignerOutputDirectory) ? JavaDesignerOutputDirectory : Path.Combine (WorkingDirectory, JavaDesignerOutputDirectory);
				Directory.CreateDirectory (designerDirectory);
				cmd.Add ("--java");
				cmd.Add (GetFullPath (JavaDesignerOutputDirectory));
			}
			if (PackageName != null) {
				cmd.Add ("--custom-package");
				cmd.Add (PackageName.ToLowerInvariant ());
			}
			
			if (AdditionalResourceArchives != null) {
				for (int i = AdditionalResourceArchives.Length - 1; i >= 0; i--) {
					var flata = Path.Combine (WorkingDirectory, AdditionalResourceArchives [i].ItemSpec);
					if (Directory.Exists (flata)) {
						foreach (var line in Directory.EnumerateFiles (flata, "*.flat", SearchOption.TopDirectoryOnly)) {
							cmd.Add ("-R");
							cmd.Add (GetFullPath (line));
						}
					} else if (File.Exists (flata)) {
						cmd.Add ("-R");
						cmd.Add (GetFullPath (flata));
					} else {
						LogDebugMessage ("Archive does not exist: " + flata);
					}
				}
			}

			if (CompiledResourceFlatArchive != null) {
				var flata = Path.Combine (WorkingDirectory, CompiledResourceFlatArchive.ItemSpec);
				if (Directory.Exists (flata)) {
					foreach (var line in Directory.EnumerateFiles (flata, "*.flat", SearchOption.TopDirectoryOnly)) {
						cmd.Add ("-R");
						cmd.Add (GetFullPath (line));
					}
				} else if (File.Exists (flata)) {
						cmd.Add ("-R");
						cmd.Add (GetFullPath (flata));
				} else {
					LogDebugMessage ("Archive does not exist: " + flata);
				}
			}

			if (CompiledResourceFlatFiles != null) {
				List<ITaskItem> appFiles = new List<ITaskItem> ();
				for (int i = CompiledResourceFlatFiles.Length - 1; i >= 0; i--) {
					var file = CompiledResourceFlatFiles [i];
					if (!string.IsNullOrEmpty (file.GetMetadata ("ResourceDirectory")) && File.Exists (file.ItemSpec)) {
						cmd.Add ("-R");
						cmd.Add (GetFullPath (file.ItemSpec));
					} else {
						appFiles.Add(file);
					}
				}
				foreach (var file in appFiles) {
					if (File.Exists (file.ItemSpec)) {
						cmd.Add ("-R");
						cmd.Add (GetFullPath (file.ItemSpec));
					}
				}
			}
			
			cmd.Add ("--auto-add-overlay");

			if (!string.IsNullOrWhiteSpace (UncompressedFileExtensions))
				foreach (var ext in UncompressedFileExtensions.Split (new char [] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)) {
					cmd.Add ("-0");
					cmd.Add (ext.StartsWith (".", StringComparison.OrdinalIgnoreCase) ? ext : $".{ext}");
				}

			if (!string.IsNullOrEmpty (ExtraPackages)) {
				cmd.Add ("--extra-packages");
				cmd.Add (ExtraPackages);
			}

			cmd.Add ("-I");
			cmd.Add (GetFullPath (JavaPlatformJarPath));

			if (!string.IsNullOrEmpty (ResourceSymbolsTextFile)) {
				cmd.Add ("--output-text-symbols");
				cmd.Add (GetFullPath (ResourceSymbolsTextFile));
			}

			if (ProtobufFormat)
				cmd.Add ("--proto-format");

			var extraArgsExpanded = ExpandString (ExtraArgs);
			if (extraArgsExpanded != ExtraArgs)
				LogDebugMessage ("  ExtraArgs expanded: {0}", extraArgsExpanded);

			if (!string.IsNullOrWhiteSpace (extraArgsExpanded)) {
				foreach (Match match in exraArgSplitRegEx.Matches (extraArgsExpanded)) {
					string value = match.Value.Trim (' ', '"', '\'');
					if (!string.IsNullOrEmpty (value))
						cmd.Add (value);
				}
			}

			if (!string.IsNullOrWhiteSpace (AssetsDirectory)) {
				var assetDir = AssetsDirectory.TrimEnd ('\\');
				if (!Path.IsPathRooted (assetDir))
					assetDir = Path.Combine (WorkingDirectory, assetDir);
				if (!string.IsNullOrWhiteSpace (assetDir) && Directory.Exists (assetDir)) {
					cmd.Add ("-A");
					cmd.Add (GetFullPath (assetDir));
				}
			}
			if (!string.IsNullOrEmpty (ProguardRuleOutput)) {
				cmd.Add ("--proguard");
				cmd.Add (GetFullPath (proguardRuleOutputTemp));
			}
			cmd.Add ("-o");
			cmd.Add (GetFullPath (currentResourceOutputFile));

			return cmd.ToArray ();
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

		bool ExecuteForAbi (string [] cmd, string currentResourceOutputFile)
		{
			apks.Add (currentResourceOutputFile, RunAapt (cmd, currentResourceOutputFile));
			return true;
		}

		bool ManifestIsUpToDate (string manifestFile)
		{
			return !String.IsNullOrEmpty (AndroidComponentResgenFlagFile) &&
				File.Exists (AndroidComponentResgenFlagFile) && File.Exists (manifestFile) &&
				File.GetLastWriteTime (AndroidComponentResgenFlagFile) > File.GetLastWriteTime (manifestFile);
		}

		void ProcessManifest (ITaskItem manifestFile)
		{
			var manifest = GetFullPath (manifestFile.ItemSpec);
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
			var abis = CreatePackagePerAbi && SupportedAbis?.Length > 1 ? defaultAbi.Concat (SupportedAbis) : defaultAbi;
			var outputFile = string.IsNullOrEmpty (OutputFile) ? GetTempFile () : OutputFile;
			foreach (var abi in abis) {
				var currentResourceOutputFile = abi != null ? string.Format ("{0}-{1}", outputFile, abi) : outputFile;
				if (!string.IsNullOrEmpty (currentResourceOutputFile) && !Path.IsPathRooted (currentResourceOutputFile))
					currentResourceOutputFile = Path.Combine (WorkingDirectory, currentResourceOutputFile);
				string[] cmd = GenerateCommandLineCommands (manifest, abi, currentResourceOutputFile);
				if (!cmd.Any () || !ExecuteForAbi (cmd, currentResourceOutputFile)) {
					Cancel ();
				}
			}
		}

		string GetTempFile ()
		{
			var temp = Path.GetTempFileName ();
			lock (tempFiles)
				tempFiles.Add (temp);
			return temp;
		}
	}

}