using System.Collections.Generic;
using System.Linq;

using Microsoft.Android.Sdk.TrimmableTypeMap;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class TrimmableTypeMapJavaNameTests
{
	[Test]
	public void SourceReferencesDistinguishGeneratedAndExternalTypes (
		[Values ("Outer$NamedComponent", "Outer$for", "Outer$var", "Outer$1")] string simpleName,
		[Values ("constructor", "method", "field", "base", "constructor-throws", "method-throws")] string referenceKind,
		[Values (false, true)] bool external)
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
		StringAssert.Contains (expectedTypeName, java);
	}

	[TestCase ("Outer$for")]
	[TestCase ("Outer$var")]
	[TestCase ("Outer$1")]
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

	[Test]
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

	[TestCase ("Outer$Nested", true)]
	[TestCase ("Outer$for", false)]
	public void InterfacesKeepNestedTypeValidation (string simpleName, bool expectedValid)
	{
		var target = CreatePeer ("example/" + simpleName) with { IsInterface = true };
		var consumer = CreatePeer ("example/Consumer") with {
			ImplementedInterfaceJavaNames = [target.JavaName],
		};
		var generator = new TrimmableTypeMapGenerator (new TrimmableTypeMapTestLogger ());
		Assert.AreEqual (expectedValid, generator.ValidateJavaNames ([target, consumer]));
	}

	[TestCase ("example/for")]
	[TestCase ("example/Outer-for")]
	[TestCase ("example/1Outer$for")]
	[TestCase ("for/Outer$for")]
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
