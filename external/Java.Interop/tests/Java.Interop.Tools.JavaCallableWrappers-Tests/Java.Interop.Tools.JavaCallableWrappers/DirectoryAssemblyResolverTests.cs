using System.Diagnostics;
using System.IO;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using NUnit.Framework;

namespace Java.Interop.Tools.JavaCallableWrappersTests
{
	[TestFixture]
	public class DirectoryAssemblyResolverTests
	{
		static void Log (TraceLevel level, string message)
		{
			TestContext.Out.WriteLine ($"{level}: {message}");

			if (level == TraceLevel.Error)
				Assert.Fail (message);
		}

		static string assembly_path;
		static string symbol_path;

		[OneTimeSetUp]
		public static void SetUp()
		{
			var assembly = typeof (DirectoryAssemblyResolverTests).Assembly;
			assembly_path = Path.Combine (Path.GetTempPath (), Path.GetFileName (assembly.Location));
			symbol_path = Path.ChangeExtension (assembly_path, ".pdb");

			File.Copy (assembly.Location, assembly_path, overwrite: true);
			File.Copy (Path.ChangeExtension (assembly.Location, ".pdb"), symbol_path, overwrite: true);
		}

		[OneTimeTearDown]
		public static void TearDown ()
		{
			File.Delete (assembly_path);
			File.Delete (symbol_path);
		}

		[Test]
		public void LoadSymbols ([Values (true, false)] bool loadDebugSymbols, [Values (true, false)] bool readWrite)
		{
			using var resolver = new DirectoryAssemblyResolver (Log, loadDebugSymbols: loadDebugSymbols, new ReaderParameters {
				ReadWrite = readWrite
			});

			var assembly = resolver.Load (assembly_path);
			Assert.IsNotNull (assembly);
			Assert.AreEqual (loadDebugSymbols, assembly.MainModule.HasSymbols);
		}
	}
}
