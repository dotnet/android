using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Irony.Parsing;

using Java.Interop.Tools.JavaSource;

using Xamarin.Android.Tools.ApiXmlAdjuster;

namespace Xamarin.Android.ApiTools.JavaStubImporter
{
	public class JavaStubSourceImporter
	{
		public void Import (ImporterOptions options)
		{
			ZipArchive zip;
			using (var stream = File.OpenRead (options.InputZipArchive)) {
				zip = new ZipArchive (stream);
				foreach (var ent in zip.Entries) {
					options.DiagnosticWriter.WriteLine (ent.FullName);
					if (!ent.Name.EndsWith (".java", StringComparison.OrdinalIgnoreCase))
						continue;
					var java = new StreamReader (ent.Open ()).ReadToEnd ();
					if (!ParseJava (java))
						break;
				}
			}
			foreach (var pkg in api.Packages) {
				foreach (var t in pkg.Types) {
					// Our API definitions don't contain non-public members, so remove those (but it does contain non-public types).
					t.Members = t.Members.Where (m => m != null && m.Visibility != "").ToList ();
					// Constructor "type" is the full name of the class.
					foreach (var c in t.Members.OfType<JavaConstructor> ())
						c.Type = (pkg.Name.Length > 0 ? (pkg.Name + '.') : string.Empty) + t.Name;
					// Pupulated enum fields need type to be filled
					var cls = t as JavaClass;
					if (cls != null && cls.Extends == "java.lang.Enum") {
						cls.ExtendsGeneric = "java.lang.Enum<" + pkg.Name + "." + t.Name + ">";
						foreach (var m in cls.Members.OfType<JavaField> ()) {
							if (m.Type == null) {
								m.Type = pkg.Name + "." + t.Name;
								m.TypeGeneric = pkg.Name + "." + t.Name;
							}
						}
						foreach (var m in cls.Members.OfType<JavaMethod> ()) {
							if (m.Name == "valueOf")
								m.Return = pkg.Name + "." + t.Name;
							else if (m.Name == "values")
								m.Return = pkg.Name + "." + t.Name + "[]";
						}
					}
					t.Members = t.Members.OfType<JavaMethodBase> ()
						.OrderBy (m => m.Name + "(" + string.Join (",", m.Parameters.Select (p => p.Type)) + ")")
						.ToArray ();
				}
				pkg.Types = pkg.Types.OrderBy (t => t.Name).ToArray ();
			}
			api.Packages = api.Packages.OrderBy (p => p.Name).ToArray ();

			if (options.OutputTextFile != null)
				api.WriteParameterNamesText (options.OutputTextFile);
			if (options.OutputXmlFile != null)
				api.WriteParameterNamesXml (options.OutputXmlFile);
		}

		JavaStubParser  parser = new JavaStubParser ();
		JavaApi api = new JavaApi ();

		bool ParseJava (string javaSourceText)
		{
			var parsedPackage = parser.TryParse (javaSourceText, out var parseTree);
			foreach (var m in parseTree.ParserMessages) {
				Console.WriteLine ($"{m.Level} {m.Location} {m.Message}");
			}
			if (parsedPackage == null) {
				return false;
			}
			var pkg = api.Packages.FirstOrDefault (p => p.Name == parsedPackage.Name);
			if (pkg == null) {
				api.Packages.Add (parsedPackage);
				pkg = parsedPackage;
			} else
				foreach (var t in parsedPackage.Types)
					pkg.Types.Add (t);
			pkg.Types = pkg.Types.OrderBy (t => t.Name).ToList ();
			return true;
		}
	}
}
