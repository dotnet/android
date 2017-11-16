using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Xamarin.Android.Tools.Bytecode
{
	public class JavaParameterNamesLoader : IJavaMethodParameterNameProvider
	{
		List<Package> packages;

		public JavaParameterNamesLoader(string path)
		{
			packages = this.LoadParameterFixupDescription(path);
		}

		class Parameter
		{
			public string Type { get; set; }
			public string Name { get; set; }
		}

		class Method
		{
			public string Name { get; set; }
			public List<Parameter> Parameters { get; set; }
		}

		class Type
		{
			public string Name { get; set; }
			public List<Method> Methods { get; set; }
		}

		class Package
		{
			public string Name { get; set; }
			public List<Type> Types { get; set; }
		}

		// from https://github.com/atsushieno/xamarin-android-docimporter-ng/blob/master/Xamarin.Android.Tools.JavaStubImporter/JavaApiParameterNamesXmlExporter.cs#L78
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
		 * 
		 */
		List<Package> LoadParameterFixupDescription (string path)
		{
			var fixup = new List<Package> ();
			string package = null;
			var types = new List<Type> ();
			string type = null;
			var methods = new List<Method> ();
			int currentLine = 0;
			foreach (var l in File.ReadAllLines (path)) {
				currentLine++;
				var line = l.IndexOf (';') >= 0 ? l.Substring (0, l.IndexOf (';')).TrimEnd (' ', '\t') : l;
				if (line.Trim ().Length == 0)
					continue;
				if (line.StartsWith ("package ", StringComparison.Ordinal)) {
					package = line.Substring ("package ".Length);
					types = new List<Type> ();
					fixup.Add (new Package { Name = package, Types = types });
					continue;
				} else if (line.StartsWith ("    ", StringComparison.Ordinal)) {
					int open = line.IndexOf ('(');
					if (open < 0)
						throw new ArgumentException ($"Unexpected line in {path} line {currentLine}: {line}");
					string parameters = line.Substring (open + 1).TrimEnd (')');
					string name = line.Substring (4, open - 4);
					if (name.FirstOrDefault () == '<') // generic method can begin with type parameters.
						name = name.Substring (name.IndexOf (' ') + 1);
					methods.Add (new Method {
						Name = name,
						Parameters = parameters.Replace (", ", "\0").Split ('\0')
								       .Select (s => s.Split (' '))
						                       .Select (a => new Parameter { Type = string.Join (" ", a.Take (a.Length - 1)).Replace (",", ", "), Name = a.Last () }).ToList ()
					});
				} else {
					type = line.Substring (line.IndexOf (' ', 2) + 1);
					// To match type name from class-parse, we need to strip off generic arguments here (generics are erased).
					if (type.IndexOf ('<') > 0)
						type = type.Substring (0, type.IndexOf ('<'));
					methods = new List<Method> ();
					types.Add (new Type { Name = type, Methods = methods });
				}
			}
			return fixup;
		}

		public string[] GetParameterNames (string package, string type, string method, string[] ptypes, bool isVarArgs)
		{
			var methods = this.packages
				.Where(p => p.Name == package)
				.SelectMany(p => p.Types)
				.Where(t => t.Name == type)
				.SelectMany(t => t.Methods)
				.Where(m => m.Name == method);
			var namedMethod = methods.FirstOrDefault (m => ParametersEqual (m.Parameters, ptypes));
			if (namedMethod == null)
				return null;
			return namedMethod.Parameters.Select (p => p.Name).ToArray ();
		}

		static bool ParametersEqual (List<Parameter> methodParameters, string[] ptypes)
		{
			if (methodParameters.Count != ptypes.Length)
				return false;
			for (int i = 0; i < ptypes.Length; ++i) {
				if (methodParameters[i].Type != ptypes [i])
					return false;
			}
			return true;
		}
    }
}
