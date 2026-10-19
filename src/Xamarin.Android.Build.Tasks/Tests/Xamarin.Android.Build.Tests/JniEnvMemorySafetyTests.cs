using System;
using System.IO;
using System.Linq;

using Mono.Cecil;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class JniEnvMemorySafetyTests : BaseTest
{
	const string RulesAttribute = "System.Runtime.CompilerServices.MemorySafetyRulesAttribute";
	const string UnsafeAttribute = "System.Diagnostics.CodeAnalysis.RequiresUnsafeAttribute";

	[Test]
	public void RuntimeCreationRequiresUnsafeOnDerivedConstructor ()
	{
		var project = new XamarinAndroidLibraryProject {
			ProjectName = "RuntimeCreationContract",
		};
		project.SetProperty ("AllowUnsafeBlocks", "true");
		project.SetProperty ("LangVersion", "preview");
		project.SetProperty ("Features", "updated-memory-safety-rules");
		var source = new BuildItem.Source ("Runtime.cs") {
			TextContent = () => """
				public sealed class Runtime : Java.Interop.JniRuntime {
					public Runtime (CreationOptions options) : base (options) {}
				}
				""",
		};
		project.Sources.Add (source);
		using var builder = CreateDllBuilder (Path.Combine ("temp", TestName));
		builder.ThrowOnBuildFailure = false;
		Assert.IsFalse (builder.Build (project), "A safe derived constructor cannot hide native pointers in CreationOptions.");
		StringAssert.Contains ("CS9362", string.Join (Environment.NewLine, builder.LastBuildOutput));

		source.TextContent = () => """
			public sealed class Runtime : Java.Interop.JniRuntime {
				public unsafe Runtime (CreationOptions options) : base (options) {}
			}
			""";
		project.Touch ("Runtime.cs");
		Assert.IsTrue (builder.Build (project), "An unsafe constructor provides the audited initializer context.");
	}

	[TestCase ("TargetJniTypeUtf8Pointer")]
	[TestCase ("TargetJniMethodNameUtf8Pointer")]
	[TestCase ("TargetJniMethodSignatureUtf8Pointer")]
	public void RemappingPointerRequiresUnsafeSetterOnly (string property)
	{
		var project = new XamarinAndroidLibraryProject {
			ProjectName = "RemappingPointerContract",
		};
		project.SetProperty ("AllowUnsafeBlocks", "true");
		project.SetProperty ("LangVersion", "preview");
		project.SetProperty ("Features", "updated-memory-safety-rules");
		var source = new BuildItem.Source ("Consumer.cs") {
			TextContent = () => $$"""
				public static class Consumer {
					public static void Read (Java.Interop.JniRuntime.ReplacementMethodInfo value) {
						var address = value.{{property}};
					}
					public static void Write (ref Java.Interop.JniRuntime.ReplacementMethodInfo value, byte* address) {
						value.{{property}} = address;
					}
				}
				""",
		};
		project.Sources.Add (source);
		using var builder = CreateDllBuilder (Path.Combine ("temp", TestName));
		builder.ThrowOnBuildFailure = false;
		Assert.IsFalse (builder.Build (project), "Publishing borrowed storage requires an unsafe caller.");
		StringAssert.Contains ("CS9362", string.Join (Environment.NewLine, builder.LastBuildOutput));

		source.TextContent = () => $$"""
			public static class Consumer {
				public static void Read (Java.Interop.JniRuntime.ReplacementMethodInfo value) {
					var address = value.{{property}};
				}
				public static void Write (ref Java.Interop.JniRuntime.ReplacementMethodInfo value, byte* address) {
					unsafe {
						value.{{property}} = address;
					}
				}
			}
			""";
		project.Touch ("Consumer.cs");
		Assert.IsTrue (builder.Build (project), "Reading an address remains safe while a local region acknowledges publication.");
	}

	[TestCase (false)]
	[TestCase (true)]
	public void GeneratedContractsCompileAndEnforceRawCalls (bool updatedRules)
	{
		var directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		var generated = Path.Combine (directory, "JNIEnv.g.cs");
		var dotnet = Path.Combine (TestEnvironment.DotNetPreviewDirectory, IsWindows ? "dotnet.exe" : "dotnet");
		var generator = Path.Combine (XABuildPaths.TopDirectory, "bin", $"Build{XABuildPaths.Configuration}", "jnienv-gen.dll");
		var generation = RunProcessWithExitCode (dotnet, $"\"{generator}\" -o=\"{generated}\"");
		Assert.AreEqual (0, generation.code, generation.stdOutput + generation.stdError);
		FileAssert.Exists (generated);
		StringAssert.Contains ("<safety>", File.ReadAllText (generated));
		StringAssert.Contains ("Pointer storage must remain readable", File.ReadAllText (generated));
		StringAssert.DoesNotContain ("JniNativeInterface", File.ReadAllText (generated));

		var contracts = new XamarinAndroidLibraryProject {
			ProjectName = "RawJniContracts",
		};
		contracts.SetProperty ("AllowUnsafeBlocks", "true");
		contracts.SetProperty ("LangVersion", updatedRules ? "preview" : "13.0");
		contracts.SetProperty ("Features", updatedRules ? "updated-memory-safety-rules" : "");
		contracts.SetProperty ("ProduceReferenceAssembly", "true");
		contracts.Sources.Add (new BuildItem.Source ("JNIEnv.g.cs") {
			TextContent = () => File.ReadAllText (generated),
		});
		using var contractBuilder = CreateDllBuilder (Path.Combine ("temp", TestName, "contracts"));
		Assert.IsTrue (contractBuilder.Build (contracts), "The canonical output must compile without customer preview syntax in legacy mode.");

		var projectDirectory = Path.Combine (Root, contractBuilder.ProjectDirectory);
		var implementation = Path.Combine (projectDirectory, contracts.OutputPath, $"{contracts.ProjectName}.dll");
		FileAssert.Exists (implementation);
		var reference = Directory.EnumerateFiles (Path.Combine (projectDirectory, "obj"), $"{contracts.ProjectName}.dll", SearchOption.AllDirectories)
			.Single (path => Path.GetFileName (Path.GetDirectoryName (path)) == "ref");
		foreach (var path in new [] { implementation, reference }) {
			using var assembly = AssemblyDefinition.ReadAssembly (path);
			var rules = assembly.MainModule.CustomAttributes.SingleOrDefault (attribute => attribute.AttributeType.FullName == RulesAttribute);
			Assert.AreEqual (updatedRules, rules != null, $"rules marker in {path}");
			if (rules != null)
				Assert.AreEqual (2, rules.ConstructorArguments [0].Value, "Pinned compiler rules version");
			var jni = assembly.MainModule.GetType ("Android.Runtime.JNIEnv");
			Assert.IsNotNull (jni, path);
			var rawCall = jni.Methods.Single (method => method.Name == "CallIntMethod" && method.Parameters.Count == 2);
			Assert.AreEqual (updatedRules, RequiresUnsafe (rawCall), "Even zero-argument raw method IDs carry a receiver/return ABI obligation.");
			Assert.AreEqual (updatedRules, RequiresUnsafe (jni.Methods.Single (method => method.Name == "NewDirectByteBuffer")));
			Assert.AreEqual (updatedRules, RequiresUnsafe (jni.Methods.Single (method => method.Name == "GetDirectBufferAddress")),
				"The raw buffer input carries the obligation, not the pointer return.");
			Assert.IsFalse (RequiresUnsafe (jni.Methods.Single (method => method.Name == "NewArray" &&
				method.Parameters [0].ParameterType.FullName == "System.Int32[]")), "Owned managed array creation is safe.");
			Assert.IsFalse (RequiresUnsafe (jni.Methods.Single (method => method.Name == "ExceptionOccurred")),
				"Returning a JNI reference does not by itself require unsafe.");
		}

		var consumer = new XamarinAndroidLibraryProject {
			ProjectName = "JniConsumer",
		};
		consumer.SetProperty ("AllowUnsafeBlocks", "true");
		consumer.SetProperty ("LangVersion", "preview");
		consumer.SetProperty ("Features", "updated-memory-safety-rules");
		consumer.References.Add (new BuildItem.Reference (contracts.ProjectName) {
			Metadata = {
				{ "HintPath", implementation },
				{ "Aliases", "prepared" },
			},
		});
		var source = new BuildItem.Source ("Consumer.cs") {
			TextContent = () => """
				extern alias prepared;
				using PreparedJNIEnv = prepared::Android.Runtime.JNIEnv;
				public static class Consumer {
					public static int Raw (nint receiver, nint method) =>
						PreparedJNIEnv.CallIntMethod (receiver, method);
				}
				""",
		};
		consumer.Sources.Add (source);
		using var consumerBuilder = CreateDllBuilder (Path.Combine ("temp", TestName, "consumer"));
		consumerBuilder.ThrowOnBuildFailure = false;
		Assert.AreEqual (!updatedRules, consumerBuilder.Build (consumer), "New-mode callers must respect new-mode raw contracts.");
		if (updatedRules)
			StringAssert.Contains ("CS9362", string.Join (Environment.NewLine, consumerBuilder.LastBuildOutput));

		source.TextContent = () => """
			extern alias prepared;
			using PreparedJNIEnv = prepared::Android.Runtime.JNIEnv;
			public static class Consumer {
				public static nint Owned (int[] values) => PreparedJNIEnv.NewArray (values);
				public static int Raw (nint receiver, nint method) {
					unsafe {
						// Non-executed contract probe: the caller owns the documented receiver/member ABI.
						return PreparedJNIEnv.CallIntMethod (receiver, method);
					}
				}
			}
			""";
		consumer.Touch ("Consumer.cs");
		Assert.IsTrue (consumerBuilder.Build (consumer), "A local region discharges raw use without making managed array creation unsafe.");

		source.TextContent = () => """
			extern alias prepared;
			using PreparedJNIEnv = prepared::Android.Runtime.JNIEnv;
			public static class Consumer {
				public static int Raw (nint receiver, nint method) =>
					PreparedJNIEnv.CallIntMethod (receiver, method);
			}
			""";
		consumer.Touch ("Consumer.cs");
		consumer.SetProperty ("LangVersion", "13.0");
		consumer.SetProperty ("Features", "");
		Assert.IsTrue (consumerBuilder.Build (consumer), "Legacy non-pointer consumers do not enforce RequiresUnsafe metadata.");
	}

	static bool RequiresUnsafe (MethodDefinition method)
		=> method.CustomAttributes.Any (attribute => attribute.AttributeType.FullName == UnsafeAttribute);
}
