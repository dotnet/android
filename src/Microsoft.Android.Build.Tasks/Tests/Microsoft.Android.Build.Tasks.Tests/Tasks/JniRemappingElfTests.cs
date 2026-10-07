#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Runtime;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class JniRemappingElfTests : BaseTest
{
	[TestCase (AndroidTargetArch.Arm)]
	[TestCase (AndroidTargetArch.Arm64)]
	[TestCase (AndroidTargetArch.X86)]
	[TestCase (AndroidTargetArch.X86_64)]
	[Category ("RequiresAndroidNdk")]
	public void SingleSymbolPayloadIsContainedInReadOnlyLoadSegment (AndroidTargetArch arch)
	{
		byte [] payload = JniRemappingAssetWriter.Write (new JniRemappingEntries ());
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string library = Path.Combine (directory, WrapJniRemappingAsSharedLibrary.LibraryName);
		using (var input = new MemoryStream (payload))
		using (var output = File.Create (library))
			AssemblyStoreElfWriter.Write (input, output, arch, WrapJniRemappingAsSharedLibrary.LibraryName,
				WrapJniRemappingAsSharedLibrary.PayloadSymbol);
		using var document = NativeToolTestHelper.ReadElf (library);
		var elf = document.RootElement [0];
		var symbols = elf.GetProperty ("DynamicSymbols").EnumerateArray ()
			.Select (entry => entry.GetProperty ("Symbol")).ToArray ();
		Assert.AreEqual (2, symbols.Length, "Only one payload symbol and the required null symbol may be present.");
		var begin = symbols.Single (entry => entry.GetProperty ("Name").GetProperty ("Name").GetString () == WrapJniRemappingAsSharedLibrary.PayloadSymbol);
		Assert.AreEqual ((ulong)payload.Length, begin.GetProperty ("Size").GetUInt64 ());
		var load = elf.GetProperty ("ProgramHeaders").EnumerateArray ()
			.Select (entry => entry.GetProperty ("ProgramHeader"))
			.Single (entry => entry.GetProperty ("Type").GetProperty ("Value").GetInt32 () == 1);
		Assert.AreEqual (4, load.GetProperty ("Flags").GetProperty ("Value").GetInt32 ());
		Assert.AreEqual (load.GetProperty ("VirtualAddress").GetUInt64 () + load.GetProperty ("MemSize").GetUInt64 (),
			begin.GetProperty ("Value").GetUInt64 () + (ulong)payload.Length);
		CollectionAssert.AreEqual (payload, NativeToolTestHelper.ReadSection (library, "payload"));
	}

	sealed class FailingWriteTask : WrapJniRemappingAsSharedLibrary
	{
		protected override void WriteOutputFile (string outputFile)
		{
			File.WriteAllBytes (outputFile, new byte [8]);
			throw new IOException ("Simulated interrupted ELF publication.");
		}
	}

	[TestCase (false)]
	[TestCase (true)]
	public void FailedWritePreservesOutputAndRetryProducesElf (bool existingOutput)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string input = Path.Combine (directory, "jni-remap.bin");
		string output = Path.Combine (directory, WrapJniRemappingAsSharedLibrary.LibraryName);
		File.WriteAllBytes (input, JniRemappingAssetWriter.Write (new JniRemappingEntries ()));
		var errors = new List<BuildErrorEventArgs> ();
		var engine = new MockBuildEngine (TestContext.Out, errors);
		var task = new WrapJniRemappingAsSharedLibrary {
			BuildEngine = engine, InputFile = input, OutputFile = output, RuntimeIdentifier = "android-arm64",
		};
		byte []? original = null;
		if (existingOutput) {
			Assert.IsTrue (task.Execute ());
			original = File.ReadAllBytes (output);
		}
		var failed = new FailingWriteTask {
			BuildEngine = engine, InputFile = input, OutputFile = output, RuntimeIdentifier = "android-arm64",
		};
		Assert.IsFalse (failed.Execute ());
		Assert.IsTrue (errors.Any (error => error.Code == "XA4331"));
		Assert.IsEmpty (Directory.GetFiles (directory, "*.tmp"));
		if (original is not null)
			CollectionAssert.AreEqual (original, File.ReadAllBytes (output));
		else
			FileAssert.DoesNotExist (output);
		Assert.IsTrue (task.Execute ());
		CollectionAssert.AreEqual (new byte [] { 0x7f, (byte)'E', (byte)'L', (byte)'F' }, File.ReadAllBytes (output).Take (4));
	}
}
