using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text;

using Mono.Options;

using Xamarin.Android.Tools.Bytecode;

namespace Xamarin.Android.Tools {

	class App {

		public static void Main (string[] args)
		{
			bool dump       = false;
			bool help       = false;
			bool docsType   = false;
			int  verbosity  = 0;
			bool autorename = false;
			var  outputFile = (string) null;
			string platform = null;
			var  docsPaths  = new List<string> ();
			var p = new OptionSet () {
				"usage: class-dump [-dump] FILES [@RESPONSE-FILES]",
				"",
				"View the metadata contents of a Java .class or .jar file.",
				"",
				"Options:",
				{ "dump",
				  "Dump out .class metadata, including constant pool.\nDefault is XML output.",
				  v => dump = v != null },
				{ "o=",
				  "Write output to {PATH}.",
				  v => outputFile = v },
				{ "docspath=",
				  "Documentation {PATH} for parameter fixup",
				  doc => docsPaths.Add (doc) },
				{ "parameter-names=",
				  "{PATH} for Java method parameter name information",
				  doc => docsPaths.Add (doc) },
				{ "docstype=",
				  "OBSOLETE: Previously used to specify a doc type (now auto detected).",
				  t => docsType = t != null },
				{ "v|verbose:",
				  "See stack traces on error.",
				  (int? v) => verbosity = v.HasValue ? v.Value : verbosity + 1 },
				{ "autorename",
				  "Renames parameter names in the interfaces by derived classes.",
				  v => autorename = v != null },
				{ "platform=",
				  "(Internal use only) specify Android framework platform ID",
				  v => platform = v },
				{ "h|?|help",
				  "Show this message and exit.",
				  v => help = v != null },
				new ResponseFileSource (),
			};
			var files = p.Parse (args);
			if (help) {
				p.WriteOptionDescriptions (Console.Out);
				return;
			}
			if (docsType)
				Console.WriteLine ("class-parse: --docstype is obsolete and no longer a valid option.");
			var output = outputFile == null
				? Console.Out
				: (TextWriter) new StreamWriter (outputFile, append: false, encoding: new UTF8Encoding (encoderShouldEmitUTF8Identifier: false));
			Log.OnLog = (t, v, m, a) => {
				Console.Error.WriteLine(m, a);
			};
			var globalClassPath = CreateClassPath (platform, docsPaths, autorename);
			var classPaths      = new List<ClassPath> ();
			foreach (var file in files) {
				try {
					if (ClassPath.IsJmodFile (file) || ClassPath.IsJarFile (file)) {
						var cp = CreateClassPath (platform, docsPaths, autorename);
						cp.Load (file);
						classPaths.Add (cp);
						continue;
					}
					using (var s = File.OpenRead (file)) {
						if (!ClassFile.IsClassFile (s)) {
							Console.Error.WriteLine ("class-parse: Unable to read file '{0}': Unknown file format.");
							Environment.ExitCode    = 1;
							continue;
						}
						globalClassPath.Add (new ClassFile (s));
					}
				} catch (Exception e) {
					Console.Error.WriteLine ("class-parse: Unable to read file '{0}': {1}",
							file, verbosity == 0 ? e.Message : e.ToString ());
					Environment.ExitCode    = 1;
				}
			}
			globalClassPath.FixupModuleVisibility (removeModules: !dump);
			foreach (var cp in classPaths) {
				globalClassPath.Add (cp, removeModules: !dump);
			}
			if (!dump) {
				globalClassPath.SaveXmlDescription (output);
			} else {
				bool first = true;
				foreach (var c in globalClassPath.GetClassFiles ()) {
					if (!first) {
						output.WriteLine ();
					}
					first = false;
					DumpClassFile (c, output);
				}
			}
			if (outputFile != null)
				output.Close ();
		}

		static ClassPath CreateClassPath (string platform, List<string> docsPaths, bool autoRename)
		{
			return new ClassPath () {
				ApiSource                   = "class-parse",
				AndroidFrameworkPlatform    = platform,
				DocumentationPaths          = docsPaths.Count == 0 ? null : docsPaths,
				AutoRename                  = autoRename
			};
		}

		static void DumpClassFile (ClassFile c, TextWriter output)
		{
			output.WriteLine ($"-- Begin {c.FullJniName} --");
			output.WriteLine (".class version: {0}.{1}", c.MajorVersion, c.MinorVersion);
			output.WriteLine ("ConstantPool Count: {0}", c.ConstantPool.Count);
			for (int i = 0; i < c.ConstantPool.Count; ++i) {
				output.WriteLine ("\t{0}: {1}", i, c.ConstantPool [i]);
			}
			output.WriteLine ("ThisClass: {0}", c.ThisClass.Name);
			output.WriteLine ("SuperClass: {0}", c.SuperClass?.Name);
			output.WriteLine ("AccessFlags: {0}", c.AccessFlags);
			output.WriteLine ("Attributes Count: {0}", c.Attributes.Count);
			for (int i = 0; i < c.Attributes.Count; ++i) {
				output.WriteLine ("\t{0}: {1}", i, c.Attributes [i]);
			}
			output.WriteLine ("Interfaces Count: {0}", c.Interfaces.Count);
			for (int i = 0; i < c.Interfaces.Count; ++i) {
				output.WriteLine ("\t{0}: {1}", i, c.Interfaces [i].Name.Value);
			}
			output.WriteLine ("Fields Count: {0}", c.Fields.Count);
			for (int i = 0; i < c.Fields.Count; ++i) {
				output.WriteLine ("\t{0}: {1} {2} {3}", i, c.Fields [i].Name, c.Fields [i].Descriptor, c.Fields [i].AccessFlags);
				foreach (var attr in c.Fields [i].Attributes) {
					output.WriteLine ("\t\t{0}", attr);
				}
			}
			output.WriteLine ("Methods Count: {0}", c.Methods.Count);
			for (int i = 0; i < c.Methods.Count; ++i) {
				output.WriteLine ("\t{0}: {1} {2} {3}", i, c.Methods [i].Name, c.Methods [i].Descriptor, c.Methods [i].AccessFlags);
				foreach (var attr in c.Methods [i].Attributes) {
					output.WriteLine ("\t\t{0}", attr);
				}
			}
		}
	}
}
