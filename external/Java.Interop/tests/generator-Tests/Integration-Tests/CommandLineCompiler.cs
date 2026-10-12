using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Xml.Linq;
using NUnit.Framework;

namespace generatortests
{
	sealed class CommandLineCompiler : IDisposable
	{
		readonly string directory = Path.Combine (Path.GetTempPath (), "binding-memory-safety-" + Guid.NewGuid ().ToString ("N"));
		int compilationCount;

		public CommandLineCompiler ()
		{
			Directory.CreateDirectory (directory);
		}

		public (int ExitCode, string Output, string AssemblyPath, string ReferencePath) Compile (
			IEnumerable<string> sources, bool updatedRules, IEnumerable<string> references = null)
		{
			var name = "Fixture" + compilationCount++;
			var projectDirectory = Path.Combine (directory, name);
			Directory.CreateDirectory (projectDirectory);
			var assemblyPath = Path.Combine (projectDirectory, "bin", name + ".dll");
			var referencePath = Path.Combine (projectDirectory, "bin", "ref", name + ".dll");
			var projectPath = Path.Combine (projectDirectory, name + ".csproj");
			var sourcePaths = sources.Select ((source, index) => {
				var path = Path.Combine (projectDirectory, "Source" + index + ".cs");
				File.WriteAllText (path, source);
				return path;
			}).ToArray ();

			var framework = typeof (CommandLineCompiler).Assembly.GetCustomAttribute<TargetFrameworkAttribute> ();
			if (framework == null)
				throw new InvalidOperationException ("The test assembly has no target framework.");
			var frameworkName = new FrameworkName (framework.FrameworkName);
			if (frameworkName.Identifier != ".NETCoreApp")
				throw new NotSupportedException ("Compiler fixtures require a .NET target framework.");
			var project = new XElement ("Project", new XAttribute ("Sdk", "Microsoft.NET.Sdk"),
				new XElement ("PropertyGroup",
					new XElement ("TargetFramework", $"net{frameworkName.Version.Major}.{frameworkName.Version.Minor}"),
					new XElement ("AllowUnsafeBlocks", "true"),
					new XElement ("LangVersion", updatedRules ? "preview" : "9"),
					new XElement ("Features", updatedRules ? "$(Features);updated-memory-safety-rules" : "$(Features);!updated-memory-safety-rules"),
					new XElement ("DefineConstants", updatedRules ? "NET;UPDATED_MEMORY_SAFETY_RULES" : "NET"),
					new XElement ("DisableImplicitFrameworkReferences", "true"),
					new XElement ("NoStdLib", "true"),
					new XElement ("EnableDefaultCompileItems", "false"),
					new XElement ("GenerateAssemblyInfo", "false"),
					new XElement ("EnableNETAnalyzers", "false"),
					new XElement ("ProduceReferenceAssembly", "true"),
					new XElement ("ProduceReferenceAssemblyInOutDir", "true"),
					new XElement ("AppendTargetFrameworkToOutputPath", "false"),
					new XElement ("OutputPath", "bin/")),
				new XElement ("ItemGroup", sourcePaths.Select (p => new XElement ("Compile", new XAttribute ("Include", p)))),
				new XElement ("ItemGroup", (references ?? Compiler.GetReferencePaths ()).Select (p =>
					new XElement ("Reference", new XAttribute ("Include", p),
						new XElement ("Private", "false")))));
			project.Save (projectPath);

			var hostPath = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH");
			if (string.IsNullOrEmpty (hostPath))
				hostPath = "dotnet";
			var startInfo = new ProcessStartInfo (hostPath, "build " + Quote (projectPath) + " --nologo -v:quiet") {
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
			};
			using (var process = Process.Start (startInfo)) {
				if (process == null)
					throw new InvalidOperationException ("Could not start the build SDK compiler.");
				var stdout = process.StandardOutput.ReadToEndAsync ();
				var stderr = process.StandardError.ReadToEndAsync ();
				if (!process.WaitForExit (120000)) {
					process.Kill ();
					process.WaitForExit ();
					Assert.Fail ("Build SDK compiler timed out: " + projectPath);
				}
				return (process.ExitCode, stdout.GetAwaiter ().GetResult () + stderr.GetAwaiter ().GetResult (), assemblyPath, referencePath);
			}
		}

		static string Quote (string value)
			=> "\"" + value.Replace ("\"", "\\\"") + "\"";

		public void Dispose ()
		{
			Directory.Delete (directory, recursive: true);
		}
	}
}
