using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Java.Interop.Tools.JavaCallableWrappers;
using NUnit.Framework;
using Xamarin.Android.Binder;

namespace generatortests
{
	public class BaseGeneratorTest
	{
		StringWriter sw = null;

		[SetUp]
		public void Setup ()
		{
			Options = new CodeGeneratorOptions ();
			Options.ApiLevel = "4";
			Options.GlobalTypeNames = true;
			Options.EnumFieldsMapFile = null;
			Options.EnumMethodsMapFile = null;
			Options.AssemblyQualifiedName = "Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
			Options.OnlyBindPublicTypes = true;
			sw = new StringWriter ();
			AdditionalSourceDirectories = new List<string> ();
		}

		protected CodeGeneratorOptions Options = null;
		protected Assembly BuiltAssembly = null;
		protected List<string> AdditionalSourceDirectories;
		protected bool AllowWarnings;

		public void Execute ()
		{
			CodeGenerator.Run (Options);
			var output = sw.ToString ();
			if (output.Contains ("error")) {
				Assert.Fail (output);
			}
			bool    hasErrors;
			string  compilerOutput;
			BuiltAssembly = Compiler.Compile (Options, FullPath ("Mono.Android.dll"), AdditionalSourceDirectories,
				out hasErrors, out compilerOutput, AllowWarnings);
			Assert.AreEqual (false, hasErrors, compilerOutput);
			Assert.IsNotNull (BuiltAssembly);
		}

		protected void CompareOutputs (string sourceDir, string destinationDir)
		{
			if (!Path.IsPathRooted (sourceDir))
				sourceDir = FullPath (sourceDir);
			if (!Path.IsPathRooted (destinationDir))
				destinationDir = FullPath (destinationDir);

			var files = Directory.GetFiles (sourceDir);
			foreach (var file in files) {
				var extension   = Path.GetExtension (file);
				if (extension == ".xml" || extension == ".fixed")
					continue;
				var filename = Path.GetFileName (file);
				var dest = Path.Combine (destinationDir, filename);
				if (!File.Exists (dest)) {
					Assert.Fail (string.Format ("Expected {0} but it was not generated.", dest));
				} else if (!FileCompare (file, dest)) {
					var fullSource  = Path.GetFullPath (file);
					var fullDest    = Path.GetFullPath (dest);
					//Error message for diff in powershell vs bash
					string message  = Environment.OSVersion.Platform == PlatformID.Win32NT ?
						$"File contents differ; run: diff (cat {fullSource}) `{Environment.NewLine}\t(cat {fullDest})" :
						$"File contents differ; run: diff -u {fullSource} \\{Environment.NewLine}\t{fullDest}";
					Assert.Fail (message);
				}
			}
		}

		protected void Cleanup (string path)
		{
			if (!Path.IsPathRooted (path))
				path = FullPath (path);
			if (Directory.Exists (path))
				Directory.Delete (path, true);
		}

		protected bool FileCompare (string file1, string file2)
		{
			bool result = false;

			result = File.Exists (file1) && File.Exists (file2);

			if (result) {
				byte[] f1 = ReadAllBytesIgnoringLineEndings (file1);
				byte[] f2 = ReadAllBytesIgnoringLineEndings (file2);

				using (var hash = new Crc64 ()) {
					var f1hash = Convert.ToBase64String (hash.ComputeHash (f1));
					var f2hash = Convert.ToBase64String (hash.ComputeHash (f2));
					result = f1hash.Equals (f2hash);
				}
			}

			return result;
		}

		private byte[] ReadAllBytesIgnoringLineEndings (string path)
		{
			using (var memoryStream = new MemoryStream ()) {
 				using (var file = File.OpenRead (path)) {
 					int readByte;
 					while ((readByte = file.ReadByte()) != -1) {
 						byte b = (byte)readByte;
 						if (b != '\r' && b != '\n') {
 							memoryStream.WriteByte (b);
 						}
 					}
 				}
				return memoryStream.ToArray ();
			}
		}

		protected void RunAllTargets (string outputRelativePath, string apiDescriptionFile, string expectedRelativePath, string[] additionalSupportPaths = null)
		{
			Run (CodeGenerationTarget.XamarinAndroid,   Path.Combine ("out", outputRelativePath),       apiDescriptionFile,     Path.Combine ("expected", expectedRelativePath),        additionalSupportPaths);
			Run (CodeGenerationTarget.JavaInterop1,     Path.Combine ("out.ji", outputRelativePath),    apiDescriptionFile,     Path.Combine ("expected.ji", expectedRelativePath),     additionalSupportPaths);
		}

		protected string FullPath (string path)
		{
			var dir = Path.GetDirectoryName (GetType ().Assembly.Location);
			return Path.Combine (dir, path.Replace ('/', Path.DirectorySeparatorChar));
		}

		protected void Run (CodeGenerationTarget target, string outputPath, string apiDescriptionFile, string expectedPath, string[] additionalSupportPaths = null)
		{
			Cleanup (outputPath);

			Options.CodeGenerationTarget                        = target;
			Options.ApiDescriptionFile                          = FullPath (apiDescriptionFile);
			Options.ManagedCallableWrapperSourceOutputDirectory = FullPath (outputPath);

			if (additionalSupportPaths != null) {
				AdditionalSourceDirectories.AddRange (additionalSupportPaths.Select (p => FullPath (p)));
			}

			Execute ();

			CompareOutputs (expectedPath, outputPath);
		}
	}
}
