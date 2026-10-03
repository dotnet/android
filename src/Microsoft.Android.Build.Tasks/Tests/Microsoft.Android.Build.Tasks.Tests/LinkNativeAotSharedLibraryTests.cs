using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Task = System.Threading.Tasks.Task;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class LinkNativeAotSharedLibraryTests : BaseTest
{
	[TestCase (1, "XA3007")]
	[TestCase (2, "XA3008")]
	[TestCase (3, "XA3008")]
	[TestCase (4, "XA3008")]
	public void NativeToolFailureRemovesPartialOutputs (int failInvocation, string errorCode)
	{
		var errors = new List<BuildErrorEventArgs> ();
		var task = CreateTask (errors);
		task.FailInvocation = failInvocation;
		string debugFile = Path.ChangeExtension (task.OutputSharedLibrary, ".dbg.so");
		File.WriteAllText (task.OutputSharedLibrary, "previous output");
		File.WriteAllText (debugFile, "previous debug info");
		Assert.IsFalse (task.Execute ());
		Assert.AreEqual (1, errors.Count);
		Assert.AreEqual (errorCode, errors [0].Code);
		Assert.AreEqual (failInvocation, task.Invocations.Count);
		FileAssert.DoesNotExist (task.OutputSharedLibrary);
		FileAssert.DoesNotExist (debugFile);
	}

	[Test]
	public void ToolStartFailureHasLinkDiagnosticAndRemovesOutputs ()
	{
		var errors = new List<BuildErrorEventArgs> ();
		var task = CreateTask (errors);
		task.ThrowOnInvocation = 1;
		File.WriteAllText (task.OutputSharedLibrary, "previous output");
		Assert.IsFalse (task.Execute ());
		Assert.AreEqual ("XA3007", errors [0].Code);
		FileAssert.DoesNotExist (task.OutputSharedLibrary);
	}

	[TestCase (false)]
	[TestCase (true)]
	public void DebugInfoExtractionPrecedesStrippingAndDebugLink (bool debugBuild)
	{
		var task = CreateTask ([]);
		task.DebugBuild = debugBuild;
		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (debugBuild ? 1 : 4, task.Invocations.Count);
		Assert.AreEqual (task.LinkerToolPath, task.Invocations [0].Tool);
		if (!debugBuild) {
			string debugFile = Path.ChangeExtension (task.OutputSharedLibrary, ".dbg.so");
			CollectionAssert.AreEqual (new [] { "--only-keep-debug", task.OutputSharedLibrary, debugFile }, task.Invocations [1].Arguments);
			CollectionAssert.AreEqual (new [] { "--strip-debug", "--strip-unneeded", task.OutputSharedLibrary }, task.Invocations [2].Arguments);
			CollectionAssert.AreEqual (new [] { "--add-gnu-debuglink=" + debugFile, task.OutputSharedLibrary }, task.Invocations [3].Arguments);
			Assert.IsTrue (task.Invocations.Skip (1).All (invocation => invocation.Tool == task.ObjcopyToolPath));
		}
	}

	[TestCase (false)]
	[TestCase (true)]
	public void ArmEhabiSymbolsAreGlobalizedAndWeakenedInACopy (bool failPromotion)
	{
		var errors = new List<BuildErrorEventArgs> ();
		var task = CreateTask (errors);
		task.Abi = "armeabi-v7a";
		task.DebugBuild = true;
		task.FailInvocation = failPromotion ? 1 : 0;
		string original = Path.Combine (task.IntermediateOutputPath, "libRuntime.WorkstationGC.a");
		File.WriteAllText (original, "original archive");
		task.NativeLibraries = [new TaskItem (original)];
		string copy = Path.Combine (task.IntermediateOutputPath, "libRuntime.WorkstationGC.arm-ehabi.a");
		Assert.AreEqual (!failPromotion, task.Execute ());
		var promotion = task.Invocations [0];
		Assert.AreEqual (task.ObjcopyToolPath, promotion.Tool);
		CollectionAssert.AreEqual (new [] {
			"--globalize-symbol=__aeabi_unwind_cpp_pr0", "--weaken-symbol=__aeabi_unwind_cpp_pr0",
			"--globalize-symbol=__aeabi_unwind_cpp_pr1", "--weaken-symbol=__aeabi_unwind_cpp_pr1",
			"--globalize-symbol=__aeabi_unwind_cpp_pr2", "--weaken-symbol=__aeabi_unwind_cpp_pr2",
			original, copy,
		}, promotion.Arguments);
		Assert.AreEqual ("original archive", File.ReadAllText (original));
		if (failPromotion) {
			Assert.AreEqual ("XA3007", errors [0].Code);
			FileAssert.DoesNotExist (copy);
			FileAssert.DoesNotExist (task.OutputSharedLibrary);
		} else {
			FileAssert.Exists (copy);
			string responseFile = task.Invocations [1].Arguments [0].Substring (1);
			string response = File.ReadAllText (responseFile);
			StringAssert.Contains (copy, response);
			CollectionAssert.DoesNotContain (File.ReadAllLines (responseFile).Select (line => line.Trim ('"')), original);
		}
	}

	[TestCase ("armeabi-v7a", "armelf_linux_eabi", 4096)]
	[TestCase ("arm64-v8a", "aarch64linux", 16384)]
	[TestCase ("x86_64", "elf_x86_64", 16384)]
	public void ResponseFilePreservesGeneratedObjectsLinkOrderAndAbiOptions (string abi, string emulation, int pageSize)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string response = Path.Combine (directory, "native.rsp");
		var task = new LinkNativeAotSharedLibrary {
			Abi = abi,
			NativeObject = "app code.o",
			OutputSharedLibrary = "libapp.so",
			AdditionalObjectFiles = [new TaskItem ("jni_init_funcs.o"), new TaskItem ("environment.o")],
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
			"--no-whole-archive", "-ldl", "-lc", "jni_init_funcs.o", "environment.o",
			"libclang_rt.builtins.a", "crtend_so.o",
		}) {
			int current = text.IndexOf (item, previous + 1, StringComparison.Ordinal);
			Assert.Greater (current, previous, $"Expected {item} in link order.");
			previous = current;
		}
	}

	TestLinker CreateTask (List<BuildErrorEventArgs> errors)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		return new TestLinker {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			Abi = "arm64-v8a",
			LinkerToolPath = "ld.lld",
			ObjcopyToolPath = "llvm-objcopy",
			NativeObject = Path.Combine (directory, "input.o"),
			OutputSharedLibrary = Path.Combine (directory, "libapp.so"),
			IntermediateOutputPath = directory,
		};
	}

	sealed class TestLinker : LinkNativeAotSharedLibrary
	{
		public int FailInvocation { get; set; }
		public int ThrowOnInvocation { get; set; }
		public List<(string Tool, string [] Arguments)> Invocations { get; } = [];

		protected override Task<int> ExecuteToolAsync (string tool, string [] arguments, TextWriter stdout, TextWriter stderr)
		{
			Invocations.Add ((tool, arguments));
			if (Invocations.Count == ThrowOnInvocation) {
				throw new Win32Exception ("Unable to start native tool.");
			}
			if (arguments [0].StartsWith ("@", StringComparison.Ordinal)) {
				File.WriteAllText (OutputSharedLibrary, "linked output");
			} else if (arguments [0] == "--only-keep-debug" || arguments [0].StartsWith ("--globalize-symbol=", StringComparison.Ordinal)) {
				File.WriteAllText (arguments [^1], "native sidecar");
			}
			if (Invocations.Count == FailInvocation) {
				stderr.WriteLine ("Native tool failed.");
				return Task.FromResult (1);
			}
			return Task.FromResult (0);
		}
	}
}
