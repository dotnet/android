using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

public class SourceEntry
{
	public SourceEntry (string [] items)
	{
		ReferrerDoc = items [0];
		string target = items [2];
		TargetLink = target;
		int idx = target.LastIndexOf ('#');
		TargetField = target.Substring (idx + 1);
		target = target.Substring (0, idx - 5); // ".html"
		idx = target.LastIndexOf ('/');
		TargetType = target.Substring (idx + 1);
		TargetPackage = target.Substring (0, idx).Replace ('/', '.');
	}

	public string ReferrerDoc { get; set; }
	public string TargetLink { get; set; }
	public string TargetPackage { get; set; }
	public string TargetType { get; set; }
	public string TargetField { get; set; }
}

public class SourceEntryComparer : IEqualityComparer<SourceEntry>
{
	public bool Equals (SourceEntry e1, SourceEntry e2)
	{
		return e1.ReferrerDoc == e2.ReferrerDoc && e1.TargetLink == e2.TargetLink;
	}
	
	public int GetHashCode (SourceEntry e)
	{
		return e.ReferrerDoc.GetHashCode () + e.TargetLink.GetHashCode ();
	}
}

public class MethodMapGen
{
	public static void Main (string [] args)
	{
		new MethodMapGen ().Run (args);
	}

	List<SourceEntry> sources = new List<SourceEntry> ();
	string doc_base;

	void Run (string [] args)
	{
		doc_base = args [2];

		var enumdoc = new XmlDocument ();
		enumdoc.Load (args [1]);
		foreach (XmlElement pkg in enumdoc.SelectNodes ("/enums/package")) {
			foreach (XmlElement cls in pkg.SelectNodes ("class")) {
				foreach (XmlElement cst in cls.SelectNodes ("const")) {
					string filename = pkg.GetAttribute ("name").Replace ('.', '/')
						+ '/' + cls.GetAttribute ("name") + ".html";
					sources.Add (new SourceEntry (new string [] {filename, null, filename + "#" + cst.GetAttribute ("name") }));
				}
			}
		}
		foreach (var line in File.ReadAllLines (args [0]))
			sources.Add (new SourceEntry (line.Split (' ')));

		sources = new List<SourceEntry> (sources.Where (e => !e.TargetLink.Contains ("GLES") && !e.TargetLink.Contains ("android/R.")).Distinct (new SourceEntryComparer ()).ToArray ());

		sources.Sort ((e1, e2) => string.Compare (e1.ReferrerDoc, e2.ReferrerDoc));
		XmlNodeList links = null;
		string prev = null;
		foreach (var e in sources) {
			Console.Error.WriteLine ("{0} -> {1}", e.ReferrerDoc, e.TargetLink);
			if (prev != e.ReferrerDoc) {
				links = GetDocument (e.ReferrerDoc).SelectNodes ("//a[@href]");
			}
			foreach (XmlElement l in links) {
				if (!l.GetAttribute ("href").Contains (e.TargetLink))
					continue;
				XmlElement aname = l.SelectSingleNode ("ancestor::div[contains(@class, 'jd-details api')]/preceding-sibling::a[@name][1]") as XmlElement;
				if (aname == null) {
					Console.WriteLine ("ERROR: {0} -> {1}", e.ReferrerDoc, e.TargetLink);
					continue;
				}
				Console.WriteLine ("SUCCESS: {0}#{1} -> {2}",
						   e.ReferrerDoc,  aname.GetAttribute ("name"), e.TargetLink);
			}
		}
	}

	XmlDocument GetDocument (string file)
	{
		string abs = Path.Combine (Path.GetFullPath (doc_base), file);
		string outfile = Path.Combine (Directory.GetParent (new Uri (Assembly.GetEntryAssembly ().CodeBase).LocalPath).ToString (), "tmp.xml");
		string args = "--html --nsclean --insert --debugent --nonet --noent --recover --dropdtd --nocatalogs --output " + outfile + " --xmlout " + abs;
		// FIXME: I cannot enable RedirectStandardError = true due to some mono bug.
		Process proc = Process.Start (new ProcessStartInfo ("xmllint", args) { /*RedirectStandardError = true,*/ UseShellExecute = false });
		proc.WaitForExit ();
		if (proc.ExitCode != 0)
			throw new Exception ("xmllint failed");

		var doc = new XmlDocument ();
		string s = File.ReadAllText ("tmp.xml");
		s = s.Replace ("<html>", "<html xmlns:android='dummy'>");
		s = s.Replace ("<!-- /New Search>", "<!-- /New Search -->"); // Why, Google. Why
		s = s.Replace ("<application ...=\"\">", "<application xxx=\"\">");
		doc.Load (new XmlTextReader (new StringReader (s)) { Namespaces = false });
		return doc;
	}
}

