using System;
using System.Reflection;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace generatortests
{
	public static class Compiler
	{
		const string RoslynEnvironmentVariable = "ROSLYN_COMPILER_LOCATION";
		private static string unitTestFrameworkAssemblyPath = typeof(Assert).Assembly.Location;
		private static string supportFilePath = typeof(Compiler).Assembly.Location;

		static CodeDomProvider GetCodeDomProvider ()
		{
			if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
				//NOTE: there is an issue where Roslyn's csc.exe isn't copied to output for non-ASP.NET projects
				// Comments on this here: https://stackoverflow.com/a/40311406/132442
				// They added an environment variable as a workaround: https://github.com/aspnet/RoslynCodeDomProvider/pull/12
				if (string.IsNullOrEmpty (Environment.GetEnvironmentVariable (RoslynEnvironmentVariable, EnvironmentVariableTarget.Process))) {
					string roslynPath = Path.GetFullPath (Path.Combine (unitTestFrameworkAssemblyPath, "..", "..", "..", "packages", "microsoft.codedom.providers.dotnetcompilerplatform", "2.0.1", "tools", "RoslynLatest"));
					Environment.SetEnvironmentVariable (RoslynEnvironmentVariable, roslynPath, EnvironmentVariableTarget.Process);
				}

				return new Microsoft.CodeDom.Providers.DotNetCompilerPlatform.CSharpCodeProvider ();
			} else {
				return new Microsoft.CSharp.CSharpCodeProvider ();
			}
		}

		public static Assembly Compile (Xamarin.Android.Binder.CodeGeneratorOptions options,
			string assemblyFileName, IEnumerable<string> AdditionalSourceDirectories,
			out bool hasErrors, out string output, bool allowWarnings)
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
			parameters.ReferencedAssemblies.Add (typeof (Enumerable).Assembly.Location);

			var binDir  = Path.GetDirectoryName (typeof (BaseGeneratorTest).Assembly.Location);
			var facDir  = GetFacadesPath ();
			parameters.ReferencedAssemblies.Add (Path.Combine (binDir, "Java.Interop.dll"));
			parameters.ReferencedAssemblies.Add (Path.Combine (facDir, "netstandard.dll"));
#if DEBUG
			parameters.IncludeDebugInformation = true;
#else
			parameters.IncludeDebugInformation = false;
#endif

			using (var codeProvider = GetCodeDomProvider ()) {
				CompilerResults results = codeProvider.CompileAssemblyFromFile (parameters, sourceFiles.ToArray ());

				hasErrors = false;

				foreach (CompilerError message in results.Errors) {
					hasErrors |= !message.IsWarning || !allowWarnings;
				}
				output = string.Join (Environment.NewLine, results.Output.Cast<string> ());

				return results.CompiledAssembly;
			}
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
	}
}

