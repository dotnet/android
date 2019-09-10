﻿using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Tasks
{
	public class BuildBaseAppBundle : BuildApk
	{
		public override string TaskPrefix => "BBA";

		/// <summary>
		/// Files that need to land in the final APK need to go in `root/`
		/// </summary>
		protected override string RootPath => "root/";

		/// <summary>
		/// `.dex` files should be in `dex/`
		/// </summary>
		protected override string DalvikPath => "dex/";

		/// <summary>
		/// Nothing needs to be compressed with app bundles. BundleConfig.json specifies the final compression mode.
		/// </summary>
		protected override CompressionMethod UncompressedMethod => CompressionMethod.Default;

		/// <summary>
		/// aapt2 is putting AndroidManifest.xml in the root of the archive instead of at manifest/AndroidManifest.xml that bundletool expects.
		/// I see no way to change this behavior, so we can move the file for now:
		/// https://github.com/aosp-mirror/platform_frameworks_base/blob/e80b45506501815061b079dcb10bf87443bd385d/tools/aapt2/LoadedApk.h#L34
		/// </summary>
		protected override void FixupArchive (ZipArchiveEx zip)
		{
			var entry = zip.Archive.ReadEntry ("AndroidManifest.xml");
			using (var stream = new MemoryStream ()) {
				entry.Extract (stream);
				stream.Position = 0;
				zip.Archive.AddEntry ("manifest/AndroidManifest.xml", stream);
				zip.Archive.DeleteEntry (entry);
				zip.Flush ();
			}
		}
	}
}
