using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using System.Runtime.Serialization;
using Xamarin.Tools.Zip;

namespace apkdiff {

	struct FileProperties : ISizeProvider {
		public long Size { get; set; }
	}

	[DataContract (Namespace = "apk")]
	public class ApkDescription {

		[DataMember]
		string Comment;

		[DataMember]
		public long PackageSize { get; protected set; }
		string PackagePath;

		ZipArchive Archive;

		[DataMember]
		readonly Dictionary<string, FileProperties> Entries = new Dictionary<string, FileProperties> ();

		Dictionary<string, (long Difference, long OriginalTotal)> totalDifferences = new Dictionary<string, (long, long)> ();

		public static ApkDescription Load (string path)
		{
			if (!File.Exists (path)) {
				Program.Error ($"File '{path}' does not exist.");
				Environment.Exit (2);
			}

			var extension = Path.GetExtension (path);
			switch (extension.ToLower ()) {
			case ".apk":
			case ".aab":
				var nd = new ApkDescription ();

				nd.LoadApk (path);

				return nd;
			case ".apkdesc":
			case ".aabdesc":
				return LoadDescription (path);
			default:
				Program.Error ($"Unknown file extension '{extension}'");
				Environment.Exit (3);

				return null;
			}
		}

		void LoadApk (string path)
		{
			Archive = ZipArchive.Open (path, FileMode.Open);

			if (Program.Verbose)
				Program.ColorWriteLine ($"Loading apk '{path}'", ConsoleColor.Yellow);

			PackageSize = new System.IO.FileInfo (path).Length;
			PackagePath = path;

			foreach (var entry in Archive) {
				var name = entry.FullName;

				if (Entries.ContainsKey (name)) {
					Program.Warning ("Duplicate APK file entry: {name}");
					continue;
				}

				Entries [name] = new FileProperties { Size = (long)entry.Size };

				if (Program.Verbose)
					Program.ColorWriteLine ($"  {entry.Size,12} {name}", ConsoleColor.Gray);
			}

			if (Program.SaveDescriptions) {
				var descPath = Path.ChangeExtension (path, Path.GetExtension (path) + "desc");

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

		static ConsoleColor PrintDifferenceStart (string key, long diff, string comment = null, string padding = null)
		{
			var color = diff == 0 ? ConsoleColor.Gray : diff > 0 ? ConsoleColor.Red : ConsoleColor.Green;
			Program.ColorWrite ($"{padding}  {diff:+;-;+}{Math.Abs (diff),12:#,0}", color);
			Program.ColorWrite ($" {key}", ConsoleColor.Gray);
			Program.ColorWrite (comment, color);

			return color;
		}

		static public void PrintDifference (string key, long diff, string comment = null, string padding = null)
		{
			PrintDifferenceStart (key, diff, comment, padding);
			Console.WriteLine ();
		}

		static public void PrintDifference (string key, long diff, long orig, string comment = null, string padding = null)
		{
			var color = PrintDifferenceStart (key, diff, comment, padding);

			if (orig != 0)
				Program.ColorWrite ($" {(float)diff/orig:0.00%} (of {orig:#,0})", color);

			Console.WriteLine ();
		}

		void AddToTotal (string entry, long size)
		{
			var entryDiff = EntryDiff.ForExtension (Path.GetExtension (entry));
			if (entryDiff == null)
				return;

			var diffType = entryDiff.Name;
			if (!totalDifferences.ContainsKey (diffType))
				totalDifferences.Add (diffType, (0, size));
			else {
				var info = totalDifferences [diffType];
				totalDifferences [diffType] = (info.Difference, info.OriginalTotal + size);
			}
		}

		bool AddToDifference (string entry, long diff, out EntryDiff entryDiff)
		{
			entryDiff = EntryDiff.ForExtension (Path.GetExtension (entry));

			if (entryDiff == null)
				return false;

			var diffType = entryDiff.Name;
			if (!totalDifferences.ContainsKey (diffType))
				totalDifferences.Add (diffType, (diff, 0));
			else {
				var info = totalDifferences [diffType];
				totalDifferences [diffType] = (info.Difference + diff, info.OriginalTotal);
			}

			return true;
		}

		public void Compare (ApkDescription other)
		{
			var keys = Entries.Keys.Union (other.Entries.Keys);
			var differences = new Dictionary<string, long> ();
			var singles = new HashSet<string> ();
			var comparingApks = Archive != null && other.Archive != null;

			Program.ColorWriteLine ("Size difference in bytes ([*1] apk1 only, [*2] apk2 only):", ConsoleColor.Yellow);

			foreach (var entry in Entries) {
				var key = entry.Key;
				if (other.Entries.ContainsKey (key)) {
					var otherEntry = other.Entries [key];
					differences [key] = otherEntry.Size - Entries [key].Size;
				} else {
					differences [key] = -Entries [key].Size;
					singles.Add (key);
				}

				AddToTotal (key, Entries [key].Size);
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

				var single = singles.Contains (diff.Key);

				PrintDifference (diff.Key, diff.Value, single ? $" *{(diff.Value > 0 ? 2 : 1)}" : null);

				EntryDiff entryDiff;
				if (!AddToDifference (diff.Key, diff.Value, out entryDiff))
					continue;

				if (Program.AssemblyRegressionThreshold != 0 && entryDiff is AssemblyDiff && diff.Value > Program.AssemblyRegressionThreshold) {
					Program.Error ($"Assembly size differs more than {Program.AssemblyRegressionThreshold:#,0} bytes.");
					Program.RegressionCount ++;
				}

				if (comparingApks && !single)
					CompareEntries (new KeyValuePair<string, FileProperties> (diff.Key, Entries [diff.Key]), new KeyValuePair<string, FileProperties> (diff.Key, other.Entries [diff.Key]), other, entryDiff);
			}

			Program.ColorWriteLine ("Summary:", ConsoleColor.Green);
			if (Program.Verbose)
				Program.ColorWriteLine ($"  apk1: {PackageSize,12}  {PackagePath}\n  apk2: {other.PackageSize,12}  {other.PackagePath}", ConsoleColor.Gray);

			foreach (var total in totalDifferences)
				PrintDifference (total.Key, total.Value.Difference, total.Value.OriginalTotal);

			PrintDifference ("Package size difference", other.PackageSize - PackageSize, PackageSize);
		}

		void CompareEntries (KeyValuePair<string, FileProperties> entry, KeyValuePair<string, FileProperties> other, ApkDescription otherApk, EntryDiff diff)
		{
			var tmpDir = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			var tmpDirOther = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());

			if (Program.Verbose)
				Program.ColorWriteLine ($"Extracting '{entry.Key}' to {tmpDir} and {tmpDirOther} temporary directories", ConsoleColor.Gray);

			Directory.CreateDirectory (tmpDir);

			var zipEntry = Archive.ReadEntry (entry.Key, true);
			zipEntry.Extract (tmpDir, entry.Key);

			var zipEntryOther = otherApk.Archive.ReadEntry (other.Key, true);
			zipEntryOther.Extract (tmpDirOther, other.Key);

			diff.Compare (Path.Combine (tmpDir, entry.Key), Path.Combine (tmpDirOther, other.Key), "  ");

			Directory.Delete (tmpDir, true);
			Directory.Delete (tmpDirOther, true);
		}
	}
}
