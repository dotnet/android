using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class ResolveAndroidNdkTests : BaseTest
{
	string NdkDirectory => Path.Combine (Root, "temp", TestName, "ndk");
	string ToolchainDirectory => Path.Combine (NdkDirectory, "toolchains", "llvm", "prebuilt", AndroidNdkTools.HostTag);
	string ToolPath (string name) => Path.Combine (ToolchainDirectory, "bin", name + (OperatingSystem.IsWindows () ? ".exe" : ""));

	[TestCase (null)]
	[TestCase ("")]
	[TestCase ("missing-ndk")]
	public void MissingNdkIsAnExplicitError (string? directory)
	{
		var errors = new List<BuildErrorEventArgs> ();
		var task = CreateTask (errors);
		task.AndroidNdkDirectory = directory == "missing-ndk" ? NdkDirectory : directory;
		Assert.IsFalse (task.Execute ());
		Assert.AreEqual ("XA5104", errors [0].Code);
	}

	[Test]
	public void MissingHostToolchainIsAnExplicitError ()
	{
		Directory.CreateDirectory (NdkDirectory);
		File.WriteAllText (Path.Combine (NdkDirectory, "source.properties"), "Pkg.Revision = 29.0.14206865");
		var errors = new List<BuildErrorEventArgs> ();
		Assert.IsFalse (CreateTask (errors).Execute ());
		Assert.AreEqual ("XA5101", errors [0].Code);
		StringAssert.Contains (AndroidNdkTools.HostTag, errors [0].Message);
	}

	[Test]
	public void StrippingOnlyRequiresTheStripTool ()
	{
		CreateNdk ("llvm-strip");
		var errors = new List<BuildErrorEventArgs> ();
		var task = CreateTask (errors);
		task.StripNativeLibraries = true;
		Assert.IsTrue (task.Execute ());
		Assert.IsEmpty (errors);
		Assert.AreEqual (Path.GetFullPath (ToolPath ("llvm-strip")), task.StripToolPath);
		Assert.IsNull (task.LinkerToolPath);
		Assert.IsNull (task.ObjcopyToolPath);
		Assert.IsNull (task.ClangRuntimeDirectory);
	}

	[TestCase ("ld.lld")]
	[TestCase ("llvm-objcopy")]
	[TestCase ("llvm-strip")]
	public void MissingRequestedToolIsAnExplicitError (string missingTool)
	{
		CreateNativeAotNdk ();
		File.Delete (ToolPath (missingTool));
		var errors = new List<BuildErrorEventArgs> ();
		var task = CreateTask (errors);
		task.AndroidRuntime = "NativeAOT";
		task.StripNativeLibraries = true;
		Assert.IsFalse (task.Execute ());
		Assert.AreEqual (1, errors.Count);
		Assert.AreEqual ("XA5105", errors [0].Code);
		StringAssert.Contains (missingTool, errors [0].Message);
	}

	[Test]
	public void NativeAotResolvesNdkToolsAndClangRuntime ()
	{
		CreateNativeAotNdk ();
		var errors = new List<BuildErrorEventArgs> ();
		var task = CreateTask (errors);
		task.AndroidRuntime = "NativeAOT";
		Assert.IsTrue (task.Execute ());
		Assert.IsEmpty (errors);
		Assert.AreEqual (Path.GetFullPath (ToolPath ("ld.lld")), task.LinkerToolPath);
		Assert.AreEqual (Path.GetFullPath (ToolPath ("llvm-objcopy")), task.ObjcopyToolPath);
		Assert.AreEqual (Path.GetFullPath (Path.Combine (ToolchainDirectory, "lib", "clang", "20", "lib", "linux")), task.ClangRuntimeDirectory);
		Assert.IsNull (task.StripToolPath);
	}

	[TestCase ("sysroot")]
	[TestCase ("lib")]
	public void MissingNativeAotLibraryDirectoryIsAnExplicitError (string subdirectory)
	{
		CreateNdk ("ld.lld", "llvm-objcopy");
		Directory.CreateDirectory (Path.Combine (ToolchainDirectory, subdirectory == "sysroot" ? "lib/clang/20/lib/linux" : "sysroot/usr/lib"));
		var errors = new List<BuildErrorEventArgs> ();
		var task = CreateTask (errors);
		task.AndroidRuntime = "NativeAOT";
		Assert.IsFalse (task.Execute ());
		Assert.AreEqual (1, errors.Count);
		Assert.AreEqual ("XA5101", errors [0].Code);
	}

	[Test]
	public void CheckedBuildResolvesLatestClangRuntimeWithoutLinker ()
	{
		CreateNdk ();
		Directory.CreateDirectory (Path.Combine (ToolchainDirectory, "lib", "clang", "9.0.1", "lib", "linux"));
		string runtime = Path.Combine (ToolchainDirectory, "lib", "clang", "20", "lib", "linux");
		Directory.CreateDirectory (runtime);
		var errors = new List<BuildErrorEventArgs> ();
		var task = CreateTask (errors);
		task.CheckedBuild = "asan";
		Assert.IsTrue (task.Execute ());
		Assert.IsEmpty (errors);
		Assert.AreEqual (Path.GetFullPath (runtime), task.ClangRuntimeDirectory);
	}

	ResolveAndroidNdk CreateTask (List<BuildErrorEventArgs> errors) => new () {
		BuildEngine = new MockBuildEngine (TestContext.Out, errors),
		AndroidNdkDirectory = NdkDirectory,
		AndroidRuntime = "CoreCLR",
	};

	void CreateNdk (params string [] tools)
	{
		Directory.CreateDirectory (Path.Combine (ToolchainDirectory, "bin"));
		File.WriteAllText (Path.Combine (NdkDirectory, "source.properties"), "Pkg.Revision = 29.0.14206865");
		foreach (string tool in tools) {
			File.WriteAllBytes (ToolPath (tool), []);
		}
	}

	void CreateNativeAotNdk ()
	{
		CreateNdk ("ld.lld", "llvm-objcopy", "llvm-strip");
		Directory.CreateDirectory (Path.Combine (ToolchainDirectory, "sysroot", "usr", "lib"));
		Directory.CreateDirectory (Path.Combine (ToolchainDirectory, "lib", "clang", "20", "lib", "linux"));
	}
}
