using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

using CodeGenerationTarget = Xamarin.Android.Binder.CodeGenerationTarget;

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

			var preprocessorSymbols = new List<string> ();
			if (options.CodeGenerationTarget == CodeGenerationTarget.JavaInterop1) {
				preprocessorSymbols.Add ("JAVA_INTEROP1");
			}
#if NET
			preprocessorSymbols.Add ("NET");
#endif  // NET

			var parseOptions = new CSharpParseOptions (preprocessorSymbols:preprocessorSymbols);


			// Parse the source files
			var syntax_trees = sourceFiles.Distinct ()
				.Select (s => CSharpSyntaxTree.ParseText (File.ReadAllText (s), options:parseOptions))
				.ToArray ();

			// Set up the assemblies we need to reference
			var binDir = Path.GetDirectoryName (typeof (BaseGeneratorTest).Assembly.Location);
			var facDir = GetFacadesPath ();

			var referencePaths = new[]{
				unitTestFrameworkAssemblyPath,
				typeof(object).Assembly.Location,
				typeof(Enumerable).Assembly.Location,
				typeof(Uri).Assembly.Location,
				Path.Combine (binDir, "Java.Interop.dll"),
				Path.Combine (facDir, "netstandard.dll"),
#if NET
				Path.Combine (facDir, "System.Runtime.dll"),
#endif  // NET
			};

			var references = referencePaths.Select (p => MetadataReference.CreateFromFile (p)).ToArray ();

			string testCommandLine =
				$"csc \"-out:{Path.GetFileName (assemblyFileName)}\" " +
				$"-unsafe -t:library " +
				string.Join (" ", preprocessorSymbols.Select (p => $"\"-define:{p}\"")) + " " +
				string.Join (" ", referencePaths.Select (p => $"\"-r:{p}\"")) + " " +
				string.Join (" ", sourceFiles)
				;

			Console.WriteLine ($"# Trying to compile: {testCommandLine}");

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
