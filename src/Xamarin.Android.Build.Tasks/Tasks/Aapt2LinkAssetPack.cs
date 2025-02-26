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
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks {

	public class Aapt2LinkAssetPack : Aapt2 {
		public override string TaskPrefix => "A2LAP";

		[Required]
		public ITaskItem Manifest { get; set; }

		[Required]
		public ITaskItem[] AssetDirectories { get; set; }

		[Required]
		public string PackageName { get; set; }

		[Required]
		public ITaskItem OutputArchive { get; set; }

		protected override int GetRequiredDaemonInstances ()
		{
			return Math.Min (1, DaemonMaxInstanceCount);
		}

		public async override System.Threading.Tasks.Task RunTaskAsync ()
		{
			RunAapt (GenerateCommandLineCommands (Manifest, OutputArchive), OutputArchive.ItemSpec);
			ProcessOutput ();
			if (File.Exists (OutputArchive.ItemSpec)) {
				// move the manifest to the right place.
				using (var zip = new ZipArchiveEx (OutputArchive.ItemSpec, File.Exists (OutputArchive.ItemSpec) ? FileMode.Open : FileMode.Create)) {
					zip.MoveEntry ("AndroidManifest.xml", "manifest/AndroidManifest.xml");
					zip.Archive.DeleteEntry ("resources.pb");
					// Fix up aapt2 not dealing with '\' in subdirectories for assets.
					zip.FixupWindowsPathSeparators ((a, b) => Log.LogDebugMessage ($"Fixing up malformed entry `{a}` -> `{b}`"));
				}
			}
			await System.Threading.Tasks.Task.CompletedTask;
		}

		protected string[] GenerateCommandLineCommands (ITaskItem manifest, ITaskItem output)
		{
			//link --manifest AndroidManifest.xml --proto-format --custom-package $(Package) -A $(AssetsDirectory) -o $(_TempOutputFile)
			List<string> cmd = new List<string> ();
			cmd.Add ("link");
			if (MonoAndroidHelper.LogInternalExceptions)
				cmd.Add ("-v");
			cmd.Add ("--manifest");
			cmd.Add (GetFullPath (manifest.ItemSpec));
			cmd.Add ("--proto-format");
			cmd.Add ("--custom-package");
			cmd.Add (PackageName);
			foreach (var assetDirectory in AssetDirectories) {
				var fullPath = GetFullPath (assetDirectory.ItemSpec);
				if (OS.IsWindows && !IsPathOnlyASCII (fullPath)) {
					LogCodedError ("APT2265", Properties.Resources.APT2265, fullPath);
					continue;
				}
				cmd.Add ("-A");
				cmd.Add (fullPath);
			}
			cmd.Add ($"-o");
			cmd.Add (GetFullPath (output.ItemSpec));
			return cmd.ToArray ();
		}
	}
}