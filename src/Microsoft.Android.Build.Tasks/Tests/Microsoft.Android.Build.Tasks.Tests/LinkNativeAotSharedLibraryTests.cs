using System;
using System.Collections.Generic;
using System.IO;
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
		Assert.AreEqual (failInvocation, task.InvocationCount);
		FileAssert.DoesNotExist (task.OutputSharedLibrary);
		FileAssert.DoesNotExist (debugFile);
	}

	[Test]
	public void ArmEhabiPromotionFailureRemovesCopiedArchive ()
	{
		var errors = new List<BuildErrorEventArgs> ();
		var task = CreateTask (errors);
		task.Abi = "armeabi-v7a";
		task.FailInvocation = 1;
		string original = Path.Combine (task.IntermediateOutputPath, "libRuntime.WorkstationGC.a");
		File.WriteAllText (original, "original archive");
		task.NativeLibraries = [new TaskItem (original)];
		string copy = Path.Combine (task.IntermediateOutputPath, "libRuntime.WorkstationGC.arm-ehabi.a");
		Assert.IsFalse (task.Execute ());
		Assert.AreEqual ("original archive", File.ReadAllText (original));
		Assert.AreEqual ("XA3007", errors [0].Code);
		FileAssert.DoesNotExist (copy);
		FileAssert.DoesNotExist (task.OutputSharedLibrary);
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
		public int InvocationCount { get; private set; }

		protected override Task<int> ExecuteToolAsync (string tool, string [] arguments, TextWriter stdout, TextWriter stderr)
		{
			InvocationCount++;
			if (arguments [0].StartsWith ("@", StringComparison.Ordinal)) {
				File.WriteAllText (OutputSharedLibrary, "linked output");
			} else if (arguments [0] == "--only-keep-debug" || arguments [0].StartsWith ("--globalize-symbol=", StringComparison.Ordinal)) {
				File.WriteAllText (arguments [^1], "native sidecar");
			}
			if (InvocationCount == FailInvocation) {
				stderr.WriteLine ("Native tool failed.");
				return Task.FromResult (1);
			}
			return Task.FromResult (0);
		}
	}
}
