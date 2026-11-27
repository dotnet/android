#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using NUnit.Framework;
using Microsoft.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class DlopenAssemblyStoreGeneratorTests : BaseTest
{
	[TestCase (AndroidTargetArch.Arm)]
	[TestCase (AndroidTargetArch.Arm64)]
	[TestCase (AndroidTargetArch.X86)]
	[TestCase (AndroidTargetArch.X86_64)]
	[Category ("RequiresAndroidNdk")]
	public void DataOnlyStoreExportsPayloadStart (AndroidTargetArch arch)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string payload = Path.Combine (directory, "assembly-store.so");
		File.WriteAllBytes (payload, [0x58, 0x41, 0x42, 0x41]);
		var task = new WrapAssemblyStoresAsSharedLibraries {
			BuildEngine = new MockBuildEngine (TestContext.Out),
		};

		string library = DlopenAssemblyStoreGenerator.WrapIt (task.Log, directory, arch, payload, "libassembly-store.so");

		FileAssert.Exists (library);
		using var document = NativeToolTestHelper.ReadElf (library);
		var symbols = document.RootElement [0].GetProperty ("DynamicSymbols").EnumerateArray ()
			.Select (item => item.GetProperty ("Symbol").GetProperty ("Name").GetProperty ("Name").GetString ()).ToArray ();
		CollectionAssert.AreEqual (new [] { "", DlopenAssemblyStoreGenerator.PayloadStartSymbol }, symbols);
		CollectionAssert.AreEqual (File.ReadAllBytes (payload), NativeToolTestHelper.ReadSection (library, "payload"));
		Assert.IsEmpty (Directory.GetFiles (directory, "*.S", SearchOption.AllDirectories));
		Assert.IsEmpty (Directory.GetFiles (directory, "*.o", SearchOption.AllDirectories));
	}

	[Test]
	[Category ("RequiresAndroidNdk")]
	public void NativeToolsCanExtractPayloadBySymbol (
		[Values (AndroidTargetArch.Arm, AndroidTargetArch.Arm64, AndroidTargetArch.X86_64)] AndroidTargetArch arch,
		[Values (1, 4097, 16385, 65537)] int size,
		[Values] bool stripped)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		byte [] expected = new byte [size];
		new Random (42).NextBytes (expected);
		string payload = Path.Combine (directory, "arbitrary payload.bin");
		File.WriteAllBytes (payload, expected);
		var task = new WrapAssemblyStoresAsSharedLibraries {
			BuildEngine = new MockBuildEngine (TestContext.Out),
		};
		string library = DlopenAssemblyStoreGenerator.WrapIt (task.Log, directory, arch, payload, "libassembly-store.so");

		if (stripped) {
			string strippedLibrary = Path.Combine (directory, "stripped.so");
			NativeToolTestHelper.Run ("llvm-strip", "--strip-all", "-o", strippedLibrary, library);
			library = strippedLibrary;
		}

		string symbols = NativeToolTestHelper.Run (
			"llvm-nm", "--dynamic", "--defined-only", "--print-size", "--format=posix", "--radix=x", library);
		var matches = Regex.Matches (symbols, @"^_assembly_store\s+R\s+([0-9a-fA-F]+)\s+([0-9a-fA-F]+)\s*$", RegexOptions.Multiline);
		Assert.AreEqual (1, matches.Count, $"Expected one global read-only payload symbol: {symbols}");
		StringAssert.DoesNotContain ("_assembly_store_end", symbols);
		ulong address = ulong.Parse (matches [0].Groups [1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		ulong symbolSize = ulong.Parse (matches [0].Groups [2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		Assert.AreEqual (expected.Length, symbolSize);

		using var document = NativeToolTestHelper.ReadElf (library);
		var segments = document.RootElement [0].GetProperty ("ProgramHeaders").EnumerateArray ()
			.Select (item => item.GetProperty ("ProgramHeader"))
			.Where (header => header.GetProperty ("Type").GetProperty ("Value").GetUInt32 () == 1)
			.Where (header => {
				ulong start = header.GetProperty ("VirtualAddress").GetUInt64 ();
				ulong length = header.GetProperty ("FileSize").GetUInt64 ();
				return address >= start && address - start <= length && symbolSize <= length - (address - start);
			}).ToArray ();
		Assert.AreEqual (1, segments.Length, "The symbol must lie entirely inside one file-backed PT_LOAD segment.");
		var segment = segments [0];
		Assert.AreEqual (4, segment.GetProperty ("Flags").GetProperty ("Value").GetInt32 (), "The mapped bytes must be read-only.");
		ulong offset = checked (segment.GetProperty ("Offset").GetUInt64 () + address - segment.GetProperty ("VirtualAddress").GetUInt64 ());

		// Extract only from the native tools' symbol and segment data, never from a section name
		// or the managed writer's layout calculations.
		using var reader = new BinaryReader (File.OpenRead (library));
		reader.BaseStream.Position = checked ((long)offset);
		byte [] actual = reader.ReadBytes (checked ((int)symbolSize));
		CollectionAssert.AreEqual (expected, actual, "Symbol-based extraction must reproduce arbitrary input bytes exactly.");

		CollectionAssert.AreEqual (expected, NativeToolTestHelper.ReadSection (library, "payload"),
			"Section-based inspection must also work after stripping.");
	}
}
