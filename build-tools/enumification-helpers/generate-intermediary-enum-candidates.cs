using System;
using System.IO;
using System.Xml;

namespace MonoDroid.Tools
{
	public static class FieldMatcher
	{
		public static void Main (string [] args)
		{
			var output = Console.Out;

			var doc = new XmlDocument ();
			doc.Load (args [0]);
			output.WriteLine ("<matches>");
			foreach (XmlElement pkg in doc.SelectNodes ("/api/package")) {
			foreach (XmlElement type in pkg.SelectNodes ("class|interface")) {
				string prev = null;
				foreach (XmlElement f in type.SelectNodes ("field[@value and @type='int']")) {
					var n = f.SelectSingleNode ("following-sibling::field[@value and @type='int']") as XmlElement;
					if (n == null)
						continue;
					var cn = f.GetAttribute ("name");
					var nn = n.GetAttribute ("name");
					var idx1 = cn.IndexOf ('_');
					var idx2 = nn.IndexOf ('_');
					if (idx1 < 0 || idx2 < 0)
						continue;
					var sub1 = cn.Substring (0, idx1);
					if (sub1 != nn.Substring (0, idx2) || prev == sub1)
						continue;
					prev = sub1;
					output.WriteLine ("<map package='{0}' class='{1}' fields='' prefix='{2}_' />",
						pkg.GetAttribute ("name"),
						type.GetAttribute ("name").Replace ('.', '$'),
						sub1);
				}
			}
			}
			output.WriteLine ("</matches>");
		}
	}
}

