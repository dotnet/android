using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class GenerateNativeRuntimeLinkingSourcesTests : BaseTest
{
	[TestCase ("linked")]
	[TestCase ("R2R")]
	public void NativeLinkingIgnoresDuplicateNonFrameworkAssemblies (string finalDirectory)
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		try {
			var arm64Framework = CreateAssembly (Path.Combine (path, "android-arm64", finalDirectory),
				"Framework", "arm64-v8a", "SystemNative_Arm64", framework: true);
			var x64Framework = CreateAssembly (Path.Combine (path, "android-x64", finalDirectory),
				"Framework", "x86_64", "SystemNative_X64", framework: true);
			var resolvedAssemblies = new List<ITaskItem> { arm64Framework, x64Framework };
			foreach (var name in new [] { "_Microsoft.Android.TypeMaps", "_Java.Interop.TypeMap" }) {
				resolvedAssemblies.Add (CreateAssembly (Path.Combine (path, "android-arm64", finalDirectory),
					name, "arm64-v8a", "SystemNative_FinalTypeMap", framework: false));
				resolvedAssemblies.Add (CreateAssembly (Path.Combine (path, "android-x64", finalDirectory),
					name, "x86_64", "SystemNative_FinalTypeMap", framework: false));
				var preTrim = CreateAssembly (Path.Combine (path, "typemap"), name, "arm64-v8a",
					"SystemNative_PreTrimTypeMap", framework: false);
				preTrim.SetMetadata ("_AndroidPreTrimTypeMapCandidate", "true");
				resolvedAssemblies.Add (preTrim);
			}
			var userAssembly = CreateAssembly (Path.Combine (path, "user"), "User", "arm64-v8a",
				"SystemNative_User", framework: false);
			userAssembly.RemoveMetadata ("FrameworkAssembly");
			resolvedAssemblies.Add (userAssembly);
			resolvedAssemblies.Add (userAssembly);
			var errors = new List<BuildErrorEventArgs> ();
			var task = new GenerateNativeRuntimeLinkingSources {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				EnvironmentOutputDirectory = path,
				ResolvedAssemblies = resolvedAssemblies.ToArray (),
				SupportedAbis = ["arm64-v8a", "x86_64"],
			};

			Assert.IsTrue (task.Execute (), "Native linking must ignore pre-trim and final typemap duplicates before ABI indexing.");
			Assert.IsEmpty (errors);
			var arm64 = File.ReadAllText (Path.Combine (path, "pinvoke_preserve.arm64-v8a.ll"));
			var x64 = File.ReadAllText (Path.Combine (path, "pinvoke_preserve.x86_64.ll"));
			StringAssert.Contains ("@SystemNative_Arm64", arm64);
			StringAssert.DoesNotContain ("@SystemNative_X64", arm64);
			StringAssert.Contains ("@SystemNative_X64", x64);
			StringAssert.DoesNotContain ("@SystemNative_Arm64", x64);
			foreach (var output in new [] { arm64, x64 }) {
				StringAssert.DoesNotContain ("@SystemNative_FinalTypeMap", output);
				StringAssert.DoesNotContain ("@SystemNative_PreTrimTypeMap", output);
				StringAssert.DoesNotContain ("@SystemNative_User", output);
			}
			foreach (var abi in task.SupportedAbis)
				Assert.IsFalse (File.Exists (Path.Combine (path, $"marshal_methods.{abi}.ll")), "Legacy marshal-method stubs must not be generated.");
		} finally {
			Directory.Delete (path, recursive: true);
		}
	}

	static TaskItem CreateAssembly (string directory, string name, string abi, string entryPoint, bool framework)
	{
		Directory.CreateDirectory (directory);
		var path = Path.Combine (directory, $"{name}.dll");
		using var assembly = AssemblyDefinition.CreateAssembly (
			new AssemblyNameDefinition (name, new Version (1, 0)), name, ModuleKind.Dll);
		var module = assembly.MainModule;
		var nativeModule = new ModuleReference ("System.Native");
		module.ModuleReferences.Add (nativeModule);
		var type = new TypeDefinition ("Example", "NativeMethods", TypeAttributes.Public, module.TypeSystem.Object);
		type.Methods.Add (new MethodDefinition ("Invoke", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.PInvokeImpl,
			module.TypeSystem.Void) {
			PInvokeInfo = new PInvokeInfo (PInvokeAttributes.CallConvCdecl, entryPoint, nativeModule),
		});
		module.Types.Add (type);
		assembly.Write (path);
		var item = new TaskItem (path);
		item.SetMetadata ("Abi", abi);
		item.SetMetadata ("FrameworkAssembly", framework.ToString ());
		return item;
	}
}
