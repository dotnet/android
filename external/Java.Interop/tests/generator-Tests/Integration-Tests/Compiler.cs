using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace generatortests
{
	public static class Compiler
	{
		private static string supportFilePath = typeof (Compiler).Assembly.Location;
		private static string unitTestFrameworkAssemblyPath = typeof (Assert).Assembly.Location;

		public static Assembly Compile (Xamarin.Android.Binder.CodeGeneratorOptions options,
			string assemblyFileName, IEnumerable<string> AdditionalSourceDirectories,
			out bool hasErrors, out string output, bool allowWarnings)
		{
			// Gather all the files we need to compile
			var generatedCodePath = options.ManagedCallableWrapperSourceOutputDirectory;
			var sourceFiles = Directory.EnumerateFiles (generatedCodePath, "*.cs",
				SearchOption.AllDirectories).ToList ();
			sourceFiles = sourceFiles.Select (x => Path.GetFullPath (x)).ToList ();

			var supportFiles = Directory.EnumerateFiles (Path.Combine (Path.GetDirectoryName (supportFilePath), "SupportFiles"),
				"*.cs", SearchOption.AllDirectories);
			sourceFiles.AddRange (supportFiles);

			foreach (var dir in AdditionalSourceDirectories) {
				var additonal = Directory.EnumerateFiles (dir, "*.cs", SearchOption.AllDirectories);
				sourceFiles.AddRange (additonal);
			}

			// Parse the source files
			var syntax_trees = sourceFiles.Distinct ().Select (s => CSharpSyntaxTree.ParseText (File.ReadAllText (s))).ToArray ();

			// Set up the assemblies we need to reference
			var binDir = Path.GetDirectoryName (typeof (BaseGeneratorTest).Assembly.Location);
			var facDir = GetFacadesPath ();

			var references = new [] {
				MetadataReference.CreateFromFile (unitTestFrameworkAssemblyPath),
				MetadataReference.CreateFromFile (typeof(object).Assembly.Location),
				MetadataReference.CreateFromFile (typeof(Enumerable).Assembly.Location),
				MetadataReference.CreateFromFile (Path.Combine (binDir, "Java.Interop.dll")),
				MetadataReference.CreateFromFile (Path.Combine (facDir, "netstandard.dll"))
			};

			// Compile!
			var compilation = CSharpCompilation.Create (
			    Path.GetFileName (assemblyFileName),
			    syntaxTrees: syntax_trees,
			    references: references,
			    options: new CSharpCompilationOptions (OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

			// Save assembly to a memory stream and load it with reflection
			using (var ms = new MemoryStream ()) {
				var result = compilation.Emit (ms);
				var success = result.Success && (allowWarnings || !result.Diagnostics.Any (d => d.Severity == DiagnosticSeverity.Warning));

				if (!success) {
					var failures = result.Diagnostics.Where (diagnostic =>
					     diagnostic.Severity == DiagnosticSeverity.Warning ||
					     diagnostic.Severity == DiagnosticSeverity.Error);

					hasErrors = true;
					output = OutputDiagnostics (failures);
				} else {
					ms.Seek (0, SeekOrigin.Begin);

					hasErrors = false;
					output = null;

					return Assembly.Load (ms.ToArray ());
				}
			}

			return null;
		}

		static string GetFacadesPath ()
		{
			var env = Environment.GetEnvironmentVariable ("FACADES_PATH");
			if (env != null)
				return env;

			var dir = Path.GetDirectoryName (typeof (object).Assembly.Location);
			var facades = Path.Combine (dir, "Facades");
			if (Directory.Exists (facades))
				return facades;

			return dir;
		}

		static string OutputDiagnostics (IEnumerable<Diagnostic> diagnostics)
		{
			var sb = new StringBuilder ();

			foreach (var d in diagnostics)
				sb.AppendLine ($"{d.Id}: {d.GetMessage ()} ({d.Location})");

			return sb.ToString ();
		}
	}
}
