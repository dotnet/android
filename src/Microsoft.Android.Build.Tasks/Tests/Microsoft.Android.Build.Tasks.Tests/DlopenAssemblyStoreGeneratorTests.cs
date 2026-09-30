#nullable enable
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
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
		using var elf = ELFReader.Load (library);
		var symbols = (ISymbolTable)elf.GetSection (".dynsym");
		Assert.IsTrue (symbols.Entries.Any (symbol => symbol.Name == DlopenAssemblyStoreGenerator.PayloadStartSymbol));
		Assert.IsFalse (symbols.Entries.Any (symbol => symbol.Name == "_assembly_store_end"));
		CollectionAssert.AreEqual (File.ReadAllBytes (payload), elf.GetSection ("payload").GetContents ());
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
			var (stripExitCode, _, stripError) = RunProcessWithExitCode (
				NativeToolTestHelper.GetToolPath ("llvm-strip"), "--strip-all", "-o", strippedLibrary, library);
			Assert.AreEqual (0, stripExitCode, $"llvm-strip failed: {stripError}");
			library = strippedLibrary;
		}

		var (nmExitCode, symbols, nmError) = RunProcessWithExitCode (
			NativeToolTestHelper.GetToolPath ("llvm-nm"), "--dynamic", "--defined-only", "--print-size", "--format=posix", "--radix=x", library);
		Assert.AreEqual (0, nmExitCode, $"llvm-nm failed: {nmError}");
		var matches = Regex.Matches (symbols, @"^_assembly_store\s+R\s+([0-9a-fA-F]+)\s+([0-9a-fA-F]+)\s*$", RegexOptions.Multiline);
		Assert.AreEqual (1, matches.Count, $"Expected one global read-only payload symbol: {symbols}");
		StringAssert.DoesNotContain ("_assembly_store_end", symbols);
		ulong address = ulong.Parse (matches [0].Groups [1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		ulong symbolSize = ulong.Parse (matches [0].Groups [2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		Assert.AreEqual (expected.Length, symbolSize);

		var (readObjExitCode, headers, readObjError) = RunProcessWithExitCode (
			NativeToolTestHelper.GetToolPath ("llvm-readobj"), "--elf-output-style=JSON", "--program-headers", library);
		Assert.AreEqual (0, readObjExitCode, $"llvm-readobj failed: {readObjError}");
		using var document = JsonDocument.Parse (headers);
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

		using var elf = ELFReader.Load (library);
		CollectionAssert.AreEqual (expected, elf.GetSection ("payload").GetContents (), "Section-based inspection must also work after stripping.");
	}

	static (int Code, string Output, string Error) RunProcessWithExitCode (string executable, params string [] arguments)
	{
		FileAssert.Exists (executable);
		using var process = new Process {
			StartInfo = new ProcessStartInfo (executable) {
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			},
		};
		foreach (string argument in arguments) {
			process.StartInfo.ArgumentList.Add (argument);
		}
		process.Start ();
		var output = process.StandardOutput.ReadToEndAsync ();
		var error = process.StandardError.ReadToEndAsync ();
		if (!process.WaitForExit (30000)) {
			process.Kill (entireProcessTree: true);
			Assert.Fail ($"Native tool timed out: {executable}");
		}
		return (process.ExitCode, output.GetAwaiter ().GetResult (), error.GetAwaiter ().GetResult ());
	}
}
