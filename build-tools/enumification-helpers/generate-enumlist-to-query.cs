using System;
using System.IO;
using System.Xml;

public class Test
{
	public static void Main (string [] args)
	{
		var doc = new XmlDocument ();
		doc.Load (args [0]);
		foreach (XmlElement p in doc.SelectNodes ("/enums/package"))
			foreach (XmlElement cls in p.SelectNodes ("class"))
				foreach (XmlElement cst in cls.SelectNodes ("const")) {
					string file = p.GetAttribute ("name").Replace ('.', '/') + '/' + cls.GetAttribute ("name") + ".html";
					string constName = cst.GetAttribute ("name");
					string value = cst.InnerText.Replace ('\n', ' ').Trim ().Split (' ') [0].Trim ();
					Console.WriteLine ("{0} {1} {2}", file, constName, value);
				}
	}
}

