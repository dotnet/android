using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using Xamarin.Android.Tools;
using System.Collections.Generic;
using System.Text;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class Lint : AndroidToolTask
	{
		public override string TaskPrefix => "LNT";

		// we need to check for lint based errors and warnings
		// Sample Manifest Warnings note the ^ and ~ differences....
		//
		//AndroidManifest.xml:19: Warning: <uses-permission> tag appears after <application> tag [ManifestOrder]
		//<uses-permission android:name="android.permission.INTERNET" />
		//	~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//AndroidManifest.xml:4: Warning: Should explicitly set android:icon, there is no default [MissingApplicationIcon]
		//<application android:label="SDLDroidTEst" android:name="mono.android.app.Application" android:debuggable="true">
		//	^
		//android/AndroidManifest.xml:4: Error: Class referenced in the manifest, mono.android.app.Application, was not found in the project or the libraries [MissingRegistered]
		//<application android:label="SDLDroidTEst" android:name="mono.android.app.Application" android:debuggable="true">
		//	^
		//android/src/sdldroidtest/SDLSurface.java:43: Error: Call requires API level 21 (current min is 10): new android.view.SurfaceView [NewApi]
		//super (p0, p1, p2, p3);
		//~~~~~
		//
		const string CodeErrorRegExString = @"(?<file>.+\.*):(?<line>\d+):(?<text>.+)";
		// We also need to capture warning and errors which DONOT include file information
		//
		//Warning: The resource R.layout.main appears to be unused [UnusedResources]
		//Warning: The resource R.string.app_name appears to be unused [UnusedResources]
		//Warning: The resource R.string.hello appears to be unused [UnusedResources]
		const string NoFileWarningOrErrorRegExString = @"(?<type>Warning|Error):(?<text>.+)";
		Regex codeErrorRegEx = new Regex (CodeErrorRegExString, RegexOptions.Compiled);
		Regex noFileWarningOrErrorRegEx = new Regex(NoFileWarningOrErrorRegExString, RegexOptions.Compiled);
		Dictionary<string, string> _resource_name_case_map;
		bool matched = false;
		string file;
		int line;
		int column;
		string text;
		string type;

		Dictionary<string, string> resource_name_case_map => _resource_name_case_map ??= MonoAndroidHelper.LoadResourceCaseMap (BuildEngine4, ProjectSpecificTaskObjectKey);

		[Required]
		public string TargetDirectory { get; set; }

		[Required]
		public string IntermediateOutputPath { get; set; }

		[Required]
		public string JavaSdkPath { get; set; }

		/// <summary>
		/// Location of an xml config files used to
		/// determine whether issues are enabled or disabled
		/// for lint. Normally named lint.xml however it can
		/// be any file using the AndroidLintConfig build action
		/// </summary>
		/// Sample config file
		/// <example>
		/// <code>
		/// <?xml version="1.0" encoding="UTF-8"?>
		/// <lint>
		/// <!-- Disable the given check in this project -->
		/// <issue id="HardcodedDebugMode" severity="ignore" />
		/// <!-- Change severity of NewApi check to warning -->
		/// <issue id="NewApi" severity="warning" />
		/// </lint>
		/// </code>
		/// </example>
		/// <value>The location of the config file.</value>
		public ITaskItem[] ConfigFiles { get; set; }

		/// <summary>
		/// Enable the specific list of issues. This checks all
		/// the default issues plus the specifically enabled
		/// issues. The list should be a comma-separated list of
		/// issue id's or categories.
		/// </summary>
		/// <value>The list of issues to enable.</value>
		public string EnabledIssues { get; set; }

		/// <summary>
		/// Disable the list of categories or specific issue
		/// id's. The list should be a comma-separated list of
		/// issue id's or categories
		/// </summary>
		/// <value>The issues to disable.</value>
		public string DisabledIssues { get; set; }

		/// <summary>
		/// Only check the specific list of issues. This will
		/// disable everything and re-enable the given list of
		/// issues. The list should be a comma-separated list of
		/// issue id's or categories.
		/// </summary>
		/// <value>The list of issues to check.</value>
		public string CheckIssues { get; set; }

		/// <summary>
		/// Add the given folders (or paths) as a resource
		/// directories for the project.
		/// </summary>
		/// <value>An array of ITaskItems containing the resource directories.</value>
		public ITaskItem[] ResourceDirectories {get; set;}

		/// <summary>
		/// Add the given folders (or paths) as a source directories
		/// for the project.
		/// </summary>
		/// <value>An array of ITaskItems containing the source directories.</value>
		public ITaskItem[] SourceDirectories   {get; set;}

		/// <summary>
		/// Add the given folder (or path) as a class directories
		/// for the project.
		/// </summary>
		/// <value>An array of ITaskItems containing class directories.</value>
		public ITaskItem[] ClassDirectories    {get; set;}

		/// <summary>
		/// Add the given folders (or jar files, or paths) as
		/// class directories for the project.
		/// </summary>
		/// <value>An array of ITaskItems containing the class path jars.</value>
		public ITaskItem[] ClassPathJars       {get; set;}

		/// <summary>
		/// Add the given folders (or paths) as a
		/// class libraries for the project.
		/// </summary>
		/// <value>An array of ITaskItems containing the list of directories</value>
		public ITaskItem[] LibraryDirectories  {get; set;}

		/// <summary>
		/// Add the given jar files as a
		/// class library for the project.
		/// </summary>
		/// <value>An array of ITaskITems containing the list of .jar files</value>
		public ITaskItem[] LibraryJars         {get; set;}

		protected override string ToolName {
			get { return OS.IsWindows ? "lint.bat" : "lint"; }
		}

		public Lint ()
		{
			ResourceDirectories = Array.Empty<ITaskItem> ();
			SourceDirectories = Array.Empty<ITaskItem> ();
			ClassDirectories = Array.Empty<ITaskItem> ();
			ClassPathJars = Array.Empty<ITaskItem> ();
			LibraryDirectories = Array.Empty<ITaskItem> ();
			LibraryJars = Array.Empty<ITaskItem> ();
		}

		static readonly Dictionary<string, Version> DisabledIssuesByVersion = new Dictionary<string, Version> () {
			// We need to hard code this test in because Lint will issue an Error
			// if android:debuggable appears in the manifest. We actually need that
			// in debug mode. It seems the android tools do some magic to
			// decide if its needed or not.
			{ "HardcodedDebugMode", new Version(1, 0) },
			// We need to hard code this test as disabled in because Lint will issue a warning
			// for all the resources not used. Now because we don't have any Java code
			// that means for EVERYTHING! Which will be a HUGE amount of warnings for a large project
			{ "UnusedResources", new Version(1, 0) },
			// We need to hard code this test as disabled in because Lint will issue a warning
			// for the MonoPackageManager.java since we have to use a static to keep track of the
			// application instance.
			{ "StaticFieldLeak", new Version(26, 0, 2) },
			// We need to hard code this test as disabled because Lint will issue a error
			// for our generated code not calling super.OnCreate. This however is by design
			// so we need to ignore this error.
			{ "MissingSuperCall", new Version (26, 1, 1) },
			// We need to disble this test since the code in
			// MonoPackageManager.java causes this warning to be emitted.
			// This is by design so we can safely ignore this.
			{ "ObsoleteSdkInt", new Version (26, 1, 1) },
			// In cmdline-tools/4.0 and LintVersion=4.2.0 we started seeing errors such as:
			// obj/Debug/android/AndroidManifest.xml(12,89): error XA0103: MainActivity must extend android.app.Activity [Instantiatable]
			// obj/Debug/android/AndroidManifest.xml(18,28): error XA0103: MonoRuntimeProvider must extend android.content.ContentProvider [Instantiatable]
			{ "Instantiatable", new Version (4, 2, 0) },
		};

		static readonly Regex lintVersionRegex = new Regex (@"version[\t\s]+(?<version>[\d\.]+)", RegexOptions.Compiled);

		public override bool RunTask ()
		{
			if (string.IsNullOrEmpty (ToolPath) || !File.Exists (GenerateFullPathToTool ())) {
				Log.LogCodedError ("XA5205", Properties.Resources.XA5205_Lint, ToolName);
				return false;
			}

			bool fromCmdlineTools   = ToolPath.IndexOf ("cmdline-tools", StringComparison.OrdinalIgnoreCase) >= 0;

			Version lintToolVersion = GetLintVersion (GenerateFullPathToTool ());
			Log.LogDebugMessage ("  LintVersion: {0}", lintToolVersion);
			foreach (var issue in DisabledIssuesByVersion) {
				if (fromCmdlineTools || lintToolVersion >= issue.Value) {
					if (string.IsNullOrEmpty (DisabledIssues) || !DisabledIssues.Contains (issue.Key))
						DisabledIssues = issue.Key + (!string.IsNullOrEmpty (DisabledIssues) ? "," + DisabledIssues : "");
				}
			}

			foreach (var issue in DisabledIssuesByVersion) {
				if (!fromCmdlineTools || (lintToolVersion < issue.Value)) {
					DisabledIssues = CleanIssues (issue.Key, lintToolVersion, DisabledIssues, nameof (DisabledIssues));
					EnabledIssues = CleanIssues (issue.Key, lintToolVersion, EnabledIssues, nameof (EnabledIssues) );
				}
			}

			EnvironmentVariables = new [] { "JAVA_HOME=" + JavaSdkPath };

			base.RunTask ();

			return !Log.HasLoggedErrors;
		}

		string CleanIssues (string issueToRemove, Version lintToolVersion, string issues, string issuePropertyName)
		{
			Regex issueReplaceRegex = new Regex ($"\b{issueToRemove}\b(,)?");
			if (!string.IsNullOrEmpty (issues) && issues.Contains (issueToRemove)) {
				var match = issueReplaceRegex.Match (DisabledIssues);
				if (match.Success) {
					issues = issues.Replace (match.Value, string.Empty);
					Log.LogCodedWarning ("XA0123", issueToRemove, issuePropertyName, lintToolVersion);
				}
			}
			return issues;
		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();
			cmd.AppendSwitch ("--quiet");
			if (ConfigFiles != null && ConfigFiles.Any ()) {
				var config = MergeConfigFiles ();
				var configPath = Path.Combine (IntermediateOutputPath, "lint.xml");
				config.Save (configPath);
				cmd.AppendSwitchIfNotNull ("--config ", configPath);
			}
			cmd.AppendSwitchIfNotNull ("--enable ", EnabledIssues);
			cmd.AppendSwitchIfNotNull ("--disable ", DisabledIssues);
			cmd.AppendSwitchIfNotNull ("--check ", CheckIssues);
			foreach (var item in ResourceDirectories) {
				cmd.AppendSwitchIfNotNull ("--resources ", item.ItemSpec);
			}
			foreach (var item in SourceDirectories) {
				cmd.AppendSwitchIfNotNull ("--sources ", item.ItemSpec);
			}
			foreach (var item in ClassDirectories) {
				cmd.AppendSwitchIfNotNull ("--classpath ", item.ItemSpec);
			}
			foreach (var item in ClassPathJars) {
				cmd.AppendSwitchIfNotNull ("--classpath ", item.ItemSpec);
			}
			foreach (var item in LibraryDirectories) {
				cmd.AppendSwitchIfNotNull ("--libraries ", item.ItemSpec);
			}
			foreach (var item in LibraryJars) {
				cmd.AppendSwitchIfNotNull ("--libraries ", item.ItemSpec);
			}
			cmd.AppendFileNameIfNotNull (TargetDirectory);
			return cmd.ToString();
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			var match = codeErrorRegEx.Match (singleLine);
			if (!match.Success)
				match = noFileWarningOrErrorRegEx.Match (singleLine);
			if (match.Success) {
				if (matched) {
					// we are already in a warning/error ...
					// dont loose it
					GenerateErrorOrWarning ();
				}
				matched = true;
				file = match.Groups ["file"].Value;
				if (!string.IsNullOrEmpty (file)) {
					file = Path.Combine (TargetDirectory, file);
					// Try to map back to the original resource file, so when the user
					// double clicks the error, it won't take them to the obj/Debug copy
					if (ResourceDirectories != null) {
						foreach (var dir in ResourceDirectories) {
							var resourceDirectory = Path.Combine (TargetDirectory, dir.ItemSpec);
							string newfile = MonoAndroidHelper.FixUpAndroidResourcePath (file, resourceDirectory, string.Empty, resource_name_case_map);
							if (!string.IsNullOrEmpty (newfile)) {
								file = newfile;
								break;
							}
						}
					}
				}
				line = 0;
				int.TryParse (match.Groups ["line"].Value, out line);
				text = match.Groups ["text"].Value.Trim ();
				type = match.Groups ["type"].Value;
				if (string.IsNullOrEmpty (type))
					type = text.Contains ("Error") ? "Error" : "Warning";
				column = 0;
			}
			if (matched) {
				if (singleLine.Trim () == "^") {
					column = singleLine.IndexOf ("^", StringComparison.Ordinal);
					GenerateErrorOrWarning ();
				}
				if (singleLine.Trim ().Contains ("~")) {
					column = singleLine.IndexOf ("~", StringComparison.Ordinal);
					GenerateErrorOrWarning ();
				}
			} else
				base.LogEventsFromTextOutput (singleLine, messageImportance);
		}

		void GenerateErrorOrWarning ()
		{
			matched = false;
			if (type == "Warning") {
				if (!string.IsNullOrEmpty (file))
					Log.LogWarning ("", "XA0102", "", file, line, column, 0, 0, text.Replace ("Warning:", ""));
				else
					Log.LogCodedWarning ("XA0102", text.Replace ("Warning:", ""));
			} else {
				if (!string.IsNullOrEmpty (file))
					Log.LogError ("", "XA0103", "", file, line, column, 0, 0, text.Replace ("Error:", ""));
				else
					Log.LogCodedError ("XA0103", text.Replace ("Error:", ""));
			}
		}

		XDocument MergeConfigFiles()
		{
			var config = new XDocument ();
			var lintRoot = new XElement ("lint");
			config.Add (lintRoot);
			foreach (var configFile in ConfigFiles) {
				var doc = XDocument.Load (configFile.ItemSpec);
				var issues = doc.Element ("lint").Elements ("issue");
				lintRoot.Add (issues);
			}
			return config;
		}

		Version GetLintVersion (string tool)
		{
			var sb = new StringBuilder ();
			var result = MonoAndroidHelper.RunProcess (tool, "--version", (s, e) => {
				if (!string.IsNullOrEmpty (e.Data))
					sb.AppendLine (e.Data);
			}, (s, e) => {
				if (!string.IsNullOrEmpty (e.Data))
					sb.AppendLine (e.Data);
			},
			new Dictionary<string, string> {
				{ "JAVA_HOME", JavaSdkPath }
			});
			var versionInfo = sb.ToString ();
			if (result != 0 || versionInfo.Contains ("unknown")) {
				// lets try to parse the lint-xx-x-x-dev.jar filename to get the version
				var libPath = Path.Combine (Path.GetDirectoryName (tool), "..", "lib");
				if (Directory.Exists (libPath)) {
					Version v;
					foreach (var file in Directory.EnumerateFiles (libPath, "lint-??.?.?-dev.jar")) {
						var split = Path.GetFileName (file).Split ('-');
						if (split.Length != 3)
							continue;
						if (!string.IsNullOrEmpty (split [1])) {
							if (Version.TryParse (split [1], out v)) {
								return v;
							}
						}
					}
				}
				Log.LogCodedWarning ("XA0108", Properties.Resources.XA0108, tool);
				return new Version (1, 0);
			}
			// lint: version 26.0.2
			var versionNumberMatch = lintVersionRegex.Match (versionInfo);
			Version versionNumber;
			if (versionNumberMatch.Success && Version.TryParse (versionNumberMatch.Groups ["version"]?.Value, out versionNumber)) {
				return versionNumber;
			}
			return new Version (1, 0);
		}
	}
}

