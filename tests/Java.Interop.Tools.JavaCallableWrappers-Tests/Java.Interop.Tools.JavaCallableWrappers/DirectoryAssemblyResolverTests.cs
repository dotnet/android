using System.Diagnostics;
using System.IO;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Java.Interop.Tools.JavaCallableWrappersTests
{
	[TestClass]
	public class DirectoryAssemblyResolverTests
	{
		static void Log (TraceLevel level, string message)
		{
			System.Console.Out.WriteLine ($"{level}: {message}");

			if (level == TraceLevel.Error)
				Assert.Fail (message);
		}

		static string assembly_path;
		static string symbol_path;

		[ClassInitialize]
		public static void SetUp (TestContext context)
		{
			var assembly = typeof (DirectoryAssemblyResolverTests).Assembly;
			assembly_path = Path.Combine (Path.GetTempPath (), Path.GetFileName (assembly.Location));
			symbol_path = Path.ChangeExtension (assembly_path, ".pdb");

			File.Copy (assembly.Location, assembly_path, overwrite: true);
			File.Copy (Path.ChangeExtension (assembly.Location, ".pdb"), symbol_path, overwrite: true);
		}

		[ClassCleanup]
		public static void TearDown ()
		{
			File.Delete (assembly_path);
			File.Delete (symbol_path);
		}

		[TestMethod]
		[CombinatorialData]
		public void LoadSymbols ([CombinatorialValues (true, false)] bool loadDebugSymbols, [CombinatorialValues (true, false)] bool readWrite)
		{
			using var resolver = new DirectoryAssemblyResolver (Log, loadDebugSymbols: loadDebugSymbols, new ReaderParameters {
				ReadWrite = readWrite
			});

			var assembly = resolver.Load (assembly_path);
			Assert.IsNotNull (assembly);
			Assert.AreEqual (loadDebugSymbols, assembly.MainModule.HasSymbols);
		}

		[TestMethod]
		[CombinatorialData]
		public void LoadSymbols_LockedPdb ([CombinatorialValues (true, false)] bool readWrite)
		{
			// Lock the PDB file exclusively so LoadSymbols will fail with IOException
			using var lockStream = new FileStream (symbol_path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

			using var resolver = new DirectoryAssemblyResolver (Log, loadDebugSymbols: true, new ReaderParameters {
				ReadWrite = readWrite
			});

			// Should succeed by retrying without symbols instead of throwing
			var assembly = resolver.Load (assembly_path);
			Assert.IsNotNull (assembly);
			Assert.IsFalse (assembly.MainModule.HasSymbols);
		}
	}
}
