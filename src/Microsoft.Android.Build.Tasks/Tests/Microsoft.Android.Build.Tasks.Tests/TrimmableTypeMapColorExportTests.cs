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

	static JavaPeerInfo ScanExport (int arrayDepth, string methodName = "EchoColor", bool isStatic = false,
		string frameworkAssemblyName = "Mono.Android")
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
			TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
			module.ImportReference (typeof (ValueType)));
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
