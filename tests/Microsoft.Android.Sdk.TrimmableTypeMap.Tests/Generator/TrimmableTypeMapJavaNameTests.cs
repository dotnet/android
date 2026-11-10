using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class TrimmableTypeMapJavaNameTests
{
	public static IEnumerable<object []> SourceReferenceCases ()
	{
		foreach (string simpleName in new [] { "Outer$NamedComponent", "Outer$for", "Outer$var", "Outer$1" }) {
			foreach (string referenceKind in new [] { "constructor", "method", "field", "base", "constructor-throws", "method-throws" }) {
				foreach (bool external in new [] { false, true }) {
					yield return new object [] { simpleName, referenceKind, external };
				}
			}
		}
	}

	[Theory]
	[MemberData (nameof (SourceReferenceCases))]
	public void SourceReferencesDistinguishGeneratedAndExternalTypes (
		string simpleName,
		string referenceKind,
		bool external)
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
		var generator = new TrimmableTypeMapGenerator (new NoOpTrimmableTypeMapLogger ());
		bool expectedValid = !external || simpleName == "Outer$NamedComponent";
		Assert.Equal (expectedValid, generator.ValidateJavaNames ([target, consumer]));
		if (!expectedValid) {
			return;
		}

		var java = new JcwJavaSourceGenerator ().GenerateContent ([target, consumer])
			.Single (source => source.RelativePath == "example/Consumer.java").Content;
		string expectedTypeName = "example.components." + (external ? simpleName.Replace ('$', '.') : simpleName);
		Assert.Contains (expectedTypeName, java);
	}

	[Theory]
	[InlineData ("Outer$for")]
	[InlineData ("Outer$var")]
	[InlineData ("Outer$1")]
	public void DeferredRegistrationUsesWholeGeneratedIdentifier (string simpleName)
	{
		var peer = CreatePeer ("example/components/" + simpleName) with {
			CannotRegisterInStaticConstructor = true,
		};
		var peers = new List<JavaPeerInfo> { peer };
		var generator = new TrimmableTypeMapGenerator (new NoOpTrimmableTypeMapLogger ());

		Assert.True (generator.ValidateJavaNames (peers));
		Assert.Equal (new [] { "example.components." + simpleName },
			TrimmableTypeMapGenerator.CollectApplicationRegistrationTypes (peers));
	}

	[Fact]
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
		var generator = new TrimmableTypeMapGenerator (new NoOpTrimmableTypeMapLogger ());
		Assert.True (generator.ValidateJavaNames ([target, consumer]));
	}

	[Theory]
	[InlineData ("Outer$Nested", true)]
	[InlineData ("Outer$for", false)]
	public void InterfacesKeepNestedTypeValidation (string simpleName, bool expectedValid)
	{
		var target = CreatePeer ("example/" + simpleName) with { IsInterface = true };
		var consumer = CreatePeer ("example/Consumer") with {
			ImplementedInterfaceJavaNames = [target.JavaName],
		};
		var generator = new TrimmableTypeMapGenerator (new NoOpTrimmableTypeMapLogger ());
		Assert.Equal (expectedValid, generator.ValidateJavaNames ([target, consumer]));
	}

	[Theory]
	[InlineData ("example/for")]
	[InlineData ("example/Outer-for")]
	[InlineData ("example/1Outer$for")]
	[InlineData ("for/Outer$for")]
	public void GeneratedWrappersStillRejectInvalidIdentifiers (string jniName)
	{
		var generator = new TrimmableTypeMapGenerator (new NoOpTrimmableTypeMapLogger ());
		Assert.False (generator.ValidateJavaNames ([CreatePeer (jniName)]));
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
