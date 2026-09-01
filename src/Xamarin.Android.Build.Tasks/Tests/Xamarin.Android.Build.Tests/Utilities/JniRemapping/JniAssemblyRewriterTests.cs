using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks.JniRemapping;

namespace Xamarin.Android.Build.Tests
{
	/// <summary>
	/// End-to-end tests that build managed PE fixtures with System.Reflection.Metadata, rewrite
	/// their JNI-bearing attributes and strings, and verify the reconstructed assembly.
	/// </summary>
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class JniAssemblyRewriterTests : BaseTest
	{
		static JniRewriteResult Rewrite (byte [] sourceImage, R8Mapping mapping)
		{
			var log = new TaskLoggingHelper (new MockBuildEngine (TestContext.Out), nameof (JniAssemblyRewriterTests));
			return JniAssemblyRewriter.Rewrite (sourceImage, mapping, log);
		}

		static JniRewriteResult Rewrite (byte [] sourceImage, R8Mapping mapping, IList<BuildWarningEventArgs> warnings)
		{
			var engine = new MockBuildEngine (TestContext.Out, warnings: warnings);
			var log = new TaskLoggingHelper (engine, nameof (JniAssemblyRewriterTests));
			return JniAssemblyRewriter.Rewrite (sourceImage, mapping, log);
		}

		static R8Mapping Mapping (string text) => R8Mapping.Parse (new StringReader (text));

		static void AssertReverseScanMatchesRewrite (byte [] rewrittenImage, R8Mapping rewriteMapping, string mappingText)
		{
			R8Mapping scanMapping = Mapping (mappingText);
			var log = new TaskLoggingHelper (new MockBuildEngine (TestContext.Out), nameof (JniAssemblyRewriterTests));
			JniAssemblyRewriter.ScanRewrittenAssembly (rewrittenImage, scanMapping, log);
			CollectionAssert.AreEquivalent (
				rewriteMapping.AccessedEntries.ToArray (),
				scanMapping.AccessedEntries.ToArray (),
				"The reverse post-link scan should recognize every mapping consumed by forward rewriting.");
		}

		static IReadOnlyList<string> AttributeStringArgs (MetadataReader reader, CustomAttributeHandleCollection attributes, EntityHandle ctor)
		{
			foreach (CustomAttributeHandle handle in attributes) {
				CustomAttribute attribute = reader.GetCustomAttribute (handle);
				if (attribute.Constructor != ctor) {
					continue;
				}

				var decoded = attribute.DecodeValue (Xamarin.Android.Tasks.DummyCustomAttributeProvider.Instance);
				var result = new List<string> ();
				foreach (var argument in decoded.FixedArguments) {
					result.Add (argument.Value as string);
				}
				return result;
			}
			return [];
		}

		static string FirstAttributeStringArg (MetadataReader reader, CustomAttributeHandleCollection attributes, EntityHandle ctor)
		{
			var args = AttributeStringArgs (reader, attributes, ctor);
			return args.Count > 0 ? args [0] : null;
		}

		static List<KeyValuePair<int, string>> LoadedStrings (PEReader peReader, MetadataReader reader, MethodDefinitionHandle method)
		{
			var result = new List<KeyValuePair<int, string>> ();
			MethodDefinition definition = reader.GetMethodDefinition (method);
			if (definition.RelativeVirtualAddress == 0) {
				return result;
			}

			byte [] il = peReader.GetMethodBody (definition.RelativeVirtualAddress).GetILBytes ();
			int i = 0;
			while (i < il.Length) {
				if (il [i] == (byte) ILOpCode.Ldstr) {
					int token = il [i + 1] | (il [i + 2] << 8) | (il [i + 3] << 16) | (il [i + 4] << 24);
					result.Add (new KeyValuePair<int, string> (i + 1, reader.GetUserString (MetadataTokens.UserStringHandle (token & 0x00FFFFFF))));
					i += 5;
					continue;
				}
				i++;
			}
			return result;
		}

		static void AssertTableRowCountsMatch (MetadataReader expected, MetadataReader actual)
		{
			for (int i = 0; i < MetadataTokens.TableCount; i++) {
				var table = (TableIndex) i;
				Assert.AreEqual (expected.GetTableRowCount (table), actual.GetTableRowCount (table), $"Row count of table '{table}' changed.");
			}
		}

		static List<string> ValuesOf (List<KeyValuePair<int, string>> pairs)
		{
			var values = new List<string> (pairs.Count);
			foreach (var pair in pairs) {
				values.Add (pair.Value);
			}
			return values;
		}

		static MethodDefinitionHandle FirstMethodOf (MetadataReader reader, TypeDefinitionHandle type)
		{
			foreach (MethodDefinitionHandle handle in reader.GetTypeDefinition (type).GetMethods ()) {
				return handle;
			}
			return default;
		}

		static BlobBuilder IntFieldSignature ()
		{
			var signature = new BlobBuilder ();
			new BlobEncoder (signature).FieldSignature ().Int32 ();
			return signature;
		}

		static BlobHandle AddLegacyJniMethodSignature (JniFixtureBuilder fixture, bool findClass)
		{
			var signature = new BlobBuilder ();
			new BlobEncoder (signature).MethodSignature ()
				.Parameters (findClass ? 1 : 3, out ReturnTypeEncoder returnType, out ParametersEncoder parameters);
			returnType.Type ().Int32 ();
			if (findClass) {
				parameters.AddParameter ().Type ().String ();
			} else {
				parameters.AddParameter ().Type ().Int32 ();
				parameters.AddParameter ().Type ().String ();
				parameters.AddParameter ().Type ().String ();
			}
			return fixture.Metadata.GetOrAddBlob (signature);
		}

		[Test]
		public void DoesNotRewriteUnrelatedBareMemberAndDescriptorStrings ()
		{
			var fixture = new JniFixtureBuilder ();
			UserStringHandle className = fixture.String ("net/dot/android/ApplicationRegistration");
			UserStringHandle fieldName = fixture.String ("Context");
			UserStringHandle descriptor = fixture.String ("Landroid/content/Context;");

			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			MethodDefinitionHandle method = fixture.AddVoidMethod ("GetContext", fixture.EmitLoadStringBody (className, fieldName, descriptor));
			fixture.AddType ("Acme", "ContextAccessor", fieldStart, methodStart);

			JniRewriteResult result = Rewrite (fixture.Serialize (), Mapping (
				"net.dot.android.ApplicationRegistration -> c4:\n" +
				"    android.content.Context Context -> a\n"));

			using var peReader = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader reader = peReader.GetMetadataReader ();
			CollectionAssert.AreEqual (new [] {
				"c4",
				"Context",
				"Landroid/content/Context;",
			}, LoadedStrings (peReader, reader, method).ConvertAll (entry => entry.Value));
		}

		[Test]
		public void RewritesLegacyJniLookupsForTwoClassesAndBothMethodHandleKinds ()
		{
			var fixture = new JniFixtureBuilder ();
			BlobHandle findClassSignature = AddLegacyJniMethodSignature (fixture, findClass: true);
			BlobHandle memberLookupSignature = AddLegacyJniMethodSignature (fixture, findClass: false);

			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			MethodDefinitionHandle findClassDefinition = fixture.Metadata.AddMethodDefinition (
				MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
				MethodImplAttributes.Runtime,
				fixture.Metadata.GetOrAddString ("FindClass"),
				findClassSignature,
				0,
				MetadataTokens.ParameterHandle (fixture.Metadata.GetRowCount (TableIndex.Param) + 1));
			MethodDefinitionHandle getStaticFieldDefinition = fixture.Metadata.AddMethodDefinition (
				MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
				MethodImplAttributes.Runtime,
				fixture.Metadata.GetOrAddString ("GetStaticFieldID"),
				memberLookupSignature,
				0,
				MetadataTokens.ParameterHandle (fixture.Metadata.GetRowCount (TableIndex.Param) + 1));
			fixture.AddType ("Android.Runtime", "JNIEnv", fieldStart, methodStart);

			TypeReferenceHandle jniEnvironmentReference = fixture.Metadata.AddTypeReference (
				fixture.CoreLibraryReference,
				fixture.Metadata.GetOrAddString ("Android.Runtime"),
				fixture.Metadata.GetOrAddString ("JNIEnv"));
			MemberReferenceHandle findClassReference = fixture.Metadata.AddMemberReference (
				jniEnvironmentReference,
				fixture.Metadata.GetOrAddString ("FindClass"),
				findClassSignature);
			MemberReferenceHandle getMethodReference = fixture.Metadata.AddMemberReference (
				jniEnvironmentReference,
				fixture.Metadata.GetOrAddString ("GetMethodID"),
				memberLookupSignature);

			UserStringHandle firstClass = fixture.String ("acme/one/First");
			UserStringHandle firstField = fixture.String ("count");
			UserStringHandle firstDescriptor = fixture.String ("I");
			UserStringHandle firstOtherField = fixture.String ("enabled");
			UserStringHandle firstOtherDescriptor = fixture.String ("Z");
			UserStringHandle secondClass = fixture.String ("acme/two/Second");
			UserStringHandle secondMethod = fixture.String ("run");
			UserStringHandle secondDescriptor = fixture.String ("()V");
			UserStringHandle ambiguousField = fixture.String ("state");

			var localSignature = new BlobBuilder ();
			var localEncoder = new BlobEncoder (localSignature).LocalVariableSignature (2);
			localEncoder.AddVariable ().Type ().Int32 ();
			localEncoder.AddVariable ().Type ().Int32 ();
			StandaloneSignatureHandle locals = fixture.Metadata.AddStandaloneSignature (fixture.Metadata.GetOrAddBlob (localSignature));
			var controlFlow = new ControlFlowBuilder ();

			fieldStart = fixture.NextFieldRid;
			methodStart = fixture.NextMethodRid;
			MethodDefinitionHandle method = fixture.AddVoidMethod ("LookUpBoth", fixture.EmitBody (encoder => {
				LabelHandle ambiguousLookup = encoder.DefineLabel ();

				encoder.LoadString (firstClass);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (findClassDefinition);
				encoder.StoreLocal (0);
				encoder.LoadLocal (0);
				encoder.LoadString (firstField);
				encoder.LoadString (firstDescriptor);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (getStaticFieldDefinition);
				encoder.OpCode (ILOpCode.Pop);

				encoder.LoadLocal (0);
				encoder.LoadString (firstOtherField);
				encoder.LoadString (firstOtherDescriptor);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (getStaticFieldDefinition);
				encoder.OpCode (ILOpCode.Pop);

				encoder.LoadString (secondClass);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (findClassReference);
				encoder.StoreLocal (1);
				encoder.LoadLocal (1);
				encoder.LoadString (secondMethod);
				encoder.LoadString (secondDescriptor);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (getMethodReference);
				encoder.OpCode (ILOpCode.Pop);

				encoder.LoadString (firstClass);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (findClassDefinition);
				encoder.StoreLocal (0);
				encoder.Branch (ILOpCode.Br_s, ambiguousLookup);
				encoder.LoadString (secondClass);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (findClassReference);
				encoder.StoreLocal (0);
				encoder.MarkLabel (ambiguousLookup);
				encoder.LoadLocal (0);
				encoder.LoadString (ambiguousField);
				encoder.LoadString (firstDescriptor);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (getStaticFieldDefinition);
				encoder.OpCode (ILOpCode.Pop);
				encoder.OpCode (ILOpCode.Ret);
			}, locals, controlFlow));
			fixture.AddType ("Acme", "LegacyLookups", fieldStart, methodStart);

			var warnings = new List<BuildWarningEventArgs> ();
			JniRewriteResult result = Rewrite (fixture.Serialize (), Mapping (
				"acme.one.First -> a.b.F:\n" +
				"    int count -> x\n" +
				"    boolean enabled -> q\n" +
				"    int state -> f\n" +
				"acme.two.Second -> a.b.S:\n" +
				"    int state -> s\n" +
				"    void run() -> y\n"), warnings);

			using var peReader = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader reader = peReader.GetMetadataReader ();
			CollectionAssert.AreEqual (new [] {
				"a/b/F",
				"x",
				"I",
				"q",
				"Z",
				"a/b/S",
				"y",
				"()V",
				"a/b/F",
				"a/b/S",
				"state",
				"I",
			}, ValuesOf (LoadedStrings (peReader, reader, method)));
			Assert.AreEqual (1, warnings.Count, "The ambiguous local class source should produce one warning.");
			Assert.AreEqual ("XA4326", warnings [0].Code);
		}

		[Test]
		public void RewritesLegacyJniLookupsUsingAUniqueCachedStaticClassHandle ()
		{
			var fixture = new JniFixtureBuilder ();
			BlobHandle findClassSignature = AddLegacyJniMethodSignature (fixture, findClass: true);
			BlobHandle memberLookupSignature = AddLegacyJniMethodSignature (fixture, findClass: false);

			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			MethodDefinitionHandle findClass = fixture.Metadata.AddMethodDefinition (
				MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
				MethodImplAttributes.Runtime,
				fixture.Metadata.GetOrAddString ("FindClass"),
				findClassSignature,
				0,
				MetadataTokens.ParameterHandle (fixture.Metadata.GetRowCount (TableIndex.Param) + 1));
			MethodDefinitionHandle getField = fixture.Metadata.AddMethodDefinition (
				MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
				MethodImplAttributes.Runtime,
				fixture.Metadata.GetOrAddString ("GetFieldID"),
				memberLookupSignature,
				0,
				MetadataTokens.ParameterHandle (fixture.Metadata.GetRowCount (TableIndex.Param) + 1));
			fixture.AddType ("Android.Runtime", "JNIEnv", fieldStart, methodStart);

			TypeReferenceHandle jniEnvironmentReference = fixture.Metadata.AddTypeReference (
				fixture.CoreLibraryReference,
				fixture.Metadata.GetOrAddString ("Android.Runtime"),
				fixture.Metadata.GetOrAddString ("JNIEnv"));
			MemberReferenceHandle findClassReference = fixture.Metadata.AddMemberReference (
				jniEnvironmentReference,
				fixture.Metadata.GetOrAddString ("FindClass"),
				findClassSignature);
			MemberReferenceHandle getMethod = fixture.Metadata.AddMemberReference (
				jniEnvironmentReference,
				fixture.Metadata.GetOrAddString ("GetMethodID"),
				memberLookupSignature);

			fieldStart = fixture.NextFieldRid;
			FieldDefinitionHandle classRef = fixture.Metadata.AddFieldDefinition (
				FieldAttributes.Private | FieldAttributes.Static,
				fixture.Metadata.GetOrAddString ("class_ref"),
				fixture.Metadata.GetOrAddBlob (IntFieldSignature ()));
			FieldDefinitionHandle ambiguousClassRef = fixture.Metadata.AddFieldDefinition (
				FieldAttributes.Private | FieldAttributes.Static,
				fixture.Metadata.GetOrAddString ("ambiguous_class_ref"),
				fixture.Metadata.GetOrAddBlob (IntFieldSignature ()));
			FieldDefinitionHandle branchTargetClassRef = fixture.Metadata.AddFieldDefinition (
				FieldAttributes.Private | FieldAttributes.Static,
				fixture.Metadata.GetOrAddString ("branch_target_class_ref"),
				fixture.Metadata.GetOrAddBlob (IntFieldSignature ()));
			FieldDefinitionHandle frameworkClassRef = fixture.Metadata.AddFieldDefinition (
				FieldAttributes.Private | FieldAttributes.Static,
				fixture.Metadata.GetOrAddString ("framework_class_ref"),
				fixture.Metadata.GetOrAddBlob (IntFieldSignature ()));
			TypeReferenceHandle contentValuesReference = fixture.Metadata.AddTypeReference (
				fixture.CoreLibraryReference,
				fixture.Metadata.GetOrAddString ("Acme"),
				fixture.Metadata.GetOrAddString ("ContentValues"));
			MemberReferenceHandle classRefReference = fixture.Metadata.AddMemberReference (
				contentValuesReference,
				fixture.Metadata.GetOrAddString ("class_ref"),
				fixture.Metadata.GetOrAddBlob (IntFieldSignature ()));

			UserStringHandle contentValuesClass = fixture.String ("acme/orig/ContentValues");
			UserStringHandle otherClass = fixture.String ("acme/orig/Other");
			UserStringHandle fieldName = fixture.String ("size");
			UserStringHandle fieldDescriptor = fixture.String ("I");
			UserStringHandle methodName = fixture.String ("clear");
			UserStringHandle methodDescriptor = fixture.String ("()V");
			UserStringHandle ambiguousName = fixture.String ("value");
			UserStringHandle frameworkClass = fixture.String ("android/content/Context");
			UserStringHandle otherFrameworkClass = fixture.String ("android/view/View");

			methodStart = fixture.NextMethodRid;
			var initializerControlFlow = new ControlFlowBuilder ();
			MethodDefinitionHandle initializer = fixture.AddVoidMethod (".cctor", fixture.EmitBody (encoder => {
				LabelHandle branchTargetAssignment = encoder.DefineLabel ();

				encoder.LoadString (contentValuesClass);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (findClass);
				encoder.OpCode (ILOpCode.Stsfld);
				encoder.Token (classRef);

				encoder.LoadString (contentValuesClass);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (findClass);
				encoder.OpCode (ILOpCode.Stsfld);
				encoder.Token (ambiguousClassRef);
				encoder.LoadString (otherClass);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (findClassReference);
				encoder.OpCode (ILOpCode.Stsfld);
				encoder.Token (ambiguousClassRef);

				encoder.Branch (ILOpCode.Br_s, branchTargetAssignment);
				encoder.MarkLabel (branchTargetAssignment);
				encoder.LoadString (contentValuesClass);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (findClass);
				encoder.OpCode (ILOpCode.Stsfld);
				encoder.Token (branchTargetClassRef);

				encoder.LoadString (frameworkClass);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (findClass);
				encoder.OpCode (ILOpCode.Stsfld);
				encoder.Token (frameworkClassRef);
				encoder.LoadString (otherFrameworkClass);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (findClassReference);
				encoder.OpCode (ILOpCode.Stsfld);
				encoder.Token (frameworkClassRef);
				encoder.OpCode (ILOpCode.Ret);
			}, controlFlow: initializerControlFlow), MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
			MethodDefinitionHandle lookup = fixture.AddVoidMethod ("LookupMembers", fixture.EmitBody (encoder => {
				encoder.OpCode (ILOpCode.Ldsfld);
				encoder.Token (classRef);
				encoder.LoadString (fieldName);
				encoder.LoadString (fieldDescriptor);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (getField);
				encoder.OpCode (ILOpCode.Pop);

				encoder.OpCode (ILOpCode.Ldsfld);
				encoder.Token (classRefReference);
				encoder.LoadString (methodName);
				encoder.LoadString (methodDescriptor);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (getMethod);
				encoder.OpCode (ILOpCode.Pop);

				encoder.OpCode (ILOpCode.Ldsfld);
				encoder.Token (ambiguousClassRef);
				encoder.LoadString (ambiguousName);
				encoder.LoadString (fieldDescriptor);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (getField);
				encoder.OpCode (ILOpCode.Pop);

				encoder.OpCode (ILOpCode.Ldsfld);
				encoder.Token (branchTargetClassRef);
				encoder.LoadString (ambiguousName);
				encoder.LoadString (fieldDescriptor);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (getField);
				encoder.OpCode (ILOpCode.Pop);

				encoder.OpCode (ILOpCode.Ldsfld);
				encoder.Token (frameworkClassRef);
				encoder.LoadString (ambiguousName);
				encoder.LoadString (fieldDescriptor);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (getField);
				encoder.OpCode (ILOpCode.Pop);
				encoder.OpCode (ILOpCode.Ret);
			}));
			fixture.AddType ("Acme", "ContentValues", fieldStart, methodStart);

			var warnings = new List<BuildWarningEventArgs> ();
			JniRewriteResult result = Rewrite (fixture.Serialize (), Mapping (
				"acme.orig.ContentValues -> a.b.C:\n" +
				"    int size -> x\n" +
				"    void clear() -> y\n" +
				"    int value -> z\n" +
				"acme.orig.Other -> a.b.O:\n" +
				"    int value -> q\n"), warnings);

			using var peReader = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader reader = peReader.GetMetadataReader ();
			CollectionAssert.AreEqual (new [] {
				"a/b/C",
				"a/b/C",
				"a/b/O",
				"a/b/C",
				"android/content/Context",
				"android/view/View",
			}, ValuesOf (LoadedStrings (peReader, reader, initializer)));
			CollectionAssert.AreEqual (new [] {
				"x",
				"I",
				"y",
				"()V",
				"value",
				"I",
				"value",
				"I",
				"value",
				"I",
			}, ValuesOf (LoadedStrings (peReader, reader, lookup)));
			Assert.AreEqual (2, warnings.Count, "Each unsafe renamed cached class handle should produce one warning.");
			Assert.IsTrue (warnings.All (warning => warning.Code == "XA4326"));
		}

		[Test]
		public void IdentityMappedLoadedStringDoesNotRequireARewrite ()
		{
			var fixture = new JniFixtureBuilder ();
			UserStringHandle className = fixture.String ("acme/orig/Identity");
			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			fixture.AddVoidMethod ("LoadClass", fixture.EmitLoadStringBody (className));
			fixture.AddType ("Acme", "Identity", fieldStart, methodStart);
			byte [] image = fixture.Serialize ();

			JniRewriteResult result = Rewrite (image, Mapping ("acme.orig.Identity -> acme.orig.Identity:\n"));

			Assert.AreEqual (0, result.ReplacementCount);
			Assert.AreSame (image, result.Image, "An identity mapping should not invoke the assembly rebuilder.");
		}

		[Test]
		public void RewritesAttributesAndLoadedStrings ()
		{
			var fixture = new JniFixtureBuilder ();

			const string myViewJni = "acme/orig/MyView";
			const string callbackDescriptor = "(Lacme/orig/Callback;)V";
			const string rewrittenCallbackDescriptor = "(La/b/Cb;)V";
			const string registerNativesLine = "onClick:" + callbackDescriptor + ":n_OnClick_Lacme_orig_Callback_Handler";

			UserStringHandle methodId = fixture.String ("onClick." + callbackDescriptor);
			UserStringHandle fieldId = fixture.String ("someField.I");
			UserStringHandle exactClassName = fixture.String ("acme/orig/Marker");
			UserStringHandle singleLine = fixture.String (registerNativesLine);
			UserStringHandle multiline = fixture.String (registerNativesLine + "\nunused:()V:n_Unused");
			UserStringHandle trailingNewline = fixture.String (registerNativesLine + "\n");
			UserStringHandle unrelated = fixture.String ("this is an ordinary string, untouched");

			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;

			var someField = fixture.Metadata.AddFieldDefinition (FieldAttributes.Public,
				fixture.Metadata.GetOrAddString ("SomeField"), fixture.Metadata.GetOrAddBlob (IntFieldSignature ()));
			fixture.Metadata.AddCustomAttribute (someField, fixture.RegisterCtor1, fixture.AttributeBlob ("someField"));

			int onClickBody = fixture.EmitLoadStringBody (methodId, fieldId, exactClassName, singleLine, multiline, trailingNewline, unrelated);
			MethodDefinitionHandle onClick = fixture.AddVoidMethod ("OnClick", onClickBody);
			fixture.Metadata.AddCustomAttribute (onClick, fixture.RegisterCtor3,
				fixture.AttributeBlob ("onClick", callbackDescriptor, "n_OnClick_Lacme_orig_Callback_Handler"));
			fixture.Metadata.AddCustomAttribute (onClick, fixture.JniMethodSignatureCtor2,
				fixture.AttributeBlob ("onClick", callbackDescriptor));

			MethodDefinitionHandle ctor = fixture.AddVoidMethod (".ctor", fixture.EmitReturnOnlyBody (),
				MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
			fixture.Metadata.AddCustomAttribute (ctor, fixture.RegisterCtor3,
				fixture.AttributeBlob (".ctor", callbackDescriptor, "n_ctor_Lacme_orig_Callback_Handler"));
			fixture.Metadata.AddCustomAttribute (ctor, fixture.JniConstructorSignatureCtor1, fixture.AttributeBlob (callbackDescriptor));

			TypeDefinitionHandle myView = fixture.AddType ("Acme.Orig", "MyView", fieldStart, methodStart);
			fixture.Metadata.AddCustomAttribute (myView, fixture.RegisterCtor1, fixture.AttributeBlob (myViewJni));

			fieldStart = fixture.NextFieldRid;
			methodStart = fixture.NextMethodRid;
			TypeDefinitionHandle marker = fixture.AddType ("Acme.Orig", "Marker", fieldStart, methodStart);
			fixture.Metadata.AddCustomAttribute (marker, fixture.JniTypeSignatureCtor1, fixture.AttributeBlob ("acme/orig/Marker"));

			UserStringHandle nestedRun = fixture.String ("run:()V:n_Run");
			fieldStart = fixture.NextFieldRid;
			methodStart = fixture.NextMethodRid;
			fixture.AddVoidMethod ("Run", fixture.EmitLoadStringBody (nestedRun));
			TypeDefinitionHandle nested = fixture.AddType (null, "Nested", fieldStart, methodStart,
				TypeAttributes.NestedPublic | TypeAttributes.Class | TypeAttributes.BeforeFieldInit);
			fixture.Metadata.AddNestedType (nested, myView);

			const string mappingText =
				"acme.orig.MyView -> a.b.C:\n" +
				"    void onClick(acme.orig.Callback) -> a\n" +
				"    int someField -> x\n" +
				"    void run() -> b\n" +
				"    void <init>(acme.orig.Callback) -> <init>\n" +
				"acme.orig.Callback -> a.b.Cb:\n" +
				"acme.orig.Marker -> a.b.D:\n";
			R8Mapping mapping = Mapping (mappingText);
			JniRewriteResult result = Rewrite (fixture.Serialize (), mapping);
			AssertReverseScanMatchesRewrite (result.Image, mapping, mappingText);

			using var peReader = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader reader = peReader.GetMetadataReader ();

			Assert.AreEqual ("a/b/C", FirstAttributeStringArg (reader, reader.GetTypeDefinition (myView).GetCustomAttributes (), fixture.RegisterCtor1));
			Assert.AreEqual ("a/b/D", FirstAttributeStringArg (reader, reader.GetTypeDefinition (marker).GetCustomAttributes (), fixture.JniTypeSignatureCtor1));
			Assert.AreEqual ("x", FirstAttributeStringArg (reader, reader.GetFieldDefinition (someField).GetCustomAttributes (), fixture.RegisterCtor1));

			CustomAttributeHandleCollection onClickAttributes = reader.GetMethodDefinition (onClick).GetCustomAttributes ();
			CollectionAssert.AreEqual (new [] { "a", rewrittenCallbackDescriptor, "n_OnClick_Lacme_orig_Callback_Handler" },
				AttributeStringArgs (reader, onClickAttributes, fixture.RegisterCtor3));
			CollectionAssert.AreEqual (new [] { "a", rewrittenCallbackDescriptor },
				AttributeStringArgs (reader, onClickAttributes, fixture.JniMethodSignatureCtor2));
			CollectionAssert.AreEqual (new [] { rewrittenCallbackDescriptor },
				AttributeStringArgs (reader, reader.GetMethodDefinition (ctor).GetCustomAttributes (), fixture.JniConstructorSignatureCtor1));
			CollectionAssert.AreEqual (new [] { ".ctor", rewrittenCallbackDescriptor, "n_ctor_Lacme_orig_Callback_Handler" },
				AttributeStringArgs (reader, reader.GetMethodDefinition (ctor).GetCustomAttributes (), fixture.RegisterCtor3));

			CollectionAssert.AreEqual (new [] {
				"a." + rewrittenCallbackDescriptor,
				"x.I",
				"a/b/D",
				"a:" + rewrittenCallbackDescriptor + ":n_OnClick_Lacme_orig_Callback_Handler",
				"a:" + rewrittenCallbackDescriptor + ":n_OnClick_Lacme_orig_Callback_Handler\nunused:()V:n_Unused",
				"a:" + rewrittenCallbackDescriptor + ":n_OnClick_Lacme_orig_Callback_Handler\n",
				"this is an ordinary string, untouched",
			}, ValuesOf (LoadedStrings (peReader, reader, onClick)));

			MethodDefinitionHandle run = FirstMethodOf (reader, nested);
			CollectionAssert.AreEqual (new [] { "b:()V:n_Run" }, ValuesOf (LoadedStrings (peReader, reader, run)));
		}

		[Test]
		public void SharedLoadedStringGetsOwnerSpecificReplacements ()
		{
			var fixture = new JniFixtureBuilder ();
			UserStringHandle shared = fixture.String ("go.()V");

			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			fixture.AddVoidMethod ("Go", fixture.EmitLoadStringBody (shared));
			TypeDefinitionHandle first = fixture.AddType ("Acme.Orig", "Dup1", fieldStart, methodStart);
			fixture.Metadata.AddCustomAttribute (first, fixture.JniTypeSignatureCtor1, fixture.AttributeBlob ("acme/orig/Dup1"));

			fieldStart = fixture.NextFieldRid;
			methodStart = fixture.NextMethodRid;
			fixture.AddVoidMethod ("Go", fixture.EmitLoadStringBody (shared));
			TypeDefinitionHandle second = fixture.AddType ("Acme.Orig", "Dup2", fieldStart, methodStart);
			fixture.Metadata.AddCustomAttribute (second, fixture.JniTypeSignatureCtor1, fixture.AttributeBlob ("acme/orig/Dup2"));

			JniRewriteResult result = Rewrite (fixture.Serialize (), Mapping (
				"acme.orig.Dup1 -> a.b.F1:\n" +
				"    void go() -> z\n" +
				"acme.orig.Dup2 -> a.b.F2:\n" +
				"    void go() -> q\n"));

			using var peReader = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader reader = peReader.GetMetadataReader ();

			CollectionAssert.AreEqual (new [] { "z.()V" }, ValuesOf (LoadedStrings (peReader, reader, FirstMethodOf (reader, first))));
			CollectionAssert.AreEqual (new [] { "q.()V" }, ValuesOf (LoadedStrings (peReader, reader, FirstMethodOf (reader, second))));
		}

		[Test]
		public void AppliesReplacementsLongerThanTheOriginal ()
		{
			var fixture = new JniFixtureBuilder ();

			UserStringHandle memberId = fixture.String ("go.()V");
			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			MethodDefinitionHandle go = fixture.AddVoidMethod ("Go", fixture.EmitLoadStringBody (memberId));
			fixture.Metadata.AddCustomAttribute (go, fixture.RegisterCtor3, fixture.AttributeBlob ("go", "()V", "n_Go"));
			TypeDefinitionHandle small = fixture.AddType ("Acme.Orig", "Small", fieldStart, methodStart);
			fixture.Metadata.AddCustomAttribute (small, fixture.JniTypeSignatureCtor1, fixture.AttributeBlob ("acme/orig/Small"));

			const string longName = "aVeryLongReplacementMethodNameThatCouldNeverFitInPlace";
			JniRewriteResult result = Rewrite (fixture.Serialize (), Mapping (
				"acme.orig.Small -> com.example.a.VeryLongObfuscatedClassName:\n" +
				"    void go() -> " + longName + "\n"));

			using var peReader = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader reader = peReader.GetMetadataReader ();

			Assert.AreEqual ("com/example/a/VeryLongObfuscatedClassName",
				FirstAttributeStringArg (reader, reader.GetTypeDefinition (small).GetCustomAttributes (), fixture.JniTypeSignatureCtor1));
			CollectionAssert.AreEqual (new [] { longName, "()V", "n_Go" },
				AttributeStringArgs (reader, reader.GetMethodDefinition (go).GetCustomAttributes (), fixture.RegisterCtor3));
			CollectionAssert.AreEqual (new [] { longName + ".()V" }, ValuesOf (LoadedStrings (peReader, reader, go)));
		}

		[Test]
		public void PreservesResourcesExceptionRegionsAndComplexIL ()
		{
			var fixture = new JniFixtureBuilder ();

			byte [] resource1 = new byte [] { 1, 2, 3, 4, 5, 6, 7 };
			var resource2 = new byte [300];
			for (int i = 0; i < resource2.Length; i++) {
				resource2 [i] = (byte) (i * 7);
			}
			fixture.AddEmbeddedResource ("First.resources", resource1);
			fixture.AddEmbeddedResource ("Second.resources", resource2);

			var localSignature = new BlobBuilder ();
			var localEncoder = new BlobEncoder (localSignature).LocalVariableSignature (2);
			localEncoder.AddVariable ().Type ().Int32 ();
			localEncoder.AddVariable ().Type ().Object ();
			StandaloneSignatureHandle locals = fixture.Metadata.AddStandaloneSignature (fixture.Metadata.GetOrAddBlob (localSignature));

			UserStringHandle jniString = fixture.String ("go.()V");
			var controlFlow = new ControlFlowBuilder ();

			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			int bodyOffset = fixture.EmitBody (encoder => {
				LabelHandle tryStart = encoder.DefineLabel ();
				LabelHandle catchStart = encoder.DefineLabel ();
				LabelHandle catchEnd = encoder.DefineLabel ();
				LabelHandle protectedStart = encoder.DefineLabel ();
				LabelHandle finallyStart = encoder.DefineLabel ();
				LabelHandle finallyEnd = encoder.DefineLabel ();
				LabelHandle afterCatch = encoder.DefineLabel ();
				LabelHandle afterFinally = encoder.DefineLabel ();
				LabelHandle case0 = encoder.DefineLabel ();
				LabelHandle case1 = encoder.DefineLabel ();
				LabelHandle case2 = encoder.DefineLabel ();
				LabelHandle done = encoder.DefineLabel ();

				encoder.MarkLabel (tryStart);
				encoder.LoadString (jniString);
				encoder.OpCode (ILOpCode.Pop);
				encoder.Branch (ILOpCode.Leave, afterCatch);

				encoder.MarkLabel (catchStart);
				encoder.StoreLocal (1);
				encoder.Branch (ILOpCode.Leave, afterCatch);
				encoder.MarkLabel (catchEnd);

				encoder.MarkLabel (afterCatch);
				encoder.MarkLabel (protectedStart);
				encoder.OpCode (ILOpCode.Nop);
				encoder.Branch (ILOpCode.Leave, afterFinally);

				encoder.MarkLabel (finallyStart);
				encoder.OpCode (ILOpCode.Endfinally);
				encoder.MarkLabel (finallyEnd);

				encoder.MarkLabel (afterFinally);
				encoder.LoadConstantI4 (1);
				encoder.StoreLocal (0);
				encoder.LoadLocal (0);
				SwitchInstructionEncoder switchEncoder = encoder.Switch (3);
				switchEncoder.Branch (case0);
				switchEncoder.Branch (case1);
				switchEncoder.Branch (case2);

				encoder.MarkLabel (case0);
				encoder.LoadConstantI4 (0);
				encoder.LoadConstantI4 (1);
				encoder.OpCode (ILOpCode.Ceq);
				encoder.OpCode (ILOpCode.Pop);
				encoder.Branch (ILOpCode.Br_s, done);

				encoder.MarkLabel (case1);
				encoder.OpCode (ILOpCode.Sizeof);
				encoder.Token (fixture.ExceptionReference);
				encoder.OpCode (ILOpCode.Pop);
				encoder.Branch (ILOpCode.Br, done);

				encoder.MarkLabel (case2);
				encoder.OpCode (ILOpCode.Nop);

				encoder.MarkLabel (done);
				encoder.OpCode (ILOpCode.Ret);

				controlFlow.AddCatchRegion (tryStart, catchStart, catchStart, catchEnd, fixture.ExceptionReference);
				controlFlow.AddFinallyRegion (protectedStart, finallyStart, finallyStart, finallyEnd);
			}, locals, controlFlow);

			MethodDefinitionHandle method = fixture.AddVoidMethod ("Go", bodyOffset);
			TypeDefinitionHandle type = fixture.AddType ("Acme.Orig", "Complex", fieldStart, methodStart);
			fixture.Metadata.AddCustomAttribute (type, fixture.JniTypeSignatureCtor1, fixture.AttributeBlob ("acme/orig/Complex"));

			byte [] source = fixture.Serialize ();
			JniRewriteResult result = Rewrite (source, Mapping (
				"acme.orig.Complex -> a.b.X:\n" +
				"    void go() -> z\n"));

			using var sourcePe = new PEReader (ImmutableArray.Create (source));
			using var rewrittenPe = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader before = sourcePe.GetMetadataReader ();
			MetadataReader after = rewrittenPe.GetMetadataReader ();

			MethodBodyBlock sourceBody = sourcePe.GetMethodBody (before.GetMethodDefinition (method).RelativeVirtualAddress);
			MethodBodyBlock rewrittenBody = rewrittenPe.GetMethodBody (after.GetMethodDefinition (method).RelativeVirtualAddress);

			Assert.AreEqual (sourceBody.GetILBytes ().Length, rewrittenBody.GetILBytes ().Length, "IL length must not change.");
			Assert.AreEqual (sourceBody.MaxStack, rewrittenBody.MaxStack);
			Assert.AreEqual (sourceBody.LocalVariablesInitialized, rewrittenBody.LocalVariablesInitialized);
			Assert.AreEqual (sourceBody.LocalSignature, rewrittenBody.LocalSignature);
			Assert.AreEqual (sourceBody.ExceptionRegions.Length, rewrittenBody.ExceptionRegions.Length);
			for (int i = 0; i < sourceBody.ExceptionRegions.Length; i++) {
				ExceptionRegion expected = sourceBody.ExceptionRegions [i];
				ExceptionRegion actual = rewrittenBody.ExceptionRegions [i];
				Assert.AreEqual (expected.Kind, actual.Kind);
				Assert.AreEqual (expected.TryOffset, actual.TryOffset);
				Assert.AreEqual (expected.TryLength, actual.TryLength);
				Assert.AreEqual (expected.HandlerOffset, actual.HandlerOffset);
				Assert.AreEqual (expected.HandlerLength, actual.HandlerLength);
				Assert.AreEqual (expected.CatchType, actual.CatchType);
			}

			byte [] sourceIL = sourceBody.GetILBytes ();
			byte [] rewrittenIL = rewrittenBody.GetILBytes ();
			var stringOperands = new HashSet<int> ();
			foreach (var pair in LoadedStrings (sourcePe, before, method)) {
				for (int i = 0; i < 4; i++) {
					stringOperands.Add (pair.Key + i);
				}
			}
			for (int i = 0; i < sourceIL.Length; i++) {
				if (!stringOperands.Contains (i)) {
					Assert.AreEqual (sourceIL [i], rewrittenIL [i], $"IL byte {i} changed.");
				}
			}

			CollectionAssert.AreEqual (new [] { "z.()V" }, ValuesOf (LoadedStrings (rewrittenPe, after, method)));
			CollectionAssert.AreEqual (resource1, ReadResource (rewrittenPe, after, "First.resources"));
			CollectionAssert.AreEqual (resource2, ReadResource (rewrittenPe, after, "Second.resources"));
			AssertTableRowCountsMatch (before, after);
		}

		static byte [] ReadResource (PEReader peReader, MetadataReader reader, string name)
		{
			foreach (ManifestResourceHandle handle in reader.ManifestResources) {
				ManifestResource resource = reader.GetManifestResource (handle);
				if (reader.GetString (resource.Name) != name) {
					continue;
				}

				DirectoryEntry directory = peReader.PEHeaders.CorHeader.ResourcesDirectory;
				PEMemoryBlock block = peReader.GetSectionData (directory.RelativeVirtualAddress);
				int offset = (int) resource.Offset;
				int size = block.GetReader (offset, sizeof (int)).ReadInt32 ();
				return block.GetReader (offset + sizeof (int), size).ReadBytes (size);
			}

			Assert.Fail ($"Resource '{name}' is missing.");
			return null;
		}

		[Test]
		public void PreservesTokensIdentityAndDebugDirectory ()
		{
			var fixture = new JniFixtureBuilder ();

			var pdbId = new BlobContentId (new Guid ("2A3B4C5D-6E7F-4011-9223-334455667788"), 0xAABBCCDD);
			var debugDirectory = new DebugDirectoryBuilder ();
			debugDirectory.AddCodeViewEntry ("/some/where/Fixture.pdb", pdbId, portablePdbVersion: 0x0100);
			debugDirectory.AddReproducibleEntry ();
			fixture.DebugDirectory = debugDirectory;

			UserStringHandle jniString = fixture.String ("go.()V");
			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			MethodDefinitionHandle go = fixture.AddVoidMethod ("Go", fixture.EmitLoadStringBody (jniString));
			TypeDefinitionHandle type = fixture.AddType ("Acme.Orig", "Identity", fieldStart, methodStart);
			fixture.Metadata.AddCustomAttribute (type, fixture.JniTypeSignatureCtor1, fixture.AttributeBlob ("acme/orig/Identity"));

			byte [] source = fixture.Serialize ();
			JniRewriteResult result = Rewrite (source, Mapping (
				"acme.orig.Identity -> a.b.I:\n" +
				"    void go() -> z\n"));

			using var sourcePe = new PEReader (ImmutableArray.Create (source));
			using var rewrittenPe = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader before = sourcePe.GetMetadataReader ();
			MetadataReader after = rewrittenPe.GetMetadataReader ();

			Assert.AreEqual (before.GetGuid (before.GetModuleDefinition ().Mvid), after.GetGuid (after.GetModuleDefinition ().Mvid));
			Assert.AreEqual (before.MetadataVersion, after.MetadataVersion);
			Assert.AreEqual (sourcePe.PEHeaders.CoffHeader.TimeDateStamp, rewrittenPe.PEHeaders.CoffHeader.TimeDateStamp);
			Assert.AreEqual (sourcePe.PEHeaders.CorHeader.Flags, rewrittenPe.PEHeaders.CorHeader.Flags);

			var sourceEntries = sourcePe.ReadDebugDirectory ();
			var rewrittenEntries = rewrittenPe.ReadDebugDirectory ();
			Assert.AreEqual (sourceEntries.Length, rewrittenEntries.Length);
			for (int i = 0; i < sourceEntries.Length; i++) {
				Assert.AreEqual (sourceEntries [i].Type, rewrittenEntries [i].Type);
				Assert.AreEqual (sourceEntries [i].Stamp, rewrittenEntries [i].Stamp);
				Assert.AreEqual (sourceEntries [i].MajorVersion, rewrittenEntries [i].MajorVersion);
				Assert.AreEqual (sourceEntries [i].MinorVersion, rewrittenEntries [i].MinorVersion);
			}

			CodeViewDebugDirectoryData codeView = rewrittenPe.ReadCodeViewDebugDirectoryData (rewrittenEntries [0]);
			Assert.AreEqual (pdbId.Guid, codeView.Guid);
			Assert.AreEqual ("/some/where/Fixture.pdb", codeView.Path);
			Assert.AreEqual (1, codeView.Age);

			AssertTableRowCountsMatch (before, after);

			var sourceStrings = LoadedStrings (sourcePe, before, go);
			var rewrittenStrings = LoadedStrings (rewrittenPe, after, go);
			Assert.AreEqual (sourceStrings [0].Key, rewrittenStrings [0].Key, "The ldstr operand moved.");
			Assert.AreEqual ("z.()V", rewrittenStrings [0].Value);
		}

		[Test]
		public void ClearsTheStrongNameSignedFlagButReservesTheSignatureDirectory ()
		{
			const int OriginalStrongNameSignatureSize = 128;

			var fixture = new JniFixtureBuilder (hasPublicKey: true) {
				Flags = CorFlags.ILOnly | CorFlags.StrongNameSigned,
				StrongNameSignatureSize = OriginalStrongNameSignatureSize,
			};

			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			fixture.AddVoidMethod ("Go", fixture.EmitReturnOnlyBody ());
			TypeDefinitionHandle type = fixture.AddType ("Acme.Orig", "Signed", fieldStart, methodStart);
			fixture.Metadata.AddCustomAttribute (type, fixture.JniTypeSignatureCtor1, fixture.AttributeBlob ("acme/orig/Signed"));

			byte [] source = fixture.Serialize ();
			JniRewriteResult result = Rewrite (source, Mapping ("acme.orig.Signed -> a.b.S:\n"));
			Assert.IsTrue (result.StrongNameSignatureCleared);

			using var rewrittenPe = new PEReader (ImmutableArray.Create (result.Image));
			CorHeader corHeader = rewrittenPe.PEHeaders.CorHeader;
			Assert.AreEqual (CorFlags.ILOnly, corHeader.Flags);

			using var sourcePe = new PEReader (ImmutableArray.Create (source));
			MetadataReader sourceMetadata = sourcePe.GetMetadataReader ();
			MetadataReader rewrittenMetadata = rewrittenPe.GetMetadataReader ();
			AssemblyDefinition sourceAssembly = sourceMetadata.GetAssemblyDefinition ();
			AssemblyDefinition rewrittenAssembly = rewrittenMetadata.GetAssemblyDefinition ();
			Assert.AreEqual (sourceAssembly.Flags, rewrittenAssembly.Flags);
			CollectionAssert.AreEqual (
				sourceMetadata.GetBlobBytes (sourceAssembly.PublicKey),
				rewrittenMetadata.GetBlobBytes (rewrittenAssembly.PublicKey));

			Assert.AreEqual (OriginalStrongNameSignatureSize, corHeader.StrongNameSignatureDirectory.Size);
			Assert.AreNotEqual (0, corHeader.StrongNameSignatureDirectory.RelativeVirtualAddress);
		}

		[Test]
		public void RewrittenAssemblyStillMatchesItsPortablePdb ()
		{
			var fixture = new JniFixtureBuilder ();

			UserStringHandle jniString = fixture.String ("go.()V");
			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			MethodDefinitionHandle go = fixture.AddVoidMethod ("Go", fixture.EmitLoadStringBody (jniString));
			TypeDefinitionHandle type = fixture.AddType ("Acme.Orig", "Debuggable", fieldStart, methodStart);
			fixture.Metadata.AddCustomAttribute (type, fixture.JniTypeSignatureCtor1, fixture.AttributeBlob ("acme/orig/Debuggable"));

			string directory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (directory);
			string assemblyPath = Path.Combine (directory, "Fixture.dll");
			string pdbPath = Path.Combine (directory, "Fixture.pdb");

			var pdbMetadata = new MetadataBuilder ();
			DocumentHandle document = pdbMetadata.AddDocument (
				pdbMetadata.GetOrAddDocumentName ("/src/Fixture.cs"), default, default, default);
			var sequencePoints = new BlobBuilder ();
			sequencePoints.WriteCompressedInteger (0);
			sequencePoints.WriteCompressedInteger (0);
			sequencePoints.WriteCompressedInteger (1);
			sequencePoints.WriteCompressedInteger (10);
			sequencePoints.WriteCompressedInteger (5);
			sequencePoints.WriteCompressedInteger (1);
			pdbMetadata.SetCapacity (TableIndex.MethodDebugInformation, MetadataTokens.GetRowNumber (go));
			for (int rid = 1; rid < MetadataTokens.GetRowNumber (go); rid++) {
				pdbMetadata.AddMethodDebugInformation (default, default);
			}
			pdbMetadata.AddMethodDebugInformation (document, pdbMetadata.GetOrAddBlob (sequencePoints));

			var pdbBuilder = new PortablePdbBuilder (pdbMetadata, fixture.Metadata.GetRowCounts (), default);
			var pdbBlob = new BlobBuilder ();
			BlobContentId pdbId = pdbBuilder.Serialize (pdbBlob);

			var debugDirectory = new DebugDirectoryBuilder ();
			debugDirectory.AddCodeViewEntry (pdbPath, pdbId, pdbBuilder.FormatVersion);
			fixture.DebugDirectory = debugDirectory;

			using (var pdbStream = File.Create (pdbPath)) {
				pdbBlob.WriteContentTo (pdbStream);
			}

			JniRewriteResult result = Rewrite (fixture.Serialize (), Mapping (
				"acme.orig.Debuggable -> a.b.D:\n" +
				"    void go() -> z\n"));
			File.WriteAllBytes (assemblyPath, result.Image);

			using var peReader = new PEReader (File.OpenRead (assemblyPath));
			Assert.IsTrue (peReader.TryOpenAssociatedPortablePdb (assemblyPath, File.OpenRead, out MetadataReaderProvider provider, out string _));

			using (provider) {
				MetadataReader pdbReader = provider.GetMetadataReader ();
				MetadataReader reader = peReader.GetMetadataReader ();
				var points = new List<SequencePoint> (pdbReader.GetMethodDebugInformation (go).GetSequencePoints ());
				Assert.AreEqual (1, points.Count);
				Assert.AreEqual (0, points [0].Offset);

				byte [] il = peReader.GetMethodBody (reader.GetMethodDefinition (go).RelativeVirtualAddress).GetILBytes ();
				Assert.AreEqual ((byte) ILOpCode.Ldstr, il [0]);
				CollectionAssert.AreEqual (new [] { "z.()V" }, ValuesOf (LoadedStrings (peReader, reader, go)));
			}
		}

		[Test]
		public void RewrittenAssemblyLoadsAndRunsInTheRuntime ()
		{
			var fixture = new JniFixtureBuilder ();
			var signature = new BlobBuilder ();
			new BlobEncoder (signature).MethodSignature ()
				.Parameters (0, out ReturnTypeEncoder returnType, out ParametersEncoder _);
			returnType.Type ().Int32 ();

			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			fixture.Metadata.AddMethodDefinition (
				MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
				MethodImplAttributes.IL,
				fixture.Metadata.GetOrAddString ("Answer"),
				fixture.Metadata.GetOrAddBlob (signature),
				fixture.EmitBody (encoder => {
					encoder.LoadConstantI4 (42);
					encoder.OpCode (ILOpCode.Ret);
				}),
				MetadataTokens.ParameterHandle (fixture.Metadata.GetRowCount (TableIndex.Param) + 1));
			TypeReferenceHandle objectType = fixture.Metadata.AddTypeReference (
				fixture.CoreLibraryReference, fixture.Metadata.GetOrAddString ("System"), fixture.Metadata.GetOrAddString ("Object"));
			TypeDefinitionHandle type = fixture.AddType ("Acme.Orig", "Loadable", fieldStart, methodStart, baseType: objectType);
			fixture.Metadata.AddCustomAttribute (type, fixture.JniTypeSignatureCtor1, fixture.AttributeBlob ("acme/orig/Loadable"));

			JniRewriteResult result = Rewrite (fixture.Serialize (), Mapping ("acme.orig.Loadable -> a.b.L:\n"));
			Assert.Greater (result.ReplacementCount, 0);

			var context = new System.Runtime.Loader.AssemblyLoadContext (TestName, isCollectible: true);
			try {
				using var stream = new MemoryStream (result.Image, writable: false);
				Assembly assembly = context.LoadFromStream (stream);
				Type loadedType = assembly.GetType ("Acme.Orig.Loadable", throwOnError: true);
				MethodInfo answer = loadedType.GetMethod ("Answer", BindingFlags.Public | BindingFlags.Static);
				Assert.AreEqual (42, answer.Invoke (null, null));
			} finally {
				context.Unload ();
			}
		}
	}
}
