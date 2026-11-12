using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Linq;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using Microsoft.Android.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Build.Tests;

[TestClass]
[OSCondition (OperatingSystems.OSX | OperatingSystems.Linux)]
[DoNotParallelize]
public class NativeAotSystemPropertiesTests : BaseTest
{
	static string nativeProbe = "";
	static string probeDirectory = "";

	static string ResourcesDirectory => Path.Combine (AppContext.BaseDirectory, "Resources", "NativeAotBootstrap");

	[ClassInitialize]
	public static void CompileProductionReader (TestContext context)
	{
		string root = NativeAotBootstrapTestTools.FindRepositoryRoot ();
		probeDirectory = Path.Combine (Root, "temp", nameof (NativeAotSystemPropertiesTests));
		Directory.CreateDirectory (probeDirectory);
		nativeProbe = Path.Combine (probeDirectory, "bootstrap-probe");
		var compile = NativeAotBootstrapTestTools.Run (Environment.GetEnvironmentVariable ("CXX") ?? "clang++",
			"-std=c++23", "-O2", "-fno-strict-aliasing", "-fsanitize=undefined", "-fno-sanitize-recover=undefined",
			"-DXA_HOST_NATIVEAOT", "-ffunction-sections", "-fdata-sections",
			OperatingSystem.IsMacOS () ? "-Wl,-dead_strip" : "-Wl,--gc-sections",
			"-I" + Path.Combine (root, "src", "native", "nativeaot", "include"),
			"-I" + Path.Combine (root, "src", "native", "common", "include"),
			Path.Combine (root, "src", "native", "nativeaot", "runtime-base", "app-system-properties.cc"),
			Path.Combine (root, "src", "native", "clr", "runtime-base", "binary-blob-loader.cc"),
			Path.Combine (ResourcesDirectory, "bootstrap-probe.cc"),
			"-o", nativeProbe);
		Assert.AreEqual (0, compile.ExitCode, compile.Output + compile.Error);
	}

	[ClassCleanup]
	public static void DeleteProductionReaderProbe ()
	{
		if (probeDirectory.Length > 0 && Directory.Exists (probeDirectory)) {
			Directory.Delete (probeDirectory, recursive: true);
		}
	}

	[TestMethod]
	[DataRow ("armeabi-v7a", false)]
	[DataRow ("armeabi-v7a", true)]
	[DataRow ("arm64-v8a", false)]
	[DataRow ("arm64-v8a", true)]
	[DataRow ("x86_64", false)]
	[DataRow ("x86_64", true)]
	public void ReadsPropertiesFromMappedDataObjectAsTrueUtf8 (string abi, bool environment)
	{
		string value = "\"C:\\quoted\"\t\u00e9\u4e2d\ud83d\ude80";
		string key = "debug.bootstrap.\ud83d\ude80";
		string contents = string.Join ("\n", Enumerable.Range (0, 1000).Select (i => $"debug.property.{i}=value-{i}"));
		var result = RunProbe ($"DOTNET_BOOTSTRAP_TEST=old\nDOTNET_BOOTSTRAP_EMPTY=\n{key}=old\n{contents}\n{key}={value}\nDOTNET_BOOTSTRAP_TEST={value}\n",
			environment ? "DOTNET_BOOTSTRAP_TEST" : key, abi: abi, environment: environment);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		Assert.AreEqual (value, result.Output);
	}

	[TestMethod]
	[DataRow (false)]
	[DataRow (true)]
	public void EmptyConfigurationAndMissingPropertiesResetLength (bool environment)
	{
		string key = environment ? "DOTNET_BOOTSTRAP_EMPTY" : "debug.empty";
		var result = RunProbe ("", key, environment: environment);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		Assert.AreEqual ("missing", result.Output);
		var emptyValue = RunProbe ($"{key}=\n", key, environment: environment);
		Assert.AreEqual (0, emptyValue.ExitCode, emptyValue.Output + emptyValue.Error);
		Assert.AreEqual ("", emptyValue.Output);
	}

	[TestMethod]
	public void RejectsMalformedBootstrapOffsets ()
	{
		var result = RunProbe ("debug.value=test\n", "debug.value", body => BitConverter.GetBytes (uint.MaxValue).CopyTo (body, 24));
		Assert.AreNotEqual (0, result.ExitCode);
		Assert.Contains ("Invalid NativeAOT bootstrap header", result.Error);
	}

	[TestMethod]
	[DataRow (true, 0)]
	[DataRow (true, 4)]
	[DataRow (false, 0)]
	[DataRow (false, 4)]
	public void RejectsMalformedPairOffsets (bool environment, int fieldOffset)
	{
		var result = RunProbe ("DOTNET_BOOTSTRAP_TEST=environment\ndebug.value=test\n", "debug.value",
			body => {
				int tableOffset = environment ? NativeAotBootstrapBlob.HeaderSize
					: checked ((int)BinaryPrimitives.ReadUInt32LittleEndian (body.AsSpan (20)));
				BinaryPrimitives.WriteUInt32LittleEndian (body.AsSpan (tableOffset + fieldOffset), uint.MaxValue);
			});
		Assert.AreNotEqual (0, result.ExitCode);
		Assert.Contains ("Invalid NativeAOT bootstrap string", result.Error);
	}

	[TestMethod]
	[DataRow ("armeabi-v7a")]
	[DataRow ("arm64-v8a")]
	[DataRow ("x86_64")]
	public void RejectsMisalignedBootstrapDataBeforeReadingPairs (string abi)
	{
		var result = RunProbe ("debug.value=test\n", "debug.value", abi: abi, misalignEnvelope: true);
		Assert.AreNotEqual (0, result.ExitCode);
		Assert.Contains ("Misaligned NativeAOT bootstrap data", result.Error);
	}

	[TestMethod]
	public void RejectsCompressedNativeAotData ()
	{
		var result = RunProbe ("debug.value=test\n", "debug.value", markCompressed: true);
		Assert.AreNotEqual (0, result.ExitCode);
		Assert.Contains ("Compressed NativeAOT binary blob", result.Error);
	}

	[TestMethod]
	public void RejectsTruncatedEnvelopeBeforeReadingHeader ()
	{
		var result = RunProbe ("debug.value=test\n", "debug.value", envelopeLengthOverride: 15);
		Assert.AreNotEqual (0, result.ExitCode);
		Assert.Contains ("Missing or truncated binary blob", result.Error);
	}

	(int ExitCode, string Output, string Error) RunProbe (
		string contents, string key, Action<byte []>? corrupt = null, string abi = "arm64-v8a",
		bool markCompressed = false, uint? envelopeLengthOverride = null, bool misalignEnvelope = false, bool environment = false)
	{
		string path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		string environmentFile = Path.Combine (path, "environment.txt");
		File.WriteAllText (environmentFile, contents);
		string objectFile = Path.Combine (path, "nativeaot-bootstrap.o");
		var task = new GenerateNativeAotDataObject {
			BuildEngine = new MockBuildEngine (Console.Out),
			OutputFile = objectFile,
			Abi = abi,
			Environments = [new TaskItem (environmentFile)],
		};
		Assert.IsTrue (task.Execute ());
		ulong envelopeOffset;
		ulong envelopeLength;
		using (IELF elf = ELFReader.Load (objectFile)) {
			Assert.AreEqual (FileType.Relocatable, elf.Type);
			var symbol = ((ISymbolTable)elf.GetSection (".symtab")).Entries.Single (entry => entry.Name == NativeAotBootstrapBlob.Symbol);
			Assert.AreEqual (".rodata", symbol.PointedSection.Name);
			if (symbol is SymbolEntry<ulong> s64 && symbol.PointedSection is Section<ulong> section64) {
				envelopeOffset = checked (section64.Offset + s64.Value);
				envelopeLength = s64.Size;
			} else if (symbol is SymbolEntry<uint> s32 && symbol.PointedSection is Section<uint> section32) {
				envelopeOffset = checked ((ulong)section32.Offset + s32.Value);
				envelopeLength = s32.Size;
			} else {
				throw new InvalidDataException ("Unsupported bootstrap ELF symbol.");
			}
		}
		const int EnvelopeSize = 16;
		byte [] image = File.ReadAllBytes (objectFile);
		var envelope = image.AsSpan (checked ((int)envelopeOffset), EnvelopeSize);
		Assert.AreEqual (0x42424c42u, BinaryPrimitives.ReadUInt32LittleEndian (envelope));
		Assert.AreEqual (0, BinaryPrimitives.ReadUInt16LittleEndian (envelope.Slice (6, 2)), "Mapped bootstrap data must be uncompressed.");
		uint bodyLength = BinaryPrimitives.ReadUInt32LittleEndian (envelope.Slice (12, 4));
		Assert.AreEqual (bodyLength, BinaryPrimitives.ReadUInt32LittleEndian (envelope.Slice (8, 4)));
		Assert.AreEqual ((ulong)bodyLength + EnvelopeSize, envelopeLength);
		ulong bodyOffset = checked (envelopeOffset + EnvelopeSize);
		if (corrupt != null || markCompressed || misalignEnvelope) {
			if (corrupt != null) {
				byte [] body = image.AsSpan (checked ((int)bodyOffset), checked ((int)bodyLength)).ToArray ();
				corrupt (body);
				body.CopyTo (image, checked ((int)bodyOffset));
			}
			if (markCompressed) {
				image [checked ((int)envelopeOffset) + 6] = 1;
			}
			if (misalignEnvelope) {
				image.AsSpan (checked ((int)envelopeOffset), checked ((int)envelopeLength))
					.CopyTo (image.AsSpan (checked ((int)envelopeOffset + 1)));
				envelopeOffset++;
			}
			File.WriteAllBytes (objectFile, image);
		}
		return NativeAotBootstrapTestTools.Run (nativeProbe, objectFile,
			envelopeOffset.ToString (CultureInfo.InvariantCulture), (envelopeLengthOverride ?? envelopeLength).ToString (CultureInfo.InvariantCulture),
			key, environment ? "environment" : "property");
	}
}
