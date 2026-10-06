#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

using Microsoft.Build.Utilities;
using Cecil = Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;

using Xamarin.Android.Tasks;
using Xamarin.Android.Tasks.JniRemapping;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class JniRemappingAssemblyScannerTests : BaseTest
	{
		[Test]
		public void RecordsOnlyMappingsReferencedBySurvivingMetadata ()
		{
			string path = CreateAssembly (p => CreateFixture (p));
			R8Mapping mapping = Scan (path, """
				com.contoso.Peer -> a.b:
				    void onClick() -> c
				    int value -> d
				com.contoso.Unused -> a.e:
				    void unused() -> f

				""");

			CollectionAssert.AreEquivalent (new [] {
				"C\tcom/contoso/Peer",
				"M\tcom/contoso/Peer\tonClick():void",
				"F\tcom/contoso/Peer\tvalue",
			}, mapping.AccessedEntries);
		}

		[TestCase ("System.Runtime.InteropServices", "System.Runtime", false)]
		[TestCase ("System.Runtime.InteropServices", "System.Runtime", true)]
		[TestCase ("System.Private.CoreLib", "System.Private.CoreLib", false)]
		[TestCase ("System.Runtime.InteropServices", "System.Private.CoreLib", true)]
		[TestCase ("System.Private.CoreLib", "System.Runtime", true)]
		public void TypeMapAttributeRetainsOnlyClassMapping (string attributeAssembly, string typeAssembly, bool localAnchor)
		{
			string path = CreateAssembly (p => CreateTypeMapFixture (p,
				attributeAssembly: attributeAssembly, typeAssembly: typeAssembly, localAnchor: localAnchor));
			byte [] originalBytes = File.ReadAllBytes (path);
			R8Mapping mapping = Scan (path, """
				com.contoso.ProxyPeer -> a.b:
				    void callback() -> c
				    int value -> d

				""");

			CollectionAssert.AreEquivalent (new [] {
				"C\tcom/contoso/ProxyPeer",
			}, mapping.AccessedEntries);
			CollectionAssert.AreEqual (originalBytes, File.ReadAllBytes (path));
		}

		[TestCase ("System.Runtime.InteropServices", "System.Runtime", false, false)]
		[TestCase ("System.Private.CoreLib", "System.Private.CoreLib", false, false)]
		[TestCase ("System.Runtime.InteropServices", "System.Private.CoreLib", false, false)]
		[TestCase ("System.Private.CoreLib", "System.Runtime", true, false)]
		[TestCase ("System.Private.CoreLib", "System.Private.CoreLib", true, false)]
		[TestCase ("System.Runtime.InteropServices", "System.Runtime", false, true)]
		[TestCase ("System.Private.CoreLib", "System.Private.CoreLib", false, true)]
		[TestCase ("System.Runtime.InteropServices", "System.Private.CoreLib", false, true)]
		[TestCase ("System.Private.CoreLib", "System.Runtime", true, true)]
		[TestCase ("System.Private.CoreLib", "System.Private.CoreLib", true, true)]
		public void SurvivingClassOnlyTypeMapProducesBothDirections (string attributeAssembly, string typeAssembly, bool localAnchor, bool includeTargetType)
		{
			string path = CreateAssembly (p => CreateTypeMapFixture (p, "com/contoso/Marker",
				attributeAssembly: attributeAssembly, typeAssembly: typeAssembly, localAnchor: localAnchor, includeTargetType: includeTargetType));
			byte [] originalBytes = File.ReadAllBytes (path);
			string xml = Generate (path, """
				com.contoso.Marker -> a.b:
				    void callback() -> c
				    int value -> d
				com.contoso.Unused -> a.e:
				    void unused() -> f

				""");
			var root = XDocument.Parse (xml).Root ?? throw new AssertionException ("Generated XML has no root.");
			CollectionAssert.AreEquivalent (new [] { "replace-type", "reverse-type" },
				root.Elements ().Select (element => element.Name.LocalName));
			var forward = root.Element ("replace-type") ?? throw new AssertionException ("Missing forward type mapping.");
			Assert.AreEqual ("com/contoso/Marker", (string?) forward.Attribute ("from"));
			Assert.AreEqual ("a/b", (string?) forward.Attribute ("to"));
			var reverse = root.Element ("reverse-type") ?? throw new AssertionException ("Missing reverse type mapping.");
			Assert.AreEqual ("a/b", (string?) reverse.Attribute ("from"));
			Assert.AreEqual ("com/contoso/Marker", (string?) reverse.Attribute ("to"));
			CollectionAssert.AreEqual (originalBytes, File.ReadAllBytes (path));
		}

		[TestCase (false, false)]
		[TestCase (false, true)]
		[TestCase (true, false)]
		[TestCase (true, true)]
		public void TypeMapWithSurvivingMetadataRetainsOnlyReferencedMembers (bool localAnchor, bool includeTargetType)
		{
			string path = CreateAssembly (p => CreateTypeMapFixture (p, localAnchor: localAnchor,
				includeTargetType: includeTargetType, includeSurvivingMetadata: true));
			byte [] originalBytes = File.ReadAllBytes (path);
			string mappingText = """
				com.contoso.ProxyPeer -> a.b:
				    void onClick() -> c
				    void onClick(int) -> e
				    void unused() -> f
				    int value -> d
				    java.lang.String value -> g
				    int unusedValue -> h

				""";
			var mapping = Scan (path, mappingText);
			CollectionAssert.AreEquivalent (new [] {
				"C\tcom/contoso/ProxyPeer",
				"M\tcom/contoso/ProxyPeer\tonClick():void",
				"F\tcom/contoso/ProxyPeer\tvalue",
			}, mapping.AccessedEntries);
			var root = XDocument.Parse (Generate (path, mappingText)).Root
				?? throw new AssertionException ("Generated XML has no root.");
			Assert.AreEqual (1, root.Elements ("replace-type").Count ());
			Assert.AreEqual (1, root.Elements ("reverse-type").Count ());
			var method = root.Elements ("replace-method").Single ();
			Assert.AreEqual ("onClick", (string?) method.Attribute ("source-method-name"));
			Assert.AreEqual ("()V", (string?) method.Attribute ("source-method-signature"));
			Assert.AreEqual ("c", (string?) method.Attribute ("target-method-name"));
			CollectionAssert.AreEquivalent (new [] { ("value", "I", "d"), ("value", "Ljava/lang/String;", "g") },
				root.Elements ("replace-field").Select (field => (
					(string?) field.Attribute ("source-field-name"),
					(string?) field.Attribute ("source-field-signature"),
					(string?) field.Attribute ("target-field-name"))));
			CollectionAssert.AreEqual (originalBytes, File.ReadAllBytes (path));
		}

		[TestCase ("System.Runtime.InteropServices", "System.Runtime")]
		[TestCase ("System.Private.CoreLib", "System.Private.CoreLib")]
		public void UnrelatedTypeMapUniverseIsNotParsedAsJni (string attributeAssembly, string typeAssembly)
		{
			string path = CreateAssembly (p => CreateTypeMapFixture (p,
				"System.Collections.Generic.IDictionary`2[System.Boolean,System.Boolean]",
				attributeAssembly: attributeAssembly, typeAssembly: typeAssembly, groupName: "JavaDictionary"));
			var mapping = Scan (path, "com.contoso.ProxyPeer -> a.b:\n");
			CollectionAssert.IsEmpty (mapping.AccessedEntries);
		}

		[Test]
		public void UnrelatedMethodAttributesAreNotDecoded ()
		{
			string path = CreateAssembly (p => CreateFixture (p, unrelatedAttributes: true));
			var mapping = Scan (path, "com.contoso.Peer -> a.b:\n    void onClick() -> c\n");
			CollectionAssert.Contains (mapping.AccessedEntries, "M\tcom/contoso/Peer\tonClick():void");
		}

		[Test]
		public void NameOnlyFieldMetadataRetainsEveryDescriptorVariant ()
		{
			string path = CreateAssembly (p => CreateFixture (p));
			string xml = Generate (path, """
				com.contoso.Peer -> a.b:
				    int value -> c
				    java.lang.String value -> d

				""");
			StringAssert.Contains ("source-field-signature=\"I\"", xml);
			StringAssert.Contains ("source-field-signature=\"Ljava/lang/String;\"", xml);
			StringAssert.Contains ("target-field-name=\"c\"", xml);
			StringAssert.Contains ("target-field-name=\"d\"", xml);
		}

		[Test]
		[NUnit.Framework.Category ("ILLink")]
		public void RealLinkedTypeMapUsesImplementationIdentities ()
		{
			string? linker = TestContext.Parameters ["ILLinkPath"];
			string? frameworkDirectory = TestContext.Parameters ["ILLinkFrameworkDirectory"];
			if (string.IsNullOrEmpty (linker) || string.IsNullOrEmpty (frameworkDirectory)) {
				Assert.Ignore ("Supply ILLinkPath and ILLinkFrameworkDirectory to exercise actual linked metadata.");
				return;
			}
			string path = CreateAssembly (p => CreateTypeMapFixture (p, "com/contoso/Marker", localAnchor: true), "_Fixture.TypeMap.dll");
			string linkedDirectory = Path.Combine (Path.GetDirectoryName (path) ?? throw new AssertionException ("Fixture has no directory."), "linked");
			var (code, output, error) = RunProcessWithExitCode (
				Path.Combine (TestEnvironment.DotNetPreviewDirectory, TestEnvironment.IsWindows ? "dotnet.exe" : "dotnet"),
				$"\"{linker}\" -reference \"{path}\" -a _Fixture.TypeMap all -d \"{frameworkDirectory}\" " +
				$"--action copyused --action link _Fixture.TypeMap --ignore-link-attributes " +
				$"--skip-unresolved false -out \"{linkedDirectory}\"", timeoutInSeconds: 120);
			Assert.AreEqual (0, code, output + error);
			string linkedFile = Path.Combine (linkedDirectory, "_Fixture.TypeMap.dll");
			using (var linked = Cecil.AssemblyDefinition.ReadAssembly (linkedFile)) {
				var attribute = linked.CustomAttributes [0];
				Assert.AreEqual ("System.Private.CoreLib", attribute.AttributeType.GetElementType ().Scope.Name);
				Assert.AreEqual ("System.Private.CoreLib", attribute.Constructor.Parameters [1].ParameterType.Scope.Name);
			}
			string xml = Generate (linkedFile, "com.contoso.Marker -> a.b:\n");
			StringAssert.Contains ("""<replace-type from="com/contoso/Marker" to="a/b" />""", xml);
			StringAssert.Contains ("""<reverse-type from="a/b" to="com/contoso/Marker" />""", xml);
		}

		[TestCase ("com/contoso/ProxyPeer[+1]", true)]
		[TestCase ("com/contoso/ProxyPeer[ 1]", true)]
		[TestCase ("com/contoso/ProxyPeer[1]", false)]
		public void TypeMapAttributeValidatesAliasSuffix (string key, bool throws)
		{
			string path = CreateAssembly (p => CreateTypeMapFixture (p, key));
			void ScanFixture () => Scan (path, "com.contoso.ProxyPeer -> a.b:\n");

			if (throws) {
				Assert.Throws<BadImageFormatException> (ScanFixture);
			} else {
				Assert.DoesNotThrow (ScanFixture);
			}
		}

		[Test]
		public void TypeMapAttributeValidatesConstructorSignature ()
		{
			string path = CreateAssembly (p => CreateTypeMapFixture (p, "com/contoso/ProxyPeer", includeTypeArgument: false));
			Assert.Throws<BadImageFormatException> (() => Scan (path, "com.contoso.ProxyPeer -> a.b:\n"));
		}

		[TestCase (false)]
		[TestCase (true)]
		public void IgnoresUserDefinedTypeMapAttribute (bool localDefinition)
		{
			string path = CreateAssembly (p => CreateTypeMapFixture (p, attributeAssembly: "User", userDefinedAttribute: localDefinition));
			var mapping = Scan (path, "com.contoso.ProxyPeer -> a.b:\n");
			CollectionAssert.IsEmpty (mapping.AccessedEntries);
		}

		string CreateAssembly (Action<string> createFixture, string fileName = "Linked.dll")
		{
			string directory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (directory);
			string path = Path.Combine (directory, fileName);
			createFixture (path);
			return path;
		}

		R8Mapping Scan (string path, string mappingText)
		{
			var mapping = R8Mapping.Parse (new StringReader (mappingText));
			var task = new GenerateR8JniRemapping { BuildEngine = new MockBuildEngine (TestContext.Out) };
			using var stream = File.OpenRead (path);
			using var peReader = new PEReader (stream);
			JniRemappingAssemblyScanner.Scan (peReader.GetMetadataReader (), mapping, new TaskLoggingHelper (task));
			return mapping;
		}

		string Generate (string path, string mappingText)
		{
			string directory = Path.GetDirectoryName (path) ?? throw new AssertionException ("Fixture has no directory.");
			string mappingFile = Path.Combine (directory, "mapping.txt");
			File.WriteAllText (mappingFile, mappingText);
			string outputFile = Path.Combine (directory, "remap.xml");
			var task = new GenerateR8JniRemapping {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				MappingFile = mappingFile, OutputFile = outputFile, LinkedAssemblies = [new TaskItem (path)],
			};
			Assert.IsTrue (task.Execute ());
			return File.ReadAllText (outputFile);
		}

		static void CreateFixture (string path, bool unrelatedAttributes = false)
		{
			using var assembly = Cecil.AssemblyDefinition.CreateAssembly (
				new Cecil.AssemblyNameDefinition ("Linked", new System.Version (1, 0)),
				"Linked",
				Cecil.ModuleKind.Dll);
			Cecil.ModuleDefinition module = assembly.MainModule;
			AddRegisteredPeer (module, "com/contoso/Peer", unrelatedAttributes);
			assembly.Write (path);
		}

		static void AddRegisteredPeer (Cecil.ModuleDefinition module, string jniName, bool unrelatedAttributes = false)
		{
			Cecil.TypeReference attributeType = module.ImportReference (typeof (System.Attribute));

			Cecil.TypeDefinition registerAttribute = AddAttribute (module, attributeType, "Android.Runtime", "RegisterAttribute", 3);
			Cecil.MethodReference registerCtor1 = registerAttribute.Methods [0];
			Cecil.MethodReference registerCtor3 = registerAttribute.Methods [1];

			var peer = new Cecil.TypeDefinition ("Com.Contoso", "Peer", Cecil.TypeAttributes.Public | Cecil.TypeAttributes.Class, module.TypeSystem.Object);
			peer.CustomAttributes.Add (Attribute (registerCtor1, jniName));
			module.Types.Add (peer);

			var method = new Cecil.MethodDefinition ("OnClick", Cecil.MethodAttributes.Public, module.TypeSystem.Void);
			method.Body.Instructions.Add (Instruction.Create (OpCodes.Ret));
			method.CustomAttributes.Add (Attribute (registerCtor3, "onClick", "()V", "n_OnClick"));
			if (unrelatedAttributes) {
				var enumConstructor = typeof (EditorBrowsableAttribute).GetConstructor ([typeof (EditorBrowsableState)]);
				var typeConstructor = typeof (TypeConverterAttribute).GetConstructor ([typeof (Type)]);
				if (enumConstructor == null || typeConstructor == null) {
					throw new AssertionException ("Fixture attribute constructors were not found.");
				}
				var enumAttribute = new Cecil.CustomAttribute (module.ImportReference (enumConstructor));
				enumAttribute.ConstructorArguments.Add (new Cecil.CustomAttributeArgument (
					module.ImportReference (typeof (EditorBrowsableState)), (int) EditorBrowsableState.Never));
				method.CustomAttributes.Add (enumAttribute);
				var typeAttribute = new Cecil.CustomAttribute (module.ImportReference (typeConstructor));
				typeAttribute.ConstructorArguments.Add (new Cecil.CustomAttributeArgument (
					module.ImportReference (typeof (Type)), module.TypeSystem.Object));
				method.CustomAttributes.Add (typeAttribute);
			}
			peer.Methods.Add (method);

			var field = new Cecil.FieldDefinition ("Value", Cecil.FieldAttributes.Public, module.TypeSystem.Int32);
			field.CustomAttributes.Add (Attribute (registerCtor1, "value"));
			peer.Fields.Add (field);
		}

		static void CreateTypeMapFixture (string path, string key = "com/contoso/ProxyPeer[1]", bool includeTypeArgument = true,
			string attributeAssembly = "System.Runtime.InteropServices", string typeAssembly = "System.Runtime",
			bool localAnchor = false, string groupName = "Object", bool userDefinedAttribute = false,
			bool includeTargetType = false, bool includeSurvivingMetadata = false)
		{
			using var assembly = Cecil.AssemblyDefinition.CreateAssembly (
				new Cecil.AssemblyNameDefinition ("_Fixture.TypeMap", new System.Version (1, 0)),
				"TypeMap",
				Cecil.ModuleKind.Dll);
			Cecil.ModuleDefinition module = assembly.MainModule;
			var systemRuntime = new Cecil.AssemblyNameReference (typeAssembly, new Version (11, 0, 0, 0));
			var systemRuntimeInteropServices = new Cecil.AssemblyNameReference (attributeAssembly, new Version (11, 0, 0, 0));
			module.AssemblyReferences.Add (systemRuntime);
			module.AssemblyReferences.Add (systemRuntimeInteropServices);
			var systemObject = new Cecil.TypeReference ("System", "Object", module, systemRuntime);
			var systemType = new Cecil.TypeReference ("System", "Type", module, systemRuntime);
			var proxy = new Cecil.TypeDefinition ("Com.Contoso", "ProxyPeer", Cecil.TypeAttributes.Public | Cecil.TypeAttributes.Class, systemObject);
			module.Types.Add (proxy);
			var target = new Cecil.TypeDefinition ("Com.Contoso", "ManagedPeer", Cecil.TypeAttributes.Public | Cecil.TypeAttributes.Class, systemObject);
			module.Types.Add (target);
			if (includeSurvivingMetadata) {
				AddRegisteredPeer (module, "com/contoso/ProxyPeer");
			}
			Cecil.TypeReference attributeType;
			if (userDefinedAttribute) {
				var definition = AddAttribute (module, module.ImportReference (typeof (System.Attribute)),
					"System.Runtime.InteropServices", "TypeMapAttribute`1", 1);
				definition.GenericParameters.Add (new Cecil.GenericParameter ("T", definition));
				definition.Methods [0].Parameters.Add (new Cecil.ParameterDefinition (systemType));
				attributeType = definition;
			} else {
				attributeType = new Cecil.TypeReference (
					"System.Runtime.InteropServices", "TypeMapAttribute`1", module, systemRuntimeInteropServices);
			}
			var closedAttribute = new Cecil.GenericInstanceType (attributeType);
			Cecil.TypeReference group;
			if (localAnchor) {
				var anchor = new Cecil.TypeDefinition ("", "__TypeMapAnchor",
					Cecil.TypeAttributes.NotPublic | Cecil.TypeAttributes.Sealed | Cecil.TypeAttributes.Class, systemObject);
				module.Types.Add (anchor);
				group = anchor;
			} else {
				var monoAndroid = new Cecil.AssemblyNameReference ("Mono.Android", new Version (0, 0, 0, 0));
				module.AssemblyReferences.Add (monoAndroid);
				group = new Cecil.TypeReference (groupName == "Object" ? "Java.Lang" : "Android.Runtime", groupName, module, monoAndroid);
			}
			closedAttribute.GenericArguments.Add (group);
			var constructor = new Cecil.MethodReference (".ctor", module.TypeSystem.Void, closedAttribute) {
				HasThis = true,
			};
			constructor.Parameters.Add (new Cecil.ParameterDefinition (module.TypeSystem.String));
			if (includeTypeArgument) {
				constructor.Parameters.Add (new Cecil.ParameterDefinition (systemType));
				if (includeTargetType) {
					constructor.Parameters.Add (new Cecil.ParameterDefinition (systemType));
				}
			}
			var attribute = new Cecil.CustomAttribute (constructor);
			attribute.ConstructorArguments.Add (new Cecil.CustomAttributeArgument (module.TypeSystem.String, key));
			if (includeTypeArgument) {
				attribute.ConstructorArguments.Add (new Cecil.CustomAttributeArgument (
					systemType,
					proxy));
				if (includeTargetType) {
					attribute.ConstructorArguments.Add (new Cecil.CustomAttributeArgument (systemType, target));
				}
			}
			assembly.CustomAttributes.Add (attribute);
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
