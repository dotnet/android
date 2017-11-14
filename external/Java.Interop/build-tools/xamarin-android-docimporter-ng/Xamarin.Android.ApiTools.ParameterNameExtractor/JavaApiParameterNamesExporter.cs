using System;
using System.IO;
using System.Linq;
using System.Xml;
using Xamarin.Android.Tools.ApiXmlAdjuster;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public static class JavaApiParameterNamesExporter
	{
		public static void WriteParameterNamesXml (this JavaApi api, string file)
		{
			using (var xw = XmlWriter.Create (file, new XmlWriterSettings { Indent = true }))
				WriteParameterNamesXml (api, xw);
		}

		public static void WriteParameterNamesXml (this JavaApi api, XmlWriter writer)
		{
			writer.WriteStartElement ("parameter-names");

			Action<JavaTypeParameters> writeTypeParameters = tps => {
				if (tps != null && tps.TypeParameters.Any ()) {
					writer.WriteStartElement ("type-parameters");
					foreach (var gt in tps.TypeParameters) {
						writer.WriteStartElement ("type-parameter");
						writer.WriteAttributeString ("name", gt.Name);
						// no need to supply constraints.
						writer.WriteEndElement ();
					}
					writer.WriteEndElement ();
				}
			};

			foreach (var package in api.Packages) {
				writer.WriteStartElement ("package");
				writer.WriteAttributeString ("name", package.Name);

				foreach (var type in package.Types) {
					if (!type.Members.OfType<JavaMethodBase> ().Any (m => m.Parameters.Any ()))
						continue; // we care only about types that has any methods that have parameters.
					
					writer.WriteStartElement (type is JavaClass ? "class" : "interface");
					writer.WriteAttributeString ("name", type.Name);

					writeTypeParameters (type.TypeParameters);

					// we care only about methods that have parameters.
					foreach (var mb in type.Members.OfType<JavaMethodBase> ().Where (m => m.Parameters.Any ())) {
						if (mb is JavaConstructor)
							writer.WriteStartElement ("constructor");
						else {
							writer.WriteStartElement ("method");
							writer.WriteAttributeString ("name", mb.Name);
						}

						writeTypeParameters (mb.TypeParameters);

						foreach (var para in mb.Parameters) {
							writer.WriteStartElement ("parameter");
							// For possible generic instances in parameter type, we replace all ", " with "," to ease parsing.
							writer.WriteAttributeString ("type", para.Type.Replace (", ", ","));
							writer.WriteAttributeString ("name", para.Name);
							writer.WriteEndElement ();
						}

						writer.WriteEndElement ();
					}

					writer.WriteEndElement ();
				}

				writer.WriteEndElement ();
			}

			writer.WriteEndElement ();
		}


/*
 * The Text Format is:
 * 
 * package {packagename}
 * ;---------------------------------------
 *   interface {interfacename}{optional_type_parameters} -or-
 *   class {classname}{optional_type_parameters}
 *     {optional_type_parameters}{methodname}({parameters})
 * 
 * Anything after ; is treated as comment.
 * 
 * optional_type_parameters: "" -or- "<A,B,C>" (no constraints allowed)
 * parameters: type1 p0, type2 p1 (pairs of {type} {name}, joined by ", ")
 * 
 * It is with strict indentations. two spaces for types, four spaces for methods.
 * 
 * Constructors are named as "#ctor".
 * 
 * Commas are used by both parameter types and parameter separators,
 * but only parameter separators can be followed by a whitespace.
 * It is useful when writing text parsers for this format.
 * 
 * Type names may contain whitespaces in case it is with generic constraints (e.g. "? extends FooBar"),
 * so when parsing a parameter type-name pair, the only trustworthy whitespace for tokenizing name is the *last* one.
 */

		public static void WriteParameterNamesText (this JavaApi api, string file)
		{
			using (var sw = new StreamWriter (file))
				WriteParameterNamesText (api, sw);
		}

		public static void WriteParameterNamesText (this JavaApi api, TextWriter writer)
		{
			Action<string,JavaTypeParameters> writeTypeParameters = (indent, tps) => {
				if (tps != null && tps.TypeParameters.Any ())
					writer.Write ($"{indent}<{string.Join (",", tps.TypeParameters.Select (p => p.Name))}>");
			};

			foreach (var package in api.Packages) {
				writer.WriteLine ();
				writer.WriteLine ($"package {package.Name}");
				writer.WriteLine (";---------------------------------------");

				foreach (var type in package.Types) {
					if (!type.Members.OfType<JavaMethodBase> ().Any (m => m.Parameters.Any ()))
						continue; // we care only about types that has any methods that have parameters.
					writer.Write (type is JavaClass ? "  class " : "  interface ");
					writer.Write (type.Name);
					writeTypeParameters ("", type.TypeParameters);
					writer.WriteLine ();

					// we care only about methods that have parameters.
					foreach (var mb in type.Members.OfType<JavaMethodBase> ().Where (m => m.Parameters.Any ())) {
						writer.Write ("   ");
						writeTypeParameters (" ", mb.TypeParameters);
						var name = mb is JavaConstructor ? "#ctor" : mb.Name;
						// For possible generic instances in parameter type, we replace all ", " with "," to ease parsing.
						writer.WriteLine ($" {name}({string.Join (", ", mb.Parameters.Select (p => p.Type.Replace (", ", ",") + ' ' + p.Name))})");
					}
				}
			}
		}
	}
}
