using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
			var assemblyPath = Path.Combine (directory, name + ".dll");
			var referencePath = Path.Combine (directory, name + ".ref.dll");
			var responsePath = Path.Combine (directory, name + ".rsp");
			var sourcePaths = sources.Select ((source, index) => {
				var path = Path.Combine (directory, name + "_" + index + ".cs");
				File.WriteAllText (path, source);
				return path;
			}).ToArray ();

			var arguments = new List<string> {
				"-nostdlib", "-unsafe", "-target:library",
				updatedRules ? "-langversion:preview" : "-langversion:9",
				updatedRules ? "-features:updated-memory-safety-rules" : "-features:!updated-memory-safety-rules",
				updatedRules ? "-define:NET,UPDATED_MEMORY_SAFETY_RULES" : "-define:NET",
				"-out:" + Quote (assemblyPath),
				"-refout:" + Quote (referencePath),
			};
			arguments.AddRange ((references ?? Compiler.GetReferencePaths ()).Select (p => "-r:" + Quote (p)));
			arguments.AddRange (sourcePaths.Select (Quote));
			File.WriteAllLines (responsePath, arguments);

			var compilerPath = GetBuildMetadata ("MemorySafetyCompilerPath");
			var hostPath = GetBuildMetadata ("MemorySafetyDotNetPath");
			Assert.That (File.Exists (compilerPath), Is.True, "Build SDK compiler not found: " + compilerPath);
			Assert.That (File.Exists (hostPath), Is.True, "Build SDK host not found: " + hostPath);
			var startInfo = new ProcessStartInfo (hostPath, Quote (compilerPath) + " -noconfig @" + Quote (responsePath)) {
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
			};
			startInfo.Environment ["DOTNET_ROOT"] = Path.GetDirectoryName (hostPath);
			using (var process = Process.Start (startInfo)) {
				if (process == null)
					throw new InvalidOperationException ("Could not start the build SDK compiler.");
				var stdout = process.StandardOutput.ReadToEndAsync ();
				var stderr = process.StandardError.ReadToEndAsync ();
				if (!process.WaitForExit (120000)) {
					process.Kill ();
					process.WaitForExit ();
					Assert.Fail ("Build SDK compiler timed out: " + responsePath);
				}
				return (process.ExitCode, stdout.GetAwaiter ().GetResult () + stderr.GetAwaiter ().GetResult (), assemblyPath, referencePath);
			}
		}

		static string GetBuildMetadata (string key)
			=> typeof (CommandLineCompiler).Assembly.GetCustomAttributes<AssemblyMetadataAttribute> ()
				.Single (a => a.Key == key).Value;

		static string Quote (string value)
			=> "\"" + value.Replace ("\"", "\\\"") + "\"";

		public void Dispose ()
		{
			Directory.Delete (directory, recursive: true);
		}
	}
}
