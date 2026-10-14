using System;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;

using Microsoft.Android.Sdk.TrimmableTypeMap;
using Mono.Cecil;
using Mono.Cecil.Cil;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class TrimmableTypeMapComponentSignatureTests
{
	[TestCase ("ActivityAttribute")]
	[TestCase ("ServiceAttribute")]
	[TestCase ("BroadcastReceiverAttribute")]
	[TestCase ("ContentProviderAttribute")]
	[TestCase ("ApplicationAttribute")]
	[TestCase ("InstrumentationAttribute")]
	public void ComponentNamesAreUsedInSignatures (string componentAttribute)
	{
		AssertComponentSignatures (componentAttribute, "example.media.ControlsService", registerName: null);
	}

	[Test]
	public void ComponentNamesAreUsedInSignaturesAcrossAssemblies ()
	{
		AssertComponentSignatures ("ServiceAttribute", "example.media.ControlsService", registerName: null, separateAssemblies: true);
	}

	[Test]
	public void RegisterNameTakesPrecedenceOverComponentName ()
	{
		AssertComponentSignatures ("ServiceAttribute", "example.media.ControlsService", "example/RegisteredService");
	}

	[Test]
	public void ComponentWithoutNameUsesGeneratedName ()
	{
		AssertComponentSignatures ("ServiceAttribute", componentName: null, registerName: null);
	}

	static void AssertComponentSignatures (string componentAttribute, string? componentName, string? registerName, bool separateAssemblies = false)
	{
		using var stream = CreateAssembly (componentAttribute, componentName, registerName, separateAssemblies, out var binderStream);
		using var binderAssemblyStream = binderStream;
		using var pe = new PEReader (stream);
		using var binderPe = binderAssemblyStream is not null ? new PEReader (binderAssemblyStream) : null;
		var componentInput = new AssemblyInput ("ComponentSignatures", "ComponentSignatures.dll", pe);
		// Scan the referencing assembly first to exercise the pre-indexed component name.
		AssemblyInput [] assemblies = binderPe is not null
			? [new AssemblyInput ("BinderSignatures", "BinderSignatures.dll", binderPe), componentInput]
			: [componentInput];
		var result = new TrimmableTypeMapGenerator (new TrimmableTypeMapTestLogger ()).Execute (
			assemblies,
			new Version (11, 0),
			[],
			generateTypeMapAssemblies: false);
		var component = result.AllPeers.Single (peer => peer.ManagedTypeName == "Example.Media.MediaControlsService");
		var binder = result.AllPeers.Single (peer => peer.ManagedTypeName == "Example.Services.BoundServiceBinder");
		if (separateAssemblies) {
			Assert.AreEqual ("BinderSignatures", binder.AssemblyName);
			Assert.AreEqual ("ComponentSignatures", component.AssemblyName);
		}
		var expectedName = registerName ?? componentName?.Replace ('.', '/') ?? component.JavaName;
		Assert.AreEqual (expectedName, component.JavaName);
		if (componentName is null && registerName is null) {
			StringAssert.StartsWith ("scrc64", component.JavaName);
		}

		var objectSignature = $"L{expectedName};";
		CollectionAssert.AreEquivalent (
			new [] { "()V", $"({objectSignature})V", $"([{objectSignature})V" },
			binder.JavaConstructors.Select (constructor => constructor.JniSignature));
		Assert.AreEqual ($"({objectSignature}){objectSignature}",
			binder.MarshalMethods.Single (method => method.JniName == "echo").JniSignature);
		Assert.AreEqual ($"([{objectSignature})[{objectSignature}",
			binder.MarshalMethods.Single (method => method.JniName == "echoArray").JniSignature);

		var javaName = expectedName.Replace ('/', '.');
		var java = result.GeneratedJavaSources.Single (source => source.RelativePath == binder.JavaName + ".java").Content;
		StringAssert.Contains ($"public BoundServiceBinder ({javaName} p0)", java);
		StringAssert.Contains ($"public BoundServiceBinder ({javaName}[] p0)", java);
		StringAssert.Contains ($"private native void nctor_1 ({javaName} p0);", java);
		StringAssert.Contains ($"private native void nctor_2 ({javaName}[] p0);", java);
		StringAssert.Contains ($"public {javaName} echo ({javaName} p0)", java);
		StringAssert.Contains ($"public {javaName}[] echoArray ({javaName}[] p0)", java);
		Assert.IsTrue (result.GeneratedJavaSources.Any (source => source.RelativePath == expectedName + ".java"),
			"The type referenced by the binder's signatures must have a matching Java wrapper.");
	}

	static MemoryStream CreateAssembly (string componentAttribute, string? componentName, string? registerName,
		bool separateAssemblies, out MemoryStream? binderStream)
	{
		using var assembly = AssemblyDefinition.CreateAssembly (
			new AssemblyNameDefinition ("ComponentSignatures", new Version (1, 0)),
			"ComponentSignatures.dll", ModuleKind.Dll);
		using var binderAssembly = separateAssemblies
			? AssemblyDefinition.CreateAssembly (new AssemblyNameDefinition ("BinderSignatures", new Version (1, 0)),
				"BinderSignatures.dll", ModuleKind.Dll)
			: null;
		var module = assembly.MainModule;
		var binderModule = binderAssembly?.MainModule ?? module;
		var monoAndroid = new AssemblyNameReference ("Mono.Android", new Version (1, 0));
		module.AssemblyReferences.Add (monoAndroid);
		var registerType = new TypeReference ("Android.Runtime", "RegisterAttribute", module, monoAndroid);
		var registerCtor = AttributeConstructor (registerType, module.TypeSystem.String);
		var methodRegisterCtor = AttributeConstructor (
			registerType, module.TypeSystem.String, module.TypeSystem.String, module.TypeSystem.String);
		var exportCtor = AttributeConstructor (new TypeReference ("Java.Interop", "ExportAttribute", module, monoAndroid),
			module.TypeSystem.String);

		var javaObject = new TypeDefinition ("Java.Lang", "Object", TypeAttributes.Public, module.TypeSystem.Object);
		var javaObjectRegister = StringAttribute (registerCtor, "java/lang/Object");
		javaObjectRegister.Properties.Add (new CustomAttributeNamedArgument ("DoNotGenerateAcw",
			new CustomAttributeArgument (module.TypeSystem.Boolean, true)));
		javaObject.CustomAttributes.Add (javaObjectRegister);
		module.Types.Add (javaObject);
		var baseConstructor = AddConstructor (javaObject);
		baseConstructor.CustomAttributes.Add (StringAttribute (methodRegisterCtor, ".ctor", "()V", ""));

		var binder = new TypeDefinition ("Example.Services", "BoundServiceBinder", TypeAttributes.Public, binderModule.ImportReference (javaObject));
		// Put the binder first so signature resolution cannot rely on the component already being scanned.
		binderModule.Types.Add (binder);
		var component = new TypeDefinition ("Example.Media", "MediaControlsService", TypeAttributes.Public, javaObject);
		module.Types.Add (component);
		var componentCtor = AttributeConstructor (new TypeReference ("Android.App", componentAttribute, module, monoAndroid));
		var attribute = new CustomAttribute (componentCtor);
		if (componentName is not null) {
			attribute.Properties.Add (new CustomAttributeNamedArgument ("Name",
				new CustomAttributeArgument (module.TypeSystem.String, componentName)));
		}
		component.CustomAttributes.Add (attribute);
		if (registerName is not null) {
			component.CustomAttributes.Add (StringAttribute (registerCtor, registerName));
		}
		AddConstructor (component);

		var componentReference = binderModule.ImportReference (component);
		var binderExportCtor = binderModule.ImportReference (exportCtor);
		AddConstructor (binder, componentReference);
		AddConstructor (binder, new ArrayType (componentReference));
		AddExport (binder, componentReference, binderExportCtor, "echo");
		AddExport (binder, new ArrayType (componentReference), binderExportCtor, "echoArray");

		var stream = new MemoryStream ();
		assembly.Write (stream);
		stream.Position = 0;
		binderStream = null;
		if (binderAssembly is not null) {
			binderStream = new MemoryStream ();
			binderAssembly.Write (binderStream);
			binderStream.Position = 0;
		}
		return stream;
	}

	static MethodReference AttributeConstructor (TypeReference type, params TypeReference [] parameterTypes)
	{
		var constructor = new MethodReference (".ctor", type.Module.TypeSystem.Void, type) { HasThis = true };
		foreach (var parameterType in parameterTypes) {
			constructor.Parameters.Add (new ParameterDefinition (parameterType));
		}
		return constructor;
	}

	static CustomAttribute StringAttribute (MethodReference constructor, params string [] arguments)
	{
		var attribute = new CustomAttribute (constructor);
		foreach (var argument in arguments) {
			attribute.ConstructorArguments.Add (new CustomAttributeArgument (constructor.Module.TypeSystem.String, argument));
		}
		return attribute;
	}

	static MethodDefinition AddConstructor (TypeDefinition type, params TypeReference [] parameterTypes)
	{
		var constructor = new MethodDefinition (".ctor",
			MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, type.Module.TypeSystem.Void);
		foreach (var parameterType in parameterTypes) {
			constructor.Parameters.Add (new ParameterDefinition (parameterType));
		}
		constructor.Body.Instructions.Add (Instruction.Create (OpCodes.Ret));
		type.Methods.Add (constructor);
		return constructor;
	}

	static void AddExport (TypeDefinition type, TypeReference parameterType, MethodReference exportCtor, string name)
	{
		var method = new MethodDefinition (name, MethodAttributes.Public, parameterType);
		method.Parameters.Add (new ParameterDefinition (parameterType));
		method.CustomAttributes.Add (StringAttribute (exportCtor, name));
		method.Body.Instructions.Add (Instruction.Create (OpCodes.Ldarg_1));
		method.Body.Instructions.Add (Instruction.Create (OpCodes.Ret));
		type.Methods.Add (method);
	}
}
