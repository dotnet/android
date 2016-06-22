using System;
using System.Reflection;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using Microsoft.CSharp;
using System.Collections.Generic;
using NUnit.Framework;
using Xamarin.Android.Binder;

namespace generatortests
{
	public static class Compiler
	{

		private static string unitTestFrameworkAssemblyPath = typeof(Assert).Assembly.Location;
		private static string supportFilePath = typeof(Compiler).Assembly.Location;

		public static Assembly Compile (Xamarin.Android.Binder.CodeGeneratorOptions options,
			string assemblyFileName, IEnumerable<string> AdditionalSourceDirectories,
			out bool hasErrors, out string output)
		{
			var generatedCodePath = options.ManagedCallableWrapperSourceOutputDirectory;
			var sourceFiles = Directory.EnumerateFiles (generatedCodePath, "*.cs",
				SearchOption.AllDirectories).ToList ();
			sourceFiles = sourceFiles.Select (x => Path.GetFullPath(x)).ToList ();

			var supportFiles = Directory.EnumerateFiles (Path.Combine (Path.GetDirectoryName (supportFilePath), "SupportFiles"),
				"*.cs", SearchOption.AllDirectories);
			sourceFiles.AddRange (supportFiles);

			foreach (var dir in AdditionalSourceDirectories) {
				var additonal = Directory.EnumerateFiles (dir, "*.cs", SearchOption.AllDirectories);
				sourceFiles.AddRange (additonal);
			}

			CompilerParameters parameters = new CompilerParameters ();
			parameters.GenerateExecutable = false;
			parameters.GenerateInMemory = true;
			parameters.CompilerOptions = "/unsafe";
			parameters.OutputAssembly = assemblyFileName;
			parameters.ReferencedAssemblies.Add (unitTestFrameworkAssemblyPath);

			var binDir  = Path.GetDirectoryName (typeof (BaseGeneratorTest).Assembly.Location);
			var facDir  = GetFacadesPath ();
			parameters.ReferencedAssemblies.Add (Path.Combine (binDir, "Java.Interop.dll"));
			parameters.ReferencedAssemblies.Add (Path.Combine (facDir, "System.Runtime.dll"));
#if DEBUG
			parameters.IncludeDebugInformation = true;
#else
			parameters.IncludeDebugInformation = false;
#endif

			CSharpCodeProvider codeProvider = new CSharpCodeProvider ();
			CompilerResults results = codeProvider.CompileAssemblyFromFile (parameters,sourceFiles.ToArray ());

			hasErrors   = false;

			foreach (CompilerError message in results.Errors) {
				hasErrors   = hasErrors || (!message.IsWarning);
			}
			output  = string.Join (Environment.NewLine, results.Output.Cast<string> ());

			return results.CompiledAssembly;
		}

		static string GetFacadesPath ()
		{
			var env = Environment.GetEnvironmentVariable ("FACADES_PATH");
			if (env != null)
				return env;
			return Path.Combine (
					Path.GetDirectoryName (typeof (object).Assembly.Location),
					"Facades");
		}
	}
}

