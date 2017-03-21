using System;
using System.IO;
using System.Xml;

public class Driver
{
	public static void Main (string [] args)
	{
		new Driver ().Run (args);
	}

	void Run (string [] args)
	{
		var doc = new XmlDocument ();
		doc.Load (args [0]);
		foreach (XmlElement pkg in doc.SelectNodes ("/api/package"))
			foreach (XmlElement type in pkg.SelectNodes ("class|interface"))
				foreach (XmlElement f in type.SelectNodes ("field[@type='int' and not (@value)]")) {
					Console.WriteLine ("{0}/{1}#{2}", pkg.GetAttribute ("name"), type.GetAttribute ("name"), f.GetAttribute ("name"));
				}
	}
}


