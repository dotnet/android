// Copyright (C) 2012 Xamarin, Inc. All rights reserved.

using System;
using System.Linq;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class JavaSourceUtils : AndroidToolTask
	{
		public  override    string  TaskPrefix => "JSU";

		[Required]
		public  string      JavaSourceUtilsJar          { get; set; }

		[Required]
		public  string      JavaSdkDirectory            { get; set; }

		[Required]
		public  ITaskItem[] InputFiles                  { get; set; }

		public  ITaskItem[] References                  { get; set; }

		public  ITaskItem[] BootClassPath               { get; set; }

		public  ITaskItem   JavadocCopyrightFile        { get; set; }
		public  string      JavadocUrlPrefix            { get; set; }
		public  string      JavadocUrlStyle             { get; set; }
		public  string      JavadocDocRootUrl           { get; set; }

		public  string      JavaOptions                 { get; set; }

		public  string      JavaMaximumHeapSize         { get; set; }

		public  ITaskItem   OutputJavadocXml            { get; set; }

		string  responseFilePath;

		public override bool RunTask ()
		{
			if (InputFiles == null || InputFiles.Count () == 0) {
				Log.LogCodedError ("XA1020", Properties.Resources.XA1020);
				return false;
			}

			if (References != null)
				foreach (var path in References)
					if (!Directory.Exists (path.ItemSpec) && !File.Exists (path.ItemSpec))
						Log.LogCodedError ("XA1022", Properties.Resources.XA1022, path.ItemSpec);

			if (Log.HasLoggedErrors)
				return false;

			try {
				return base.RunTask ();
			}
			finally {
				File.Delete (responseFilePath);
			}
		}

		static string GetOutputFileName (ITaskItem[] items) =>
			Files.HashString (
				string.Join ("\n", items.Select (item => Path.GetFullPath (item.ItemSpec).Replace ('\\', '/'))));


		protected override string GenerateCommandLineCommands ()
		{
			responseFilePath    = CreateResponseFile ();

			var cmd = new CommandLineBuilder ();

			// Add the JavaOptions if they are not null
			// These could be any of the additional options
			if (!string.IsNullOrEmpty (JavaOptions)) {
				cmd.AppendSwitch (JavaOptions);
			}

			if (!string.IsNullOrEmpty (JavaMaximumHeapSize)) {
				cmd.AppendSwitchIfNotNull("-Xmx", JavaMaximumHeapSize);
			}

			// Arguments sent to java.exe
			cmd.AppendSwitchIfNotNull ("-jar ", JavaSourceUtilsJar);

			cmd.AppendSwitch ($"@{responseFilePath}");


			return cmd.ToString ();
		}

		string CreateResponseFile ()
		{
			var responseFile    = Path.GetTempFileName ();

			using var response  = new StreamWriter (responseFile, append: false, encoding: Files.UTF8withoutBOM);
			Log.LogDebugMessage ("[java-source-utils] response file contents: {0}", responseFile);

			if (BootClassPath != null && BootClassPath.Any ()) {
				var classpath = string.Join (Path.PathSeparator.ToString (), BootClassPath.Select (p => Path.GetFullPath (p.ItemSpec)));
				AppendArg (response, "--classpath");
				AppendArg (response, classpath);
			}

			if (References != null && References.Any ()) {
				foreach (var r in References) {
					if (Directory.Exists (r.ItemSpec)) {
						AppendArg (response, "--source");
						AppendArg (response, Path.GetFullPath (r.ItemSpec));
						continue;
					}
					if (!File.Exists (r.ItemSpec)) {
						Log.LogCodedError ("XA1022", Properties.Resources.XA1022, r.ItemSpec);
						continue;
					}
					if (r.ItemSpec.EndsWith (".jar", StringComparison.OrdinalIgnoreCase)) {
						AppendArg (response, "--jar");
						AppendArg (response, Path.GetFullPath (r.ItemSpec));
						continue;
					}
					if (r.ItemSpec.EndsWith (".aar", StringComparison.OrdinalIgnoreCase)) {
						AppendArg (response, "--aar");
						AppendArg (response, Path.GetFullPath (r.ItemSpec));
						continue;
					}
					Log.LogError ($"Unsupported @(Reference) item: {r.ItemSpec}");
				}
			}
			AppendArg (response, "--output-javadoc");
			AppendArg (response, OutputJavadocXml.ItemSpec);

			if (!string.IsNullOrEmpty (JavadocCopyrightFile?.ItemSpec)) {
				AppendArg (response, "--doc-copyright");
				AppendArg (response, Path.GetFullPath (JavadocCopyrightFile.ItemSpec));
			}
			if (!string.IsNullOrEmpty (JavadocUrlPrefix)) {
				AppendArg (response, "--doc-url-prefix");
				AppendArg (response, JavadocUrlPrefix);
			}
			if (!string.IsNullOrEmpty (JavadocUrlStyle)) {
				AppendArg (response, "--doc-url-style");
				AppendArg (response, JavadocUrlStyle);
			}
			if (!string.IsNullOrEmpty (JavadocDocRootUrl)) {
				AppendArg (response, "--doc-root-url");
				AppendArg (response, JavadocDocRootUrl);
			}

			var inputs  = InputFiles.Select (p => Path.GetFullPath (p.ItemSpec))
				.Distinct (StringComparer.OrdinalIgnoreCase);

			foreach (var path in inputs) {
				AppendArg (response, path);
			}

			return responseFile;

			void AppendArg (TextWriter writer, string line)
			{
				writer.WriteLine (line);
				Log.LogDebugMessage (line);
			}
		}

		protected override string ToolName {
			get => "java-source-utils";
		}

		public override string ToolExe {
			get { return OS.IsWindows ? "java.exe" : "java"; }
			set { base.ToolExe = value; }
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (JavaSdkDirectory, "bin", ToolExe);
		}
	}
}
