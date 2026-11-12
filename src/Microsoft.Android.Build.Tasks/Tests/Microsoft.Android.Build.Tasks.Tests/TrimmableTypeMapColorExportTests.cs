using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;

using Microsoft.Android.Sdk.TrimmableTypeMap;

using Mono.Cecil;
using Mono.Cecil.Cil;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class TrimmableTypeMapColorExportTests
{
	[TestCase (0, false)]
	[TestCase (0, true)]
	[TestCase (1, false)]
	[TestCase (3, false)]
	public void ExportedColorSignaturesUseJavaInt (int arrayDepth, bool isStatic)
	{
		var peer = ScanExport (arrayDepth, isStatic: isStatic);
		var method = peer.MarshalMethods.Single ();
		string descriptor = new string ('[', arrayDepth) + "I";
		Assert.AreEqual ($"({descriptor}){descriptor}", method.JniSignature);
		Assert.IsTrue (method.IsExport);
		Assert.IsTrue (method.CallManagedMethodDirectly);

		using var writer = new StringWriter ();
		new JcwJavaSourceGenerator ().Generate (peer, writer);
		string javaType = "int" + string.Concat (Enumerable.Repeat ("[]", arrayDepth));
		string staticKeyword = isStatic ? "static " : "";
		StringAssert.Contains ($"public {staticKeyword}{javaType} EchoColor ({javaType} p0)", writer.ToString ());
		StringAssert.Contains ($"public {staticKeyword}native {javaType} {method.NativeCallbackName} ({javaType} p0);", writer.ToString ());
	}

	[TestCase (0)]
	[TestCase (1)]
	[TestCase (3)]
	public void ExportedColorConstructorsUseJavaInt (int arrayDepth)
	{
		var peer = ScanExport (arrayDepth, methodName: ".ctor");
		Assert.IsEmpty (peer.ConstructorDiagnostics);
		var constructor = peer.JavaConstructors.Single ();
		Assert.AreEqual ($"({new string ('[', arrayDepth)}I)V", constructor.JniSignature);
		Assert.IsTrue (constructor.HasMatchingManagedCtor);
	}

	[TestCase (0)]
	[TestCase (3)]
	public void ColorFromAnotherAssemblyIsRejected (int arrayDepth)
	{
		Assert.IsEmpty (ScanExport (arrayDepth, frameworkAssemblyName: "Lookalike").MarshalMethods);
	}

	[TestCase ("EchoColor", "Mono.Android", false, true)]
	[TestCase (".ctor", "Mono.Android", false, true)]
	[TestCase ("EchoColor", "Lookalike", true, false)]
	[TestCase (".ctor", "Lookalike", true, false)]
	public void ColorConversionRequiresFrameworkColorIdentity (string methodName, string frameworkAssemblyName, bool colorIsEnum, bool expectColorConversion)
	{
		var peer = ScanExport (0, methodName: methodName, frameworkAssemblyName: frameworkAssemblyName, colorIsEnum: colorIsEnum);
		string signature = methodName == ".ctor" ? peer.JavaConstructors.Single ().JniSignature : peer.MarshalMethods.Single ().JniSignature;
		Assert.AreEqual (methodName == ".ctor" ? "(I)V" : "(I)I", signature);

		var colorMembers = GetColorMemberReferences (peer);
		string [] expected = !expectColorConversion ? [] : methodName == ".ctor" ? [".ctor"] : [".ctor", "op_Implicit"];
		CollectionAssert.AreEquivalent (expected, colorMembers);
	}

	static List<string> GetColorMemberReferences (JavaPeerInfo peer)
	{
		// The synthetic peer has no activation ctor; give it one so a proxy with UCO wrappers is emitted.
		peer = peer with {
			ActivationCtor = new ActivationCtorInfo {
				DeclaringTypeName = peer.ManagedTypeName,
				DeclaringAssemblyName = peer.AssemblyName,
				Style = ActivationCtorStyle.XamarinAndroid,
			},
		};
		using var stream = new MemoryStream ();
		new TypeMapAssemblyGenerator (new Version (11, 0)).Generate ([peer], stream, "_ColorExports.TypeMap");
		stream.Position = 0;
		using var reader = new PEReader (stream);
		var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader (reader);
		var members = new List<string> ();
		foreach (var handle in metadata.MemberReferences) {
			var member = metadata.GetMemberReference (handle);
			if (member.Parent.Kind != System.Reflection.Metadata.HandleKind.TypeReference)
				continue;
			var type = metadata.GetTypeReference ((System.Reflection.Metadata.TypeReferenceHandle) member.Parent);
			if (metadata.GetString (type.Namespace) == "Android.Graphics" && metadata.GetString (type.Name) == "Color")
				members.Add (metadata.GetString (member.Name));
		}
		return members;
	}

	static JavaPeerInfo ScanExport (int arrayDepth, string methodName = "EchoColor", bool isStatic = false,
		string frameworkAssemblyName = "Mono.Android", bool colorIsEnum = false)
	{
		using var framework = AssemblyDefinition.CreateAssembly (
			new AssemblyNameDefinition (frameworkAssemblyName, new Version (1, 0)),
			$"{frameworkAssemblyName}.dll", ModuleKind.Dll);
		var module = framework.MainModule;
		var javaObject = new TypeDefinition ("Java.Lang", "Object",
			TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
		javaObject.CustomAttributes.Add (CreateStringAttribute (module,
			new TypeReference ("Android.Runtime", "RegisterAttribute", module, module),
			"java/lang/Object"));
		module.Types.Add (javaObject);
		var color = new TypeDefinition ("Android.Graphics", "Color",
			TypeAttributes.Public | TypeAttributes.Sealed | (colorIsEnum ? default : TypeAttributes.SequentialLayout),
			module.ImportReference (colorIsEnum ? typeof (Enum) : typeof (ValueType)));
		if (colorIsEnum) {
			color.Fields.Add (new FieldDefinition ("value__",
				FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName,
				module.TypeSystem.Int32));
		}
		module.Types.Add (color);

		using var application = AssemblyDefinition.CreateAssembly (
			new AssemblyNameDefinition ("ColorExports", new Version (1, 0)),
			"ColorExports.dll", ModuleKind.Dll);
		module = application.MainModule;
		var peer = new TypeDefinition ("Example", "ColorExportPeer", TypeAttributes.Public | TypeAttributes.Class,
			module.ImportReference (javaObject));
		module.Types.Add (peer);
		TypeReference valueType = module.ImportReference (color);
		for (int i = 0; i < arrayDepth; i++)
			valueType = new ArrayType (valueType);

		var attributes = MethodAttributes.Public | MethodAttributes.HideBySig;
		if (isStatic)
			attributes |= MethodAttributes.Static;
		bool isConstructor = methodName == ".ctor";
		if (isConstructor)
			attributes |= MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
		var method = new MethodDefinition (methodName, attributes, isConstructor ? module.TypeSystem.Void : valueType);
		method.CustomAttributes.Add (CreateStringAttribute (module,
			new TypeReference ("Java.Interop", "ExportAttribute", module, peer.BaseType.Scope), methodName));
		method.Parameters.Add (new ParameterDefinition ("color", ParameterAttributes.None, valueType));
		if (!isConstructor)
			method.Body.Instructions.Add (Instruction.Create (isStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1));
		method.Body.Instructions.Add (Instruction.Create (OpCodes.Ret));
		peer.Methods.Add (method);

		using var frameworkStream = WriteAssembly (framework);
		using var applicationStream = WriteAssembly (application);
		using var frameworkReader = new PEReader (frameworkStream);
		using var applicationReader = new PEReader (applicationStream);
		using var scanner = new JavaPeerScanner ();
		return scanner.Scan ([
			(frameworkAssemblyName, frameworkReader),
			("ColorExports", applicationReader),
		]).Single (peer => peer.ManagedTypeName == "Example.ColorExportPeer");
	}

	static CustomAttribute CreateStringAttribute (ModuleDefinition module, TypeReference attributeType, string argument)
	{
		var constructor = new MethodReference (".ctor", module.TypeSystem.Void, attributeType) { HasThis = true };
		constructor.Parameters.Add (new ParameterDefinition (module.TypeSystem.String));
		var attribute = new CustomAttribute (constructor);
		attribute.ConstructorArguments.Add (new CustomAttributeArgument (module.TypeSystem.String, argument));
		return attribute;
	}

	static MemoryStream WriteAssembly (AssemblyDefinition assembly)
	{
		var stream = new MemoryStream ();
		assembly.Write (stream);
		stream.Position = 0;
		return stream;
	}
}
