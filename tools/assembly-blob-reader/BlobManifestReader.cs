using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Xamarin.Android.AssemblyBlobReader
{
	class BlobManifestReader
	{
		public List<BlobManifestEntry> Entries                      { get; } = new List<BlobManifestEntry> ();
		public Dictionary<uint, BlobManifestEntry> EntriesByHash32  { get; } = new Dictionary<uint, BlobManifestEntry> ();
		public Dictionary<ulong, BlobManifestEntry> EntriesByHash64 { get; } = new Dictionary<ulong, BlobManifestEntry> ();

		public BlobManifestReader (Stream manifest)
		{
			manifest.Seek (0, SeekOrigin.Begin);
			using (var sr = new StreamReader (manifest, Encoding.UTF8, detectEncodingFromByteOrderMarks: false)) {
				ReadManifest (sr);
			}
		}

		void ReadManifest (StreamReader reader)
		{
			// First line is ignored, it contains headers
			reader.ReadLine ();

			// Each subsequent line consists of fields separated with any number of spaces (for the pleasure of a human being reading the manifest)
			while (!reader.EndOfStream) {
				string[]? fields = reader.ReadLine ()?.Split (' ', StringSplitOptions.RemoveEmptyEntries);
				if (fields == null) {
					continue;
				}

				var entry = new BlobManifestEntry (fields);
				Entries.Add (entry);
				if (entry.Hash32 != 0) {
					EntriesByHash32.Add (entry.Hash32, entry);
				}

				if (entry.Hash64 != 0) {
					EntriesByHash64.Add (entry.Hash64, entry);
				}
			}
		}
	}
}
