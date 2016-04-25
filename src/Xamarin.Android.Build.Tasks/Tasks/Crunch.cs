using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using Xamarin.AndroidTools;
using Xamarin.Android.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class Crunch : ToolTask
	{
		[Required]
		public ITaskItem[] SourceFiles { get; set; }

		protected override string ToolName { get { return OS.IsWindows ? "aapt.exe" : "aapt"; } }

		private string tempDirectory;
		private string tempOutputDirectory;

		public override bool Execute ()
		{
			Log.LogDebugMessage ("Crunch Task");
			Log.LogDebugTaskItems ("  SourceFiles:", SourceFiles);
			Log.LogDebugMessage ("  ToolPath: {0}", ToolPath);
			Log.LogDebugMessage ("  ToolExe: {0}", ToolExe);

			// copy the changed files over to a temp location for processing
			var imageFiles = SourceFiles.Where (x => string.Equals (Path.GetExtension (x.ItemSpec),".png", StringComparison.OrdinalIgnoreCase));

			if (!imageFiles.Any ())
				return true;

			foreach (var imageGroup in imageFiles.GroupBy ( x => Path.GetDirectoryName (Path.GetFullPath (x.ItemSpec)))) {
		
				tempDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
				Directory.CreateDirectory (tempDirectory);
				tempOutputDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
				Directory.CreateDirectory (tempOutputDirectory);
				try {
					Log.LogDebugMessage ("Crunch Processing : {0}", imageGroup.Key);
					Log.LogDebugTaskItems ("  Items :", imageGroup.ToArray ());
					foreach (var item in imageGroup) {
						var dest = Path.GetFullPath (item.ItemSpec).Replace (imageGroup.Key, tempDirectory); 
						Directory.CreateDirectory (Path.GetDirectoryName (dest));
						MonoAndroidHelper.CopyIfChanged (item.ItemSpec, dest);
						MonoAndroidHelper.SetWriteable (dest);
					}

					// crunch them
					if (!base.Execute ())
						return false;

					// copy them back
					foreach (var item in imageGroup) {
						var dest = Path.GetFullPath (item.ItemSpec).Replace (imageGroup.Key, tempOutputDirectory); 
						var srcmodifiedDate = File.GetLastWriteTimeUtc (item.ItemSpec);
						if (!File.Exists (dest))
							continue;
						MonoAndroidHelper.CopyIfChanged (dest, item.ItemSpec);
						MonoAndroidHelper.SetWriteable (dest);
						// reset the Dates so MSBuild/xbuild doesn't think they changed.
						MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (item.ItemSpec, srcmodifiedDate, Log);
					}
				} finally {
					Directory.Delete (tempDirectory, recursive:true);
					Directory.Delete (tempOutputDirectory, recursive:true);
				}
			}

			return !Log.HasLoggedErrors;
		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();

			cmd.AppendSwitch ("c");
			cmd.AppendSwitchIfNotNull ("-S " , tempDirectory);
			cmd.AppendSwitchIfNotNull ("-C " , tempOutputDirectory);

			return cmd.ToString ();
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}
	}
}
