#nullable enable

using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Microsoft.Build.Utilities;
using Cecil = Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;

using Xamarin.Android.Tasks;
using Xamarin.Android.Tasks.JniRemapping;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class JniRemappingAssemblyScannerTests : BaseTest
	{
		[Test]
		public void RecordsOnlyMappingsReferencedBySurvivingMetadata ()
		{
			string path = Path.Combine (Root, "temp", TestName, "Linked.dll");
			Directory.CreateDirectory (Path.Combine (Root, "temp", TestName));
			CreateFixture (path);

			R8Mapping mapping = R8Mapping.Parse (new StringReader ("""
				com.contoso.Peer -> a.b:
				    void onClick() -> c
				    int value -> d
				com.contoso.Unused -> a.e:
				    void unused() -> f

				"""));
			var engine = new MockBuildEngine (TestContext.Out);
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine,
			};

			using var stream = File.OpenRead (path);
			using var peReader = new PEReader (stream);
			var reader = peReader.GetMetadataReader ();
			JniRemappingAssemblyScanner.Scan (reader, mapping, new TaskLoggingHelper (task));

			CollectionAssert.AreEquivalent (new [] {
				"C\tcom/contoso/Peer",
				"M\tcom/contoso/Peer\tonClick():void",
				"F\tcom/contoso/Peer\tvalue",
			}, mapping.AccessedEntries);
		}

		[Test]
		public void TypeMapAttributeRetainsGeneratedMappings ()
		{
			string directory = Path.Combine (Root, "temp", TestName);
			string path = Path.Combine (directory, "TypeMap.dll");
			Directory.CreateDirectory (directory);
			CreateTypeMapFixture (path);

			R8Mapping mapping = R8Mapping.Parse (new StringReader ("""
				com.contoso.ProxyPeer -> a.b:
				    void callback() -> c
				    int value -> d

				"""));
			var task = new GenerateR8JniRemapping {
				BuildEngine = new MockBuildEngine (TestContext.Out),
			};

			using var stream = File.OpenRead (path);
			using var peReader = new PEReader (stream);
			JniRemappingAssemblyScanner.Scan (peReader.GetMetadataReader (), mapping, new TaskLoggingHelper (task));

			CollectionAssert.AreEquivalent (new [] {
				"C\tcom/contoso/ProxyPeer",
				"M\tcom/contoso/ProxyPeer\tcallback():void",
				"F\tcom/contoso/ProxyPeer\tvalue",
			}, mapping.AccessedEntries);
		}

		[TestCase ("com/contoso/ProxyPeer[+1]", true)]
		[TestCase ("com/contoso/ProxyPeer[ 1]", true)]
		[TestCase ("com/contoso/ProxyPeer[1]", false)]
		public void TypeMapAttributeValidatesAliasSuffix (string key, bool throws)
		{
			string directory = Path.Combine (Root, "temp", TestName);
			string path = Path.Combine (directory, "TypeMap.dll");
			Directory.CreateDirectory (directory);
			CreateTypeMapFixture (path, key);
			R8Mapping mapping = R8Mapping.Parse (new StringReader ("com.contoso.ProxyPeer -> a.b:\n"));
			var task = new GenerateR8JniRemapping {
				BuildEngine = new MockBuildEngine (TestContext.Out),
			};

			using var stream = File.OpenRead (path);
			using var peReader = new PEReader (stream);
			void Scan () => JniRemappingAssemblyScanner.Scan (peReader.GetMetadataReader (), mapping, new TaskLoggingHelper (task));

			if (throws) {
				Assert.Throws<BadImageFormatException> (Scan);
			} else {
				Assert.DoesNotThrow (Scan);
			}
		}

		[Test]
		public void TypeMapAttributeValidatesConstructorSignature ()
		{
			string directory = Path.Combine (Root, "temp", TestName);
			string path = Path.Combine (directory, "TypeMap.dll");
			Directory.CreateDirectory (directory);
			CreateTypeMapFixture (path, "com/contoso/ProxyPeer", includeTypeArgument: false);
			R8Mapping mapping = R8Mapping.Parse (new StringReader ("com.contoso.ProxyPeer -> a.b:\n"));
			var task = new GenerateR8JniRemapping {
				BuildEngine = new MockBuildEngine (TestContext.Out),
			};

			using var stream = File.OpenRead (path);
			using var peReader = new PEReader (stream);
			Assert.Throws<BadImageFormatException> (() =>
				JniRemappingAssemblyScanner.Scan (peReader.GetMetadataReader (), mapping, new TaskLoggingHelper (task)));
		}

		[Test]
		public void IgnoresUserDefinedTypeMapAttribute ()
		{
			string directory = Path.Combine (Root, "temp", TestName);
			string path = Path.Combine (directory, "TypeMap.dll");
			Directory.CreateDirectory (directory);
			CreateUserTypeMapFixture (path);
			R8Mapping mapping = R8Mapping.Parse (new StringReader ("com.contoso.ProxyPeer -> a.b:\n"));
			var task = new GenerateR8JniRemapping {
				BuildEngine = new MockBuildEngine (TestContext.Out),
			};

			using var stream = File.OpenRead (path);
			using var peReader = new PEReader (stream);
			Assert.DoesNotThrow (() =>
				JniRemappingAssemblyScanner.Scan (peReader.GetMetadataReader (), mapping, new TaskLoggingHelper (task)));
			CollectionAssert.IsEmpty (mapping.AccessedEntries);
		}

		static void CreateFixture (string path)
		{
			using var assembly = Cecil.AssemblyDefinition.CreateAssembly (
				new Cecil.AssemblyNameDefinition ("Linked", new System.Version (1, 0)),
				"Linked",
				Cecil.ModuleKind.Dll);
			Cecil.ModuleDefinition module = assembly.MainModule;
			Cecil.TypeReference attributeType = module.ImportReference (typeof (System.Attribute));

			Cecil.TypeDefinition registerAttribute = AddAttribute (module, attributeType, "Android.Runtime", "RegisterAttribute", 3);
			Cecil.MethodReference registerCtor1 = registerAttribute.Methods [0];
			Cecil.MethodReference registerCtor3 = registerAttribute.Methods [1];

			var peer = new Cecil.TypeDefinition ("Com.Contoso", "Peer", Cecil.TypeAttributes.Public | Cecil.TypeAttributes.Class, module.TypeSystem.Object);
			peer.CustomAttributes.Add (Attribute (registerCtor1, "com/contoso/Peer"));
			module.Types.Add (peer);

			var method = new Cecil.MethodDefinition ("OnClick", Cecil.MethodAttributes.Public, module.TypeSystem.Void);
			method.Body.Instructions.Add (Instruction.Create (OpCodes.Ret));
			method.CustomAttributes.Add (Attribute (registerCtor3, "onClick", "()V", "n_OnClick"));
			peer.Methods.Add (method);

			var field = new Cecil.FieldDefinition ("Value", Cecil.FieldAttributes.Public, module.TypeSystem.Int32);
			field.CustomAttributes.Add (Attribute (registerCtor1, "value"));
			peer.Fields.Add (field);

			assembly.Write (path);
		}

		static void CreateTypeMapFixture (string path, string key = "com/contoso/ProxyPeer[1]", bool includeTypeArgument = true)
		{
			using var assembly = Cecil.AssemblyDefinition.CreateAssembly (
				new Cecil.AssemblyNameDefinition ("TypeMap", new System.Version (1, 0)),
				"TypeMap",
				Cecil.ModuleKind.Dll);
			Cecil.ModuleDefinition module = assembly.MainModule;
			var systemRuntime = new Cecil.AssemblyNameReference ("System.Runtime", new Version (11, 0, 0, 0));
			var systemRuntimeInteropServices = new Cecil.AssemblyNameReference ("System.Runtime.InteropServices", new Version (11, 0, 0, 0));
			module.AssemblyReferences.Add (systemRuntime);
			module.AssemblyReferences.Add (systemRuntimeInteropServices);
			var attributeType = new Cecil.TypeReference (
				"System.Runtime.InteropServices",
				"TypeMapAttribute`1",
				module,
				systemRuntimeInteropServices);
			var closedAttribute = new Cecil.GenericInstanceType (attributeType);
			var systemObject = new Cecil.TypeReference ("System", "Object", module, systemRuntime);
			var systemType = new Cecil.TypeReference ("System", "Type", module, systemRuntime);
			closedAttribute.GenericArguments.Add (systemObject);
			var constructor = new Cecil.MethodReference (".ctor", module.TypeSystem.Void, closedAttribute) {
				HasThis = true,
			};
			constructor.Parameters.Add (new Cecil.ParameterDefinition (module.TypeSystem.String));
			if (includeTypeArgument) {
				constructor.Parameters.Add (new Cecil.ParameterDefinition (systemType));
			}
			var attribute = new Cecil.CustomAttribute (constructor);
			attribute.ConstructorArguments.Add (new Cecil.CustomAttributeArgument (module.TypeSystem.String, key));
			if (includeTypeArgument) {
				attribute.ConstructorArguments.Add (new Cecil.CustomAttributeArgument (
					systemType,
					systemObject));
			}
			assembly.CustomAttributes.Add (attribute);
			assembly.Write (path);
		}

		static void CreateUserTypeMapFixture (string path)
		{
			using var assembly = Cecil.AssemblyDefinition.CreateAssembly (
				new Cecil.AssemblyNameDefinition ("TypeMap", new Version (1, 0)),
				"TypeMap",
				Cecil.ModuleKind.Dll);
			Cecil.ModuleDefinition module = assembly.MainModule;
			var attribute = new Cecil.TypeDefinition (
				"System.Runtime.InteropServices",
				"TypeMapAttribute`1",
				Cecil.TypeAttributes.Public | Cecil.TypeAttributes.Class,
				module.ImportReference (typeof (Attribute)));
			attribute.GenericParameters.Add (new Cecil.GenericParameter ("T", attribute));
			module.Types.Add (attribute);
			var constructor = new Cecil.MethodDefinition (
				".ctor",
				Cecil.MethodAttributes.Public | Cecil.MethodAttributes.SpecialName | Cecil.MethodAttributes.RTSpecialName,
				module.TypeSystem.Void);
			constructor.Parameters.Add (new Cecil.ParameterDefinition (module.TypeSystem.String));
			constructor.Parameters.Add (new Cecil.ParameterDefinition (module.ImportReference (typeof (Type))));
			var il = constructor.Body.GetILProcessor ();
			il.Append (Instruction.Create (OpCodes.Ldarg_0));
			il.Append (Instruction.Create (OpCodes.Call, module.ImportReference (typeof (Attribute).GetConstructor (
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
				null,
				Type.EmptyTypes,
				null))));
			il.Append (Instruction.Create (OpCodes.Ret));
			attribute.Methods.Add (constructor);

			var closedAttribute = new Cecil.GenericInstanceType (attribute);
			closedAttribute.GenericArguments.Add (module.TypeSystem.Object);
			var constructorRef = new Cecil.MethodReference (".ctor", module.TypeSystem.Void, closedAttribute) {
				HasThis = true,
			};
			constructorRef.Parameters.Add (new Cecil.ParameterDefinition (module.TypeSystem.String));
			constructorRef.Parameters.Add (new Cecil.ParameterDefinition (module.ImportReference (typeof (Type))));
			var customAttribute = new Cecil.CustomAttribute (constructorRef);
			customAttribute.ConstructorArguments.Add (new Cecil.CustomAttributeArgument (module.TypeSystem.String, "com/contoso/ProxyPeer"));
			customAttribute.ConstructorArguments.Add (new Cecil.CustomAttributeArgument (
				module.ImportReference (typeof (Type)),
				module.TypeSystem.Object));
			assembly.CustomAttributes.Add (customAttribute);
			assembly.Write (path);
		}

		static Cecil.TypeDefinition AddAttribute (Cecil.ModuleDefinition module, Cecil.TypeReference attributeType, string ns, string name, int maxArguments)
		{
			var type = new Cecil.TypeDefinition (ns, name, Cecil.TypeAttributes.Public | Cecil.TypeAttributes.Class, attributeType);
			module.Types.Add (type);
			for (int argumentCount = 1; argumentCount <= maxArguments; argumentCount += 2) {
				var constructor = new Cecil.MethodDefinition (".ctor",
					Cecil.MethodAttributes.Public | Cecil.MethodAttributes.SpecialName | Cecil.MethodAttributes.RTSpecialName,
					module.TypeSystem.Void);
				for (int i = 0; i < argumentCount; i++) {
					constructor.Parameters.Add (new Cecil.ParameterDefinition (module.TypeSystem.String));
				}
				var il = constructor.Body.GetILProcessor ();
				il.Append (Instruction.Create (OpCodes.Ldarg_0));
				il.Append (Instruction.Create (OpCodes.Call, module.ImportReference (typeof (System.Attribute).GetConstructor (
					System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
					null,
					System.Type.EmptyTypes,
					null))));
				il.Append (Instruction.Create (OpCodes.Ret));
				type.Methods.Add (constructor);
			}
			return type;
		}

		static Cecil.CustomAttribute Attribute (Cecil.MethodReference constructor, params string [] values)
		{
			var attribute = new Cecil.CustomAttribute (constructor);
			foreach (string value in values) {
				attribute.ConstructorArguments.Add (new Cecil.CustomAttributeArgument (constructor.Module.TypeSystem.String, value));
			}
			return attribute;
		}
	}
}
