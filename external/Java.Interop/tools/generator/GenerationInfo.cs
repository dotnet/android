using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Xamarin.Android.Binder;

namespace MonoDroid.Generation {

	public class GenerationInfo {

		public GenerationInfo (string csdir, string javadir, string assembly)
		{
			this.csdir = csdir;
			this.javadir = javadir;
			this.assembly = assembly;
		}

		string assembly;
		public string Assembly {
			get { return assembly; }
		}

		string csdir;
		public string CSharpDir {
			get { return csdir; }
		}

		string javadir;
		public string JavaDir {
			get { return javadir; }
		}

		string member;
		public string CurrentMember {
			get { return typename + "." + member; }
			set { member = value; }
		}

		string typename;
		public string CurrentType {
			get { return typename; }
			set { typename = value; }
		}

		StreamWriter sw;
		public StreamWriter Writer {
			get { return sw; }
			set { sw = value; }
		}
		
		List<string> generated_files = new List<string> ();
		public IEnumerable<string> GeneratedFiles {
			get { return generated_files; }
		}

		public StreamWriter OpenStream (string name) 
		{
			if (!Directory.Exists(csdir))
				Directory.CreateDirectory (csdir);
			string filename = Path.Combine (csdir, name + ".cs");
			
			sw = new StreamWriter (File.Create (filename));
			generated_files.Add (filename);
			return sw;
		}

		List<string> enums = new List<string> ();
		public ICollection<string> Enums {
			get { return enums; }
		}

		List<KeyValuePair<string, string>> type_registrations = new List<KeyValuePair<string, string>> ();
		public ICollection<KeyValuePair<string, string>> TypeRegistrations {
			get { return type_registrations; }
		}

		internal void GenerateLibraryProjectFile (CodeGeneratorOptions options, IEnumerable<string> enumFiles, string path = null)
		{
			if (path == null) {
				var     name    = Assembly ?? "GeneratedFiles";
				int     idx     = name.IndexOf (',');
				name            = idx < 0 ? name : name.Substring (0, idx);
				path            = Path.Combine (csdir, name + ".csproj");
			}

			var msbuild = XNamespace.Get ("http://schemas.microsoft.com/developer/msbuild/2003");
			var compile = msbuild + "Compile";
			var project = new XElement (
				msbuild + "Project",
				ToDefineConstants (options, msbuild),
				new XComment (" Classes "),
				new XElement (
					msbuild + "ItemGroup",
					GeneratedFiles
						.OrderBy (f => f, StringComparer.OrdinalIgnoreCase)
						.Select (f => ToCompileElement (compile, f))),
				new XComment (" Enums "),
				new XElement (
					msbuild + "ItemGroup",
					enumFiles
						?.OrderBy (f => f, StringComparer.OrdinalIgnoreCase)
						?.Select (f => ToCompileElement (compile, f))));

			project.Save (path);
		}

		XElement ToDefineConstants (CodeGeneratorOptions options, XNamespace msbuild)
		{
			if (options.ApiLevel == null)
				return null;
			int level;
			if (!int.TryParse (options.ApiLevel, out level))
				return null;
			var defines = new StringBuilder ()
				.Append ("$(DefineConstants);ANDROID_1");
			for (int i = 2; i <= level; ++i) {
				defines.AppendFormat (";ANDROID_{0}", i.ToString ());
			}
			return new XElement (
				msbuild + "PropertyGroup",
				new XElement (
					msbuild + "DefineConstants",
					defines));
		}

		XElement ToCompileElement (XName compile, string path)
		{
			path = path.Replace (CSharpDir, "$(MSBuildThisFileDirectory)")
				.Replace ('/', '\\');
			return new XElement (compile, new XAttribute ("Include", path));
		}
	}
}

