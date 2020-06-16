using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.JavaCallableWrappers;
using Mono.Cecil;
using Mono.Options;

namespace Java.Interop.Tools
{
	class App
	{
		public static int Main (string [] args)
		{
			var     resolver    = new DirectoryAssemblyResolver (logger: Diagnostic.CreateConsoleLogger (), loadDebugSymbols: false);

			bool    help        = false;
			string  outputPath  = null;
			int     verbosity   = 0;

			var options = new OptionSet {
				"Usage: jcw-gen.exe OPTIONS* ASSEMBLY+ [@RESPONSE-FILES]",
				"",
				"Generates Java Callable Wrappers from specified assemblies.",
				"",
				"Copyright 2016 Xamarin Inc.",
				"",
				"Options:",
				{ "L=",
				  "{DIRECTORY} to resolve assemblies from.",
				  v => resolver.SearchDirectories.Add (v) },
				{ "o=",
				  "{DIRECTORY} to write Java source code to.",
				  v => outputPath = v },
				{ "v:",
				  "Logging verbosity.",
				  (int? v) => verbosity = v.HasValue ? v.Value : verbosity + 1 },
				{ "h|help|?",
				  "Show this message and exit",
				  v => help = v != null },
				new ResponseFileSource (),
			};
			var cache = new TypeDefinitionCache ();
			var scanner = new JavaTypeScanner (Diagnostic.CreateConsoleLogger (), cache);
			try {
				var assemblies = options.Parse (args);
				if (assemblies.Count == 0 || outputPath == null || help) {
					int r = 0;
					if (assemblies.Count == 0) {
						Console.Error.WriteLine ("jcw-gen: No assemblies specified.");
						r = 1;
					}
					else if (outputPath == null) {
						Console.Error.WriteLine ("jcw-gen: No output directory specified. Use `jcw-gen -o PATH`.");
						r = 1;
					}
					options.WriteOptionDescriptions (Console.Out);
					return r;
				}
				foreach (var assembly in assemblies) {
					resolver.SearchDirectories.Add (Path.GetDirectoryName (assembly));
					resolver.Load (assembly);
				}
				var types = scanner.GetJavaTypes (assemblies, resolver)
					.Where (td => !JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (td, cache));
				foreach (var type in types) {
					GenerateJavaCallableWrapper (type, outputPath, cache);
				}
				return 0;
			}
			catch (Exception e) {
				Console.Error.Write ("jcw-gen: {0}", verbosity > 0 ? e.ToString () : e.Message);
				return 1;
			}
			finally {
				resolver.Dispose ();
			}
		}

		static void GenerateJavaCallableWrapper (TypeDefinition type, string outputPath, TypeDefinitionCache cache)
		{
			var generator = new JavaCallableWrapperGenerator (type, log: Console.WriteLine, cache) {
			};
			generator.Generate (outputPath);
		}
	}
}
