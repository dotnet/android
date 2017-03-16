using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using Xamarin.Tools.Zip;

public struct SampleDesc {
	public string ID { get; set; }
	public string FullTypeName { get; set; }
	public string DocumentationFilePath { get; set; }
	public string Language { get; set; } // e.g 'obj-c' when taken from Apple otherwise a value understood by mdoc like 'C#'

	public override string ToString ()
	{
		return string.Format ("{0}: {1} '{2}' in {3}", ID, FullTypeName, DocumentationFilePath, Language);
	}
}

public class SampleRepository
{
	ZipArchive archive;
	HashAlgorithm hasher = MD5.Create ();
	HashSet<string> validFiles = new HashSet<string> ();
	Dictionary<string, SampleDesc> index = new Dictionary<string, SampleDesc> ();
	Dictionary<string, string>     updates = new Dictionary<string, string> ();

	public SampleRepository (string name) : this (ZipArchive.Open (name.EndsWith (".zip") ? name : name + ".zip", FileMode.Open))
	{
		
	}

	protected SampleRepository (ZipArchive archive)
	{
		this.archive = archive;
		if (archive.ContainsEntry ("index.xml"))
			using (var stream = new MemoryStream ()) {
				archive.First (e => e.FullName.Equals ("index.xml", StringComparison.OrdinalIgnoreCase)).Extract (stream);
				stream.Position = 0;
				index = ((ICollection<SampleDesc>)IndexSerializer.Deserialize (stream)).ToDictionary (sd => sd.ID, sd => sd);
			}
	}

	// Returns an id that can be used to register the sample position in the documentation flow
	public string RegisterSample (string source, SampleDesc desc)
	{
		var hash = StringHash (source);
		validFiles.Add (hash);
		desc.ID = hash;

		if (!archive.ContainsEntry (hash)) {
			updates.Add (hash, source);
			archive.AddEntry (hash, source, Encoding.UTF8);
			index[hash] = desc;
		}

		return hash;
	}

	public void OverwriteSample (string hash, string content, SampleDesc newDesc)
	{
		if (!archive.ContainsEntry (hash))
			return;

		UpdateEntry (archive, hash, content);
		index[hash] = newDesc;
	}

	void UpdateEntry (ZipArchive archive, string file, string content)
	{
		var existing = archive.FirstOrDefault (e => e.FullName == file);
		if (existing != null)
			archive.DeleteEntry (existing);
		archive.AddEntry (file, content, Encoding.UTF8);
	}

	public string GetSampleFromID (string id, out SampleDesc desc)
	{
		desc = new SampleDesc ();

		string source;
		if (updates.TryGetValue (id, out source))
			return source;

		var entry = archive.FirstOrDefault (e => e.FullName == id);
		if (entry == null)
			return null;

		desc = index[id];
		using (var stream = new MemoryStream ()) {
			entry.Extract (stream);
			stream.Position = 0;
			return new StreamReader (stream).ReadToEnd ();
		}
	}

	public string GetSampleFromContent (string content, out SampleDesc desc)
	{
		return GetSampleFromID (StringHash (content), out desc);
	}

	public SampleDesc GetSampleDescFromID (string id)
	{
		return index[id];
	}

	public static SampleRepository LoadFrom (string file)
	{
		using (var archive = ZipArchive.Open (file, FileMode.Open)) {
			var samples = new SampleRepository (archive);
			return samples;
		}
	}

	public void Close (bool removeOldEntries)
	{
		// See if we have any stale file
		if (removeOldEntries) {
			var list = new List<ZipEntry> (archive);
			foreach (var entry in list) {
				if (!validFiles.Contains (entry.FullName))
					archive.DeleteEntry (entry);
			}
		}
		// Serialize index
		var writer = new StringWriter ();
		IndexSerializer.Serialize (writer, new List<SampleDesc> (index.Values));
		UpdateEntry (archive, "index.xml", writer.ToString ());

		archive.Close ();
	}

	public IEnumerable<string> AllIDs {
		get {
			return index.Keys;
		}
	}

	public bool IsValidID (string id)
	{
		return index.ContainsKey (id);
	}

	string StringHash (String input)
	{
		// TODO: Reuse byte array
		return hasher.ComputeHash (Encoding.UTF8.GetBytes (input)).Select (b => String.Format("{0:X2}", b)).Aggregate (string.Concat);
	}

	XmlSerializer IndexSerializer {
		get {
			return new XmlSerializer (typeof (List<SampleDesc>));
		}
	}

#if STANDALONE_EXPORTER
	public static void Main (string[] args)
	{
		string samplesPath = args[0];
		string outDir = args[1];

		if (!File.Exists (samplesPath)) {
			Console.WriteLine ("Couldn't find samples repository at {0}");
			return;
		}

		SampleRepository samples = SampleRepository.LoadFrom (samplesPath);

		if (!Directory.Exists (outDir))
			Directory.CreateDirectory (outDir);

		foreach (var id in samples.AllIDs) {
			SampleDesc desc;
			var content = samples.GetSampleFromID (id, out desc);

			var type = desc.FullTypeName.Substring (desc.FullTypeName.LastIndexOf ('.') + 1);
			var ns = desc.FullTypeName.Substring (0, desc.FullTypeName.LastIndexOf ('.'));

			Console.WriteLine ("Processing {0}::{1}", ns, type);
			var path = Path.Combine (outDir, ns, type);
			if (!Directory.Exists (path))
				Directory.CreateDirectory (path);
			if (!Directory.Exists (path))
				Directory.CreateDirectory (path);
			path = Path.Combine (path, id + (desc.Language == "xml" ? ".xml" : ".native"));

			File.WriteAllText (path, content);
		}
	}
#elif STANDALONE_IMPORTER
	public static void Main (string[] args)
	{
		string samplesPath = args[0];
		string inDir = args[1];

		if (!File.Exists (samplesPath)) {
			Console.WriteLine ("Couldn't find samples repository at {0}");
			return;
		}
		if (!Directory.Exists (inDir)) {
			Console.WriteLine ("Couldn't find input snippet directory");
			return;
		}

		var langExtensions = new Dictionary<string, string> () {
			{ ".xml", "XML" },
			{ ".cs", "C#" }
		};

		SampleRepository samples = SampleRepository.LoadFrom (samplesPath);

		foreach (var file in Directory.EnumerateFiles (inDir, "*", SearchOption.AllDirectories)) {
			var extension = Path.GetExtension (file);
			var id = Path.GetFileNameWithoutExtension (file);
			if (string.IsNullOrEmpty (extension) || string.IsNullOrEmpty (id))
				continue;

			string lang;
			if (!langExtensions.TryGetValue (extension, out lang) || !samples.IsValidID (id)) {
				Console.WriteLine ("Not processed: {0}", Path.GetFileName (file));
				continue;
			}
			
			SampleDesc oldDesc = samples.GetSampleDescFromID (id);
			samples.OverwriteSample (id, File.ReadAllText (file), new SampleDesc { 
				ID = id,
				Language = lang,
				DocumentationFilePath = oldDesc.DocumentationFilePath,
				FullTypeName = oldDesc.FullTypeName
			});
			Console.WriteLine ("Done: {0}", id);
		}

		samples.Close (false);
	}
#endif
}
