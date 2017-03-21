using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;

public class Driver
{
	public static void Main (string [] args)
	{
		new Driver ().Run (args);
	}

	const string android_ns = "http://schemas.android.com/apk/res/android";

	List<string> atts = new List<string> ();
	TextWriter fs1;

	TextWriter output = Console.Out;
	string doc_base;

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
		s = s.Replace ("<!-- /New Search>", "<!-- /New Search -->"); // Why?!
		s = s.Replace ("<application ...=\"\">", "<application xxx=\"\">"); // ...
		doc.Load (new XmlTextReader (new StringReader (s)) { Namespaces = false });
		return doc;
	}

	string [] entries;

	public void Run (string [] args)
	{
		doc_base = args [0];
		entries = File.ReadAllText (args [1]).Split ('\n');

		var doc = GetDocument ("packages.html");

		foreach (XmlElement package in doc.SelectNodes ("//div[@id='packages-nav']/ul/li")) {
			string pkgName = package.InnerText.Replace ('.', '/');

			if (pkgName.StartsWith ("org"))
				continue;
			
			string pkgfile = pkgName + "/" + "package-summary.html";
			var pkgdoc = GetDocument (pkgfile);
			foreach (XmlElement classes in pkgdoc.SelectNodes ("//div[@id='classes-nav' or @id='interfaces-nav']/ul/li")) {
				bool isInterface = classes.SelectSingleNode ("h2").InnerText == "Interfaces";
				foreach (XmlElement cls in classes.SelectNodes ("ul/li"))
					ProcessClass (cls, pkgName, isInterface);
			}
		}
	}
	
	void ProcessClass (XmlElement cls, string pkgName, bool isInterface)
	{
		string className = cls.InnerText;
		string classNameNonGeneric = className.IndexOf ('<') > 0 ? className.Substring (0, className.IndexOf ('<')) : className;
		var filename = pkgName + "/" + classNameNonGeneric + ".html";

		if (filename.StartsWith ("android/R")) return;

		string text = File.ReadAllText (Path.Combine (doc_base, filename));
		foreach (var entry in entries) {
			if (entry == "") continue;

			string [] tokens = entry.Split (' ');
			if (tokens.Length != 3) {
				Console.Error.WriteLine ("!!!!!! WARNING: wrong entry : " + entry);
				continue;
			}
			string file = tokens [0];
			string name = tokens [1];
			if (filename != file && text.IndexOf (file + "#" + name) > 0)
				output.WriteLine ("{0} mentions {1}", filename, entry.Replace (' ', '#'));
		}
	}
}
