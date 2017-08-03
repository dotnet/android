using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

	bool verbose;

	XmlDocument GetDocument (string file)
	{
		try {
			return DoGetDocument (file);
		} catch (Exception ex) {
			if (verbose)
				Console.Error.WriteLine ("Processing " + file);
			throw;
		}
	}

	XmlDocument DoGetDocument (string file)
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
		s = s.Replace ("<!-- /New Search>", "<!-- /New Search -->"); // Why, Google. Why.
		s = s.Replace ("<application ...=\"\">", "<application xxx=\"\">"); // ...
		s = s.Replace (@" ..="""">setChannelMask(int)</a>",  @" __="""">setChannelMask(int)</a>"); // Really?!
		s = s.Replace (@" ...=""""", @" __=""...""");
		doc.Load (new XmlTextReader (new StringReader (s)) { Namespaces = false });
		return doc;
	}

	public void Run (string [] args)
	{
		output.WriteLine ("<enums>");
		doc_base = args [0];
		verbose = args.Any (a => a == "-v" || a == "--verbose");

		foreach (var rawPkgName in File.ReadAllLines (Path.Combine (Path.GetFullPath (doc_base), "packages.html"))) {
			if (rawPkgName.Length == 0)
				continue;
			if (rawPkgName.StartsWith ("com.android.internal"))
				continue;

			output.WriteLine ("  <package name='{0}'>", rawPkgName);
			string pkgName = rawPkgName.Replace ('.', '/');

			if (pkgName.StartsWith ("org") && pkgName != "org/xmlpull/v1") {
				output.WriteLine ("  </package>");
				continue;
			}
			
Console.Error.WriteLine ("Package " + pkgName);
			string pkgfile = pkgName + "/" + "package-summary.html";
			var pkgdoc = GetDocument (pkgfile);
			foreach (XmlElement classes in GetTypeNodes (pkgdoc)) {
				bool isInterface = classes.SelectSingleNode ("h2").InnerText == "Interfaces";
Console.Error.WriteLine ("Class/Interface");
				foreach (XmlElement cls in classes.SelectNodes ("ul/li"))
					ProcessClass (cls, pkgName, isInterface);
			}
			output.WriteLine ("  </package>");
		}
		output.WriteLine ("</enums>");
	}
	
	IEnumerable<XmlElement> GetTypeNodes (XmlDocument pkgdoc)
	{
		foreach (XmlElement e in pkgdoc.SelectNodes ("//div[@id='classes-nav' or @id='interfaces-nav']/ul/li"))
			yield return e;
		foreach (XmlElement e in pkgdoc.SelectNodes ("//ul[@data-reference-resources]/li"))
			yield return e;
	}
	
	void ProcessClass (XmlElement cls, string pkgName, bool isInterface)
	{
		StringWriter output = new StringWriter ();
		bool existed = false;

		string className = cls.InnerText;
		string classNameNonGeneric = className.IndexOf ('<') > 0 ? className.Substring (0, className.IndexOf ('<')) : className;
Console.Error.WriteLine ("  Class " + className + " from " + pkgName + "/" + classNameNonGeneric + ".html");
		output.WriteLine ("    <{0} name='{1}' api-level='{2}'>", isInterface ? "interface" : "class", classNameNonGeneric, cls.GetAttribute ("class").Substring ("api apilevel-".Length));
		var clsdoc = GetDocument (pkgName + "/" + classNameNonGeneric + ".html");
		var constLinks = clsdoc.SelectNodes ("//h2[text()='Constants']/following-sibling::a[@name]");
		var values = new Dictionary<string,XmlElement> ();
		foreach (XmlElement constLink in constLinks) {
			XmlElement details = constLink.SelectSingleNode ("following-sibling::div[1]") as XmlElement;
			values [constLink.GetAttribute ("name")] = details;
		}
		var consts = clsdoc.SelectSingleNode ("//table[@id='constants'][1]");
		if (consts == null)
			Console.Error.WriteLine ("No constants in " + className);
		else {
			foreach (XmlElement constr in consts.SelectNodes ("tr[@class]")) {
				string constClassAtt = constr.GetAttribute ("class");
				string constType = constr.SelectSingleNode ("td[1]").InnerText;

				if (constType != "int")
					continue;

				string constName = (constr.SelectSingleNode ("td[2]/code") ?? constr.SelectSingleNode ("td[2]")).InnerText;
				var descDiv = values [constName];
				var apiLevel = constClassAtt.Substring (constClassAtt.LastIndexOf ('-') + 1);
				// FIXME: this is really hacky. Since Google made buggy changes to their HTML documents and their format miss correct API level for new stuff (or whatever), it now does not fill API Level there.
				if (string.IsNullOrEmpty (apiLevel))
					apiLevel = "24";
				string value;
				var valueSpan =
							descDiv.SelectSingleNode (".//div[@class='jd-tagdata']/span[text()='Constant Value: ']/following-sibling::span");
				if (valueSpan != null)
					value = valueSpan.InnerText.Trim ();
				else {
					valueSpan = descDiv.SelectSingleNode (".//p[contains (text(), 'Constant Value:')]");
					var splitValues = valueSpan.InnerText.Replace (" ", string.Empty).Split (new char [] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
					value = splitValues [1];
				}
				output.WriteLine ("      <const type='{0}' name='{1}' api-level='{2}'>{3}</const>",
					constType,
					constName,
					apiLevel,
					value);
				existed = true;
			}
		}
		output.WriteLine ("    </{0}>", isInterface ? "interface" : "class");

		output.Close ();		
		if (existed)
			this.output.Write (output);
	}
}
