using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using System.Runtime.Serialization;
using Xamarin.Tools.Zip;

namespace apkdiff {

	struct FileInfo {
		public long Size;
	}

	[DataContract (Namespace = "apk")]
	public class ApkDescription {

		[DataMember]
		string Comment;

		[DataMember]
		long PackageSize;
		string PackagePath;

		[DataMember]
		readonly Dictionary<string, FileInfo> Entries = new Dictionary<string, FileInfo> ();

		public static ApkDescription Load (string path)
		{
			if (!File.Exists (path)) {
				Program.Error ($"File '{path}' does not exist.");
				Environment.Exit (2);
			}

			var extension = Path.GetExtension (path);
			switch (extension.ToLower ()) {
			case ".apk":
				var nd = new ApkDescription ();

				nd.LoadApk (path);

				return nd;
			case ".apkdesc":
				return LoadDescription (path);
			default:
				Program.Error ($"Unknown file extension '{extension}'");
				Environment.Exit (3);

				return null;
			}
		}

		void LoadApk (string path)
		{
			var zip = ZipArchive.Open (path, FileMode.Open);

			if (Program.Verbose)
				Program.ColorWriteLine ($"Loading apk '{path}'", ConsoleColor.Yellow);

			PackageSize = new System.IO.FileInfo (path).Length;
			PackagePath = path;

			foreach (var entry in zip) {
				var name = entry.FullName;

				if (Entries.ContainsKey (name)) {
					Program.Warning ("Duplicate APK file entry: {name}");
					continue;
				}

				Entries [name] = new FileInfo { Size = (long)entry.Size };

				if (Program.Verbose)
					Program.ColorWriteLine ($"  {entry.Size,12} {name}", ConsoleColor.Gray);
			}

			if (Program.SaveDescriptions) {
				var descPath = Path.ChangeExtension (path, ".apkdesc");

				Program.ColorWriteLine ($"Saving apk description to '{descPath}'", ConsoleColor.Yellow);
				SaveDescription (descPath);
			}
		}

		static ApkDescription LoadDescription (string path)
		{
			if (Program.Verbose)
				Program.ColorWriteLine ($"Loading description '{path}'", ConsoleColor.Yellow);

			using (var reader = File.OpenText (path)) {
				return new Newtonsoft.Json.JsonSerializer ().Deserialize (reader, typeof (ApkDescription)) as ApkDescription;
			}
		}

		void SaveDescription (string path)
		{
			Comment = Program.Comment;

			using (var writer = File.CreateText (path)) {
				new Newtonsoft.Json.JsonSerializer () { Formatting = Newtonsoft.Json.Formatting.Indented }.Serialize (writer, this);
			}
		}

		void PrintDifference (string key, long diff, string comment = null)
		{
			var color = diff > 0 ? ConsoleColor.Red : ConsoleColor.Green;
			Program.ColorWrite ($"  {diff:+;-;+}{Math.Abs (diff),12}", color);
			Program.ColorWrite ($" {key}", ConsoleColor.Gray);
			Program.ColorWriteLine (comment, color);
		}

		public void Compare (ApkDescription other)
		{
			var keys = Entries.Keys.Union (other.Entries.Keys);
			var differences = new Dictionary<string, long> ();
			var singles = new HashSet<string> ();

			Program.ColorWriteLine ("Size difference in bytes ([*1] apk1 only, [*2] apk2 only):", ConsoleColor.Yellow);

			foreach (var key in Entries.Keys) {
				if (other.Entries.ContainsKey (key)) {
					differences [key] = other.Entries [key].Size - Entries [key].Size;
				} else {
					differences [key] = -Entries [key].Size;
					singles.Add (key);
				}
			}

			foreach (var key in other.Entries.Keys) {
				if (Entries.ContainsKey (key))
					continue;

				differences [key] = other.Entries [key].Size;
				singles.Add (key);
			}

			foreach (var diff in differences.OrderByDescending (v => v.Value)) {
				if (diff.Value == 0)
					continue;

				PrintDifference (diff.Key, diff.Value, singles.Contains (diff.Key) ? $" *{(diff.Value > 0 ? 2 : 1)}" : null);
			}

			Program.ColorWriteLine ("Summary:", ConsoleColor.Green);
			if (Program.Verbose)
				Program.ColorWriteLine ($"  apk1: {PackageSize,12}  {PackagePath}\n  apk2: {other.PackageSize,12}  {other.PackagePath}", ConsoleColor.Gray);

			PrintDifference ("Package size difference", other.PackageSize - PackageSize);
		}
	}
}
