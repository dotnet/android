using System;
using System.Collections.Concurrent;
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
			CSharpDir = csdir;
			JavaDir = javadir;
			Assembly = assembly;
		}

		public string Assembly { get; }
		public string CSharpDir { get; }
		public string JavaDir { get; }
		public ConcurrentBag<string> GeneratedFiles { get; } = new ConcurrentBag<string> ();
		public ConcurrentBag<string> Enums { get; } = new ConcurrentBag<string> ();
		public ConcurrentBag<KeyValuePair<string, string>> TypeRegistrations { get; } = new ConcurrentBag<KeyValuePair<string, string>> ();

		public StreamWriter OpenStream (string name) 
		{
			if (!Directory.Exists (CSharpDir))
				Directory.CreateDirectory (CSharpDir);
			string filename = Path.Combine (CSharpDir, name + ".cs");
			
			var sw = new StreamWriter (File.Create (filename));
			GeneratedFiles.Add (filename);
			return sw;
		}

		internal void GenerateLibraryProjectFile (CodeGeneratorOptions options, IEnumerable<string> enumFiles, string path = null)
		{
			if (path == null) {
				var     name    = Assembly ?? "GeneratedFiles";
				int     idx     = name.IndexOf (',');
				name            = idx < 0 ? name : name.Substring (0, idx);
				path            = Path.Combine (CSharpDir, name + ".projitems");
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
				.Replace ('/', '\\')
				.Replace ("$(MSBuildThisFileDirectory)\\", "$(MSBuildThisFileDirectory)");

			return new XElement (compile, new XAttribute ("Include", path));
		}
	}
}

