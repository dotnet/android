using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class Lint : ToolTask
	{
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
		bool matched = false;
		string file;
		int line;
		int column;
		string text;
		string type;

		[Required]
		public string TargetDirectory { get; set; }

		[Required]
		public string IntermediateOutputPath { get; set; }

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
			ResourceDirectories = new ITaskItem[0];
			SourceDirectories = new ITaskItem[0];
			ClassDirectories = new ITaskItem[0];
			ClassPathJars = new ITaskItem[0];
			LibraryDirectories = new ITaskItem[0];
			LibraryJars = new ITaskItem[0];
		}


		string [] disabledIssues = new string [] {
			// We need to hard code this test in because Lint will issue an Error 
			// if android:debuggable appears in the manifest. We actually need that
			// in debug mode. It seems the android tools do some magic to
			// decide if its needed or not.
			"HardcodedDebugMode",
			// We need to hard code this test as disabled in because Lint will issue a warning
			// for all the resources not used. Now because we don't have any Java code
			// that means for EVERYTHING! Which will be a HUGE amount of warnings for a large project
			"UnusedResources",
			// We need to hard code this test as disabled in because Lint will issue a warning
			// for the MonoPackageManager.java since we have to use a static to keep track of the
			// application instance.
			"StaticFieldLeak",
			// We don't call base.Super () for onCreate so we need to ignore this too.
			"MissingSuperCall",
		};

		public override bool Execute ()
		{
			foreach (var disabled in disabledIssues) {
				if (string.IsNullOrEmpty (DisabledIssues) || !DisabledIssues.Contains (disabled))
					DisabledIssues = disabled + (!string.IsNullOrEmpty (DisabledIssues) ? "," + DisabledIssues : "");
			}
			
			Log.LogDebugMessage ("Lint Task");
			Log.LogDebugMessage ("  TargetDirectory: {0}", TargetDirectory);
			Log.LogDebugMessage ("  EnabledChecks: {0}", EnabledIssues);
			Log.LogDebugMessage ("  DisabledChecks: {0}", DisabledIssues);
			Log.LogDebugMessage ("  CheckIssues: {0}", CheckIssues);
			Log.LogDebugTaskItems ("  ConfigFiles:", ConfigFiles);
			Log.LogDebugTaskItems ("  ResourceDirectories:", ResourceDirectories);
			Log.LogDebugTaskItems ("  SourceDirectories:", SourceDirectories);
			Log.LogDebugTaskItems ("  ClassDirectories:", ClassDirectories);
			Log.LogDebugTaskItems ("  LibraryDirectories:", LibraryDirectories);
			Log.LogDebugTaskItems ("  LibraryJars:", LibraryJars);

			if (string.IsNullOrEmpty (ToolPath) || !File.Exists (GenerateFullPathToTool ())) {
				Log.LogCodedError ("XA5205", $"Cannot find `{ToolName}` in the Android SDK. Please set its path via /p:LintToolPath.");
				return false;
			}

			base.Execute ();

			return !Log.HasLoggedErrors;
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
				if (!string.IsNullOrEmpty (file))
					file = Path.Combine (TargetDirectory, file);
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
					column = singleLine.IndexOf ("^");
					GenerateErrorOrWarning ();
				}
				if (singleLine.Trim ().Contains ("~")) {
					column = singleLine.IndexOf ("~");
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
					Log.LogWarning (text.Replace ("Warning:", ""));
			} else {
				if (!string.IsNullOrEmpty (file))
					Log.LogError ("", "XA0103", "", file, line, column, 0, 0, text.Replace ("Error:", ""));
				else
					Log.LogError (text.Replace ("Error:", ""));
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
	}
}

