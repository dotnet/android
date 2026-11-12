using System.Collections.Generic;
using System.Linq;

using Microsoft.Android.Sdk.TrimmableTypeMap;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Xamarin.Android.Build.Tests;

[TestClass]
[DoNotParallelize]
public class TrimmableTypeMapJavaNameTests
{
	[TestMethod]
	[CombinatorialData]
	public void SourceReferencesDistinguishGeneratedAndExternalTypes (
		[CombinatorialValues ("Outer$NamedComponent", "Outer$for", "Outer$var", "Outer$1")] string simpleName,
		[CombinatorialValues ("constructor", "method", "field", "base", "constructor-throws", "method-throws")] string referenceKind,
		[CombinatorialValues (false, true)] bool external)
	{
		string jniName = "example/components/" + simpleName;
		var target = CreatePeer (jniName) with {
			DoNotGenerateAcw = external,
		};
		var consumer = CreatePeer ("example/Consumer");
		consumer = referenceKind switch {
			"constructor" => consumer with {
				JavaConstructors = [new JavaConstructorInfo {
					JniSignature = $"(L{jniName};[L{jniName};)V",
					ConstructorIndex = 0,
				}],
			},
			"method" => consumer with {
				MarshalMethods = [new MarshalMethodInfo {
					JniName = "echo",
					JniSignature = $"(L{jniName};[L{jniName};)[L{jniName};",
					ManagedMethodName = "Echo",
					NativeCallbackName = "n_echo",
					IsExport = true,
				}],
			},
			"field" => consumer with {
				JavaFields = [new JavaFieldInfo {
					FieldName = "component",
					JavaTypeName = $"example.components.{simpleName}[]",
					JniTypeName = $"[L{jniName};",
					InitializerMethodName = "createComponent",
					Visibility = "public",
				}],
			},
			"base" => consumer with { BaseJavaName = jniName },
			"constructor-throws" => consumer with {
				JavaConstructors = [new JavaConstructorInfo {
					JniSignature = "()V",
					ConstructorIndex = 0,
					ThrownNames = [jniName],
				}],
			},
			"method-throws" => consumer with {
				MarshalMethods = [new MarshalMethodInfo {
					JniName = "call",
					JniSignature = "()V",
					ManagedMethodName = "Call",
					NativeCallbackName = "n_call",
					IsExport = true,
					ThrownNames = [jniName],
				}],
			},
			_ => throw new System.ArgumentException (nameof (referenceKind)),
		};
		var generator = new TrimmableTypeMapGenerator (new TrimmableTypeMapTestLogger ());
		bool expectedValid = !external || simpleName == "Outer$NamedComponent";
		Assert.AreEqual (expectedValid, generator.ValidateJavaNames ([target, consumer]),
			"Only generated wrappers can preserve '$' instead of treating it as a nested-type separator.");
		if (!expectedValid) {
			return;
		}

		var java = new JcwJavaSourceGenerator ().GenerateContent ([target, consumer])
			.Single (source => source.RelativePath == "example/Consumer.java").Content;
		string expectedTypeName = "example.components." + (external ? simpleName.Replace ('$', '.') : simpleName);
		Assert.Contains (expectedTypeName, java);
	}

	[TestMethod]
	[DataRow ("Outer$for")]
	[DataRow ("Outer$var")]
	[DataRow ("Outer$1")]
	public void DeferredRegistrationUsesWholeGeneratedIdentifier (string simpleName)
	{
		var peer = CreatePeer ("example/components/" + simpleName) with {
			CannotRegisterInStaticConstructor = true,
		};
		var peers = new List<JavaPeerInfo> { peer };
		var generator = new TrimmableTypeMapGenerator (new TrimmableTypeMapTestLogger ());

		Assert.IsTrue (generator.ValidateJavaNames (peers));
		CollectionAssert.AreEqual (new [] { "example.components." + simpleName },
			TrimmableTypeMapGenerator.CollectApplicationRegistrationTypes (peers));
	}

	[TestMethod]
	public void ThrowsCanReferenceGeneratedTypeWithoutPackage ()
	{
		var target = CreatePeer ("Outer$for");
		var consumer = CreatePeer ("Consumer") with {
			JavaConstructors = [new JavaConstructorInfo {
				JniSignature = "()V",
				ConstructorIndex = 0,
				ThrownNames = [target.JavaName],
			}],
			MarshalMethods = [new MarshalMethodInfo {
				JniName = "call",
				JniSignature = "()V",
				ManagedMethodName = "Call",
				NativeCallbackName = "n_call",
				IsExport = true,
				ThrownNames = [target.JavaName],
			}],
		};
		var generator = new TrimmableTypeMapGenerator (new TrimmableTypeMapTestLogger ());
		Assert.IsTrue (generator.ValidateJavaNames ([target, consumer]));
	}

	[TestMethod]
	[DataRow ("Outer$Nested", true)]
	[DataRow ("Outer$for", false)]
	public void InterfacesKeepNestedTypeValidation (string simpleName, bool expectedValid)
	{
		var target = CreatePeer ("example/" + simpleName) with { IsInterface = true };
		var consumer = CreatePeer ("example/Consumer") with {
			ImplementedInterfaceJavaNames = [target.JavaName],
		};
		var generator = new TrimmableTypeMapGenerator (new TrimmableTypeMapTestLogger ());
		Assert.AreEqual (expectedValid, generator.ValidateJavaNames ([target, consumer]));
	}

	[TestMethod]
	[DataRow ("example/for")]
	[DataRow ("example/Outer-for")]
	[DataRow ("example/1Outer$for")]
	[DataRow ("for/Outer$for")]
	public void GeneratedWrappersStillRejectInvalidIdentifiers (string jniName)
	{
		var generator = new TrimmableTypeMapGenerator (new TrimmableTypeMapTestLogger ());
		Assert.IsFalse (generator.ValidateJavaNames ([CreatePeer (jniName)]));
	}

	static JavaPeerInfo CreatePeer (string jniName) => new () {
		JavaName = jniName,
		CompatJniName = jniName,
		ManagedTypeName = jniName.Replace ('/', '.'),
		ManagedTypeNamespace = "Example",
		ManagedTypeShortName = "Peer",
		AssemblyName = "Example",
		BaseJavaName = "java/lang/Exception",
	};
}
