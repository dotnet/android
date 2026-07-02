using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MonoDroid.Generation
{
	public class ApiTransform
	{
		public bool PreserveType { get; set; }
		public string Version { get; set; }
		public string Package { get; set; }
		public string Class { get; set; }
		public string Member { get; set; }
		public string Parameter { get; set; }
		public string Enum { get; set; }

		public ApiTransform (bool preserveType, string[] args)
		{
			PreserveType = preserveType;
			Version = args[0].Trim ();
			Package = args[1].Trim ();
			Class = args[2].Trim ();
			Member = args[3].Trim ();
			Parameter = args[4].Trim ();
			Enum = args[5].Trim ();
		}

		public void WriteTransform (StreamWriter sw)
		{
			string preserveAttr = PreserveType ? " preserveType=\"true\"" : null;

			string cls = Class.StartsWith ("[Interface]", StringComparison.Ordinal) ? Class.Substring ("[Interface]".Length) : Class;

			if (string.IsNullOrEmpty (Parameter)) {
				// This is a field
				sw.WriteLine ("  <attr path=\"/api/package[@name='{0}']/*[@name='{1}']/field[@name='{2}']\" name=\"enumType\"{4}>{3}</attr>", Package, Class, Member, Enum, preserveAttr);		
			} else if (Parameter == "return") {
				// This is the return type
				sw.WriteLine ("  <attr path=\"/api/package[@name='{0}']/*[@name='{1}']/method[@name='{2}']\" name=\"enumReturn\"{4}>{3}</attr>", Package, cls, Member, Enum, preserveAttr);
			} else if (Member == "ctor" || Member == "constructor") {
				// This is the return type
				sw.WriteLine ("  <attr path=\"/api/package[@name='{0}']/*[@name='{1}']/constructor/parameter[@name='{2}']\" name=\"enumType\"{4}>{3}</attr>", Package, cls, Parameter, Enum, preserveAttr);
			} else {
				// This is a parameter
				sw.WriteLine ("  <attr path=\"/api/package[@name='{0}']/*[@name='{1}']/method[@name='{2}']/parameter[@name='{3}']\" name=\"enumType\"{5}>{4}</attr>", Package, cls, Member, Parameter, Enum, preserveAttr);
			}
		}
	}
}
