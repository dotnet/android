using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class LinkNativeAotSharedLibraryTests : BaseTest
{
	[TestCase (false, "XA3007")]
	[TestCase (true, "XA3008")]
	public void NativeToolFailureRemovesPartialOutputs (bool failDebugInfo, string errorCode)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		var errors = new List<BuildErrorEventArgs> ();
		var task = new LinkNativeAotSharedLibrary {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			Abi = "arm64-v8a",
			NativeObject = Path.Combine (directory, "input.so"),
			OutputSharedLibrary = Path.Combine (directory, "libapp.so"),
			IntermediateOutputPath = directory,
		};
		var ndk = NativeToolTestHelper.GetNdk (new TaskLoggingHelper (task));
		task.LinkerToolPath = ndk.GetToolPath ("ld.lld");
		// ld.lld rejects objcopy's options after the real link has succeeded.
		task.ObjcopyToolPath = task.LinkerToolPath;
		if (failDebugInfo) {
			using var payload = new MemoryStream ([0x58, 0x41, 0x42, 0x41]);
			using var output = File.Create (task.NativeObject);
			AssemblyStoreElfWriter.Write (payload, output, AndroidTargetArch.Arm64, "libinput.so");
		}
		string debugFile = Path.ChangeExtension (task.OutputSharedLibrary, ".dbg.so");
		File.WriteAllText (task.OutputSharedLibrary, "previous output");
		File.WriteAllText (debugFile, "previous debug info");
		Assert.IsFalse (task.Execute ());
		Assert.AreEqual (errorCode, errors [0].Code);
		FileAssert.DoesNotExist (task.OutputSharedLibrary);
		FileAssert.DoesNotExist (debugFile);
	}

	[TestCase ("armeabi-v7a", "armelf_linux_eabi", 4096)]
	[TestCase ("arm64-v8a", "aarch64linux", 16384)]
	[TestCase ("x86_64", "elf_x86_64", 16384)]
	public void ResponseFilePreservesLinkOrderAndAbiOptions (string abi, string emulation, int pageSize)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string response = Path.Combine (directory, "native.rsp");
		var task = new LinkNativeAotSharedLibrary {
			Abi = abi,
			NativeObject = "app code.o",
			OutputSharedLibrary = "libapp.so",
			CrtStartFiles = [new TaskItem ("crtbegin_so.o")],
			CrtEndFiles = [new TaskItem ("crtend_so.o")],
			CompilerRuntimeLibraries = [new TaskItem ("libclang_rt.builtins.a")],
			SystemLibraries = [new TaskItem ("dl"), new TaskItem ("c")],
			LibrarySearchPaths = [new TaskItem ("ndk sysroot")],
			ExportsFile = "app exports",
			LinkerScript = "sections.ld",
		};
		var archive = new TaskItem ("runtime archive.a");
		archive.SetMetadata ("LinkWholeArchive", "true");
		archive.SetMetadata ("DontExportSymbols", "true");
		task.WriteResponseFile (response, [archive]);
		string text = File.ReadAllText (response);
		StringAssert.Contains ("-m " + emulation, text);
		StringAssert.Contains ($"-z max-page-size={pageSize}", text);
		StringAssert.Contains ("-L \"ndk sysroot\"", text);
		StringAssert.Contains ("--version-script=\"app exports\"", text);
		StringAssert.Contains ("-T sections.ld", text);
		StringAssert.Contains ("--exclude-libs=\"runtime archive.a\"", text);
		Assert.AreEqual (abi == "arm64-v8a", text.Contains ("--fix-cortex-a53-843419", StringComparison.Ordinal));
		Assert.AreEqual (abi == "armeabi-v7a", text.Contains ("-X" + Environment.NewLine, StringComparison.Ordinal));

		int previous = -1;
		foreach (string item in new [] {
			"crtbegin_so.o", "\"app code.o\"", "--whole-archive", "\"runtime archive.a\"",
			"--no-whole-archive", "-ldl", "-lc", "libclang_rt.builtins.a", "crtend_so.o",
		}) {
			int current = text.IndexOf (item, previous + 1, StringComparison.Ordinal);
			Assert.Greater (current, previous, $"Expected {item} in link order.");
			previous = current;
		}
	}
}
