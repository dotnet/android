using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace MonoDroid.Tools
{
	class DefEntry
	{
		public DefEntry (string pkg, string type, string cst, string val, string api, bool isIface)
		{
			Package = pkg;
			Type = type;
			Constant = cst;
			Value = val;
			ApiLevel = api;
			IsInterface = isIface;
		}

		public string Package { get; set; }
		public string Type { get; set; }
		public string Constant { get; set; }
		public string Value { get; set; }
		public string ApiLevel { get; set; }
		public bool IsInterface { get; set; }
	}

	public class ConversionMapping2MapCsv
	{
		static readonly Dictionary<string,string> namespaces = new Dictionary<string,string> ();

		static ConversionMapping2MapCsv ()
		{
			foreach (Android.Runtime.NamespaceMappingAttribute a in
				  typeof (ConversionMapping2MapCsv).Assembly.GetCustomAttributes (typeof (Android.Runtime.NamespaceMappingAttribute), false))
				namespaces.Add (a.Java, a.Managed);
		}

		public static void Main (string [] args)
		{
			new ConversionMapping2MapCsv ().Run (args);
		}

		List<DefEntry> defs = new List<DefEntry> ();
		TextWriter output = Console.Out;

		void LoadEnums (string xmlfile)
		{
			var doc = new XmlDocument ();
			doc.Load (xmlfile);
			foreach (XmlElement pkg in doc.SelectNodes ("/enums/package"))
				foreach (XmlElement type in pkg.SelectNodes ("class | interface"))
					foreach (XmlElement cst in type.SelectNodes ("const"))
						defs.Add (new DefEntry (pkg.GetAttribute ("name"),
							type.GetAttribute ("name"),
							cst.GetAttribute ("name"),
							cst.InnerText.Trim ().Split (' ') [0].Trim (),
							cst.GetAttribute ("api-level"),
							type.LocalName == "interface"));
			Console.Error.WriteLine ("{0} defs were loaded.", defs.Count);
		}

		void Run (string [] args)
		{
			var doc = new XmlDocument ();
			doc.Load (args [0]);
			LoadEnums (args [1]);

			var mappingElems = new List<XmlElement> ();
			foreach (XmlElement map in doc.SelectNodes ("/*/map"))
				mappingElems.Add (map);
			mappingElems = mappingElems.Where (e => e.GetAttribute ("is-transient") == "false")
				.Concat (mappingElems.Where (e => e.GetAttribute ("is-transient") != "false"))
				.ToList ();

			bool enteredTransientMode = false;

			foreach (XmlElement map in mappingElems) {
				var pkg = map.GetAttribute ("package");
				var type = map.GetAttribute ("class");
				var isInterface = map.GetAttribute ("isInterface");
				var fields = map.GetAttribute ("fields");
				var prefix = map.GetAttribute ("prefix");
				var suffix = map.GetAttribute ("suffix");
				var exclude = map.GetAttribute ("exclude");
				var enn = map.GetAttribute ("enum-name");
				var extdef = map.GetAttribute ("extra-default");
				var overrides = new Dictionary<string,string> ();
				var forceApiLevel = map.GetAttribute ("force-api-level");
				foreach (XmlElement field in map.SelectNodes ("field"))
					overrides.Add (field.GetAttribute ("java"), field.GetAttribute ("managed"));
				try {
				foreach (var fld in SelectFields (pkg, type.Replace ('$', '.'), fields, prefix, suffix, exclude, extdef, isInterface)) {
					var val = fld.Value;
					string fieldName;
					if (!overrides.TryGetValue (fld.Constant, out fieldName)) {
						var f = fld.Constant.Substring (prefix.Length);
						f = f.Substring (0, f.Length - suffix.Length);
						if (Char.IsNumber (f [0]) && prefix.Length > 0)
							f = prefix [0] + f;
						fieldName = ToPascal (f);
					}
					var api = fld.ApiLevel;

					if (!enteredTransientMode && map.GetAttribute ("is-transient") != "false") {
						output.WriteLine ();
						output.WriteLine ("- ENTER TRANSIENT MODE -");
						output.WriteLine ();
						enteredTransientMode = true;
					}

					output.WriteLine ("{6},{0}{1},{2},{3}{4},{5}",
						enn.Contains ('.') ? string.Empty : CSharpPkg (pkg),
						enn.Contains ('.') ? enn : "." + enn,
						fieldName,
						fieldName != extdef && fld.IsInterface ? "I:" : "",
						fieldName == extdef ? "" : pkg.Replace ('.', '/') + '/' + type + '.' + fld.Constant,
						val,
						string.IsNullOrEmpty (forceApiLevel) ? api : forceApiLevel);
				}
				} catch (Exception ex) {
					Console.Error.WriteLine ("Error in {0} / {1} / {2} \n {3}", pkg, type, enn, ex);
					throw;
				}
			}
		}

		static readonly string [] empty = new string [] {""};

		IEnumerable<DefEntry> SelectFields (string pkg, string type, string fields, string prefix, string suffix, string exclude, string extdef, string isInterface)
		{
			if (!string.IsNullOrEmpty (extdef))
				yield return new DefEntry (pkg, type, prefix + extdef, "0", "0", isInterface.Length > 0 ? XmlConvert.ToBoolean (isInterface) : defs.First (d => d.Package == pkg && d.Type == type).IsInterface);
			foreach (var field in fields == " " ? empty : fields.Split (' ')) {
				foreach (var def in defs.Where (d => d.Package == pkg && d.Type == type && (string.IsNullOrEmpty (exclude) || !(exclude.Contains (d.Constant) || Regex.IsMatch (d.Constant, exclude))) &&
					 (field == "*" || field.IndexOf ('*') >= 0 && Regex.IsMatch (d.Constant, field) || field == "" && !string.IsNullOrEmpty (prefix) && d.Constant.StartsWith (prefix) || !string.IsNullOrEmpty (suffix) && d.Constant.EndsWith (suffix) || d.Constant == field)))
					yield return def;
			}
		}

		string CSharpPkg (string javapkg)
		{
			string managed;
			if (namespaces.TryGetValue (javapkg, out managed))
				return managed;
			string ret = String.Join (".", (from s in javapkg.Split ('.') select
				Char.ToUpper (s [0]) + s.Substring (1)).ToArray ());
			return ret;
		}

		string ToPascal (string field)
		{
			return String.Join ("", (from s in field.Split ('_') select s.Length == 0 ? "" : Char.ToUpper (s [0]) + s.Substring (1).ToLower ()).ToArray ());
		}
	}
}
