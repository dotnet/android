// Copyright (C) 2012 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class BindingsGenerator : AndroidToolTask
	{
		public bool OnlyRunXmlAdjuster { get; set; }

		public string XmlAdjusterOutput { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		public string EnumDirectory { get; set; }

		public string EnumMetadataDirectory { get; set; }

		[Required]
		public string AndroidApiLevel { get; set; }

		[Required]
		public string ApiXmlInput { get; set; }

		public string AssemblyName { get; set; }
		
		public string CodegenTarget { get; set; }

		public bool NoStdlib { get; set; }

		public string TypeMappingReportFile { get; set; }

		public bool UseShortFileNames { get; set; }

		// apart from ReferencedManagedLibraries we need it to find mscorlib.dll.
		[Required]
		public string MonoAndroidFrameworkDirectories { get; set; }

		public ITaskItem[] TransformFiles { get; set; }
		public ITaskItem[] ReferencedManagedLibraries { get; set; }
		public ITaskItem[] AnnotationsZipFiles { get; set; }

		protected override string DefaultErrorCode => "BG0000";

		private List<Tuple<string, string>> transform_files = new List<Tuple<string,string>> ();

		public override bool Execute ()
		{
			Log.LogDebugMessage ("BindingsGenerator Task");
			Log.LogDebugMessage ("  OnlyRunXmlAdjuster: {0}", OnlyRunXmlAdjuster);
			Log.LogDebugMessage ("  OutputDirectory: {0}", OutputDirectory);
			Log.LogDebugMessage ("  EnumDirectory: {0}", EnumDirectory);
			Log.LogDebugMessage ("  EnumMetadataDirectory: {0}", EnumMetadataDirectory);
			Log.LogDebugMessage ("  ApiXmlInput: {0}", ApiXmlInput);
			Log.LogDebugMessage ("  AssemblyName: {0}", AssemblyName);
			Log.LogDebugMessage ("  AndroidApiLevel: {0}", AndroidApiLevel);
			Log.LogDebugMessage ("  UseShortFileNames: {0}", UseShortFileNames);
			Log.LogDebugTaskItems ("  TransformFiles:", TransformFiles);
			Log.LogDebugTaskItems ("  ReferencedManagedLibraries:", ReferencedManagedLibraries);
			Log.LogDebugTaskItems ("  AnnotationsZipFiles:", AnnotationsZipFiles);
			Log.LogDebugTaskItems ("  TypeMappingReportFile:", TypeMappingReportFile);

			Directory.CreateDirectory (OutputDirectory);

			// We need to do this validation in Execute rather than GenerateCommandLineCommands
			// because we can't terminate the build nicely in GenerateCommandLineCommands.
			if (TransformFiles != null)
				foreach (var fixup in TransformFiles) {
					try {
						var doc = XDocument.Load (fixup.ItemSpec);

						switch (doc.Root.Name.LocalName) {
							case "metadata":
								Log.LogDebugMessage ("Adding transform file {0} as metadata.", fixup.ItemSpec);
								transform_files.Add (new Tuple<string, string> (fixup.ItemSpec, "fixup"));
								break;
							case "enum-field-mappings":
								Log.LogDebugMessage ("Adding transform file {0} as enumfields.", fixup.ItemSpec);
								transform_files.Add (new Tuple<string, string> (fixup.ItemSpec, "enumfields"));
								break;
							case "enum-method-mappings":
								Log.LogDebugMessage ("Adding transform file {0} as enummethods.", fixup.ItemSpec);
								transform_files.Add (new Tuple<string, string> (fixup.ItemSpec, "enummethods"));
								break;
							default:
								Log.LogError ("Unrecognized file", string.Empty, string.Empty, fixup.ItemSpec, 0, 0, 0, 0, "Unrecognized transform root element: {0}.", doc.Root.Name.LocalName);
								return false;
						}
					} catch (Exception ex) {
						Log.LogError ("Invalid Xml", string.Empty, string.Empty, fixup.ItemSpec, 0, 0, 0, 0, "Error parsing xml.\n{0}", ex);
						return false;
					}
				}

			return base.Execute ();
		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();

			cmd.AppendFileNameIfNotNull (ApiXmlInput);

			if (OnlyRunXmlAdjuster)
				cmd.AppendSwitch ("--only-xml-adjuster");
			cmd.AppendSwitchIfNotNull ("--xml-adjuster-output=", XmlAdjusterOutput);

			cmd.AppendSwitchIfNotNull ("--codegen-target=", CodegenTarget);
			cmd.AppendSwitchIfNotNull ("--csdir=", OutputDirectory);
			cmd.AppendSwitchIfNotNull ("--enumdir=", EnumDirectory);
			cmd.AppendSwitchIfNotNull ("--enummetadata=", EnumMetadataDirectory);
			cmd.AppendSwitchIfNotNull ("--assembly=", AssemblyName);

			if (!NoStdlib) {
				string fxpath = MonoAndroidFrameworkDirectories.Split (';').First (p => new DirectoryInfo (p).GetFiles ("mscorlib.dll").Any ());
				cmd.AppendSwitchIfNotNull ("--ref=", Path.Combine (Path.GetFullPath (fxpath), "mscorlib.dll"));
			}
			
			if (ReferencedManagedLibraries != null)
				foreach (var lib in ReferencedManagedLibraries)
					cmd.AppendSwitchIfNotNull ("--ref=", Path.GetFullPath (lib.ItemSpec));
			if (AnnotationsZipFiles != null)
				foreach (var zip in AnnotationsZipFiles)
					cmd.AppendSwitchIfNotNull ("--annotations=", Path.GetFullPath (zip.ItemSpec));

			foreach (var tf in transform_files)
				cmd.AppendSwitchIfNotNull (string.Format ("--{0}=", tf.Item2), tf.Item1);

			cmd.AppendSwitchIfNotNull ("--api-level=", AndroidApiLevel);

			cmd.AppendSwitchIfNotNull ("--type-map-report=", TypeMappingReportFile);

			cmd.AppendSwitch ("--global");
			cmd.AppendSwitch ("--public");

			if (UseShortFileNames)
				cmd.AppendSwitch ("--use-short-file-names");

			return cmd.ToString ();
		}

		protected override string ToolName {
			get { return "generator.exe"; }
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}
	}
}
