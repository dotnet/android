using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks.JniRemapping;

namespace Xamarin.Android.Build.Tests
{
	/// <summary>
	/// End-to-end tests that build small PE fixtures purely with System.Reflection.Metadata /
	/// Ecma335 (no Mono.Cecil), run the two-pass JNI rewriter against them, and then read the
	/// rebuilt image back with a fresh PEReader/MetadataReader to verify both that the JNI names
	/// were rewritten and that everything else survived the reconstruction unchanged.
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

		static R8Mapping Mapping (string text) => R8Mapping.Parse (new StringReader (text));

		static IReadOnlyList<string> AttributeStringArgs (MetadataReader reader, CustomAttributeHandleCollection attributes, MethodDefinitionHandle ctor)
		{
			foreach (CustomAttributeHandle handle in attributes) {
				CustomAttribute attribute = reader.GetCustomAttribute (handle);
				if (attribute.Constructor.Kind != HandleKind.MethodDefinition || (MethodDefinitionHandle) attribute.Constructor != ctor) {
					continue;
				}

				var decoded = attribute.DecodeValue (Xamarin.Android.Tasks.DummyCustomAttributeProvider.Instance);
				var result = new List<string> ();
				foreach (var argument in decoded.FixedArguments) {
					result.Add (argument.Value as string);
				}
				return result;
			}
			return Array.Empty<string> ();
		}

		static string FirstAttributeStringArg (MetadataReader reader, CustomAttributeHandleCollection attributes, MethodDefinitionHandle ctor)
		{
			var args = AttributeStringArgs (reader, attributes, ctor);
			return args.Count > 0 ? args [0] : null;
		}

		/// <summary>
		/// Collects the <c>ldstr</c> operand offsets and the strings they load, so a rebuilt body
		/// can be compared against the source instruction-for-instruction.
		/// </summary>
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

		static void AssertTableRowCountsMatch (MetadataReader expected, MetadataReader actual, params TableIndex [] except)
		{
			for (int i = 0; i < MetadataTokens.TableCount; i++) {
				var table = (TableIndex) i;
				if (Array.IndexOf (except, table) >= 0) {
					continue;
				}
				Assert.AreEqual (expected.GetTableRowCount (table), actual.GetTableRowCount (table), $"Row count of table '{table}' changed.");
			}
		}

		[Test]
		public void RewritesBareMemberAndDescriptorForAReferencedJniClass ()
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
				"a",
				"Landroid/content/Context;",
			}, LoadedStrings (peReader, reader, method).ConvertAll (entry => entry.Value));
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

			int ctorBody = fixture.EmitReturnOnlyBody ();
			MethodDefinitionHandle ctor = fixture.AddVoidMethod (".ctor", ctorBody,
				MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
			fixture.Metadata.AddCustomAttribute (ctor, fixture.JniConstructorSignatureCtor1, fixture.AttributeBlob (callbackDescriptor));

			TypeDefinitionHandle myView = fixture.AddType ("Acme.Orig", "MyView", fieldStart, methodStart);
			fixture.Metadata.AddCustomAttribute (myView, fixture.RegisterCtor1, fixture.AttributeBlob (myViewJni));

			fieldStart = fixture.NextFieldRid;
			methodStart = fixture.NextMethodRid;
			TypeDefinitionHandle marker = fixture.AddType ("Acme.Orig", "Marker", fieldStart, methodStart);
			fixture.Metadata.AddCustomAttribute (marker, fixture.JniTypeSignatureCtor1, fixture.AttributeBlob ("acme/orig/Marker"));

			// A nested type with no JNI identity of its own inherits its owner from MyView.
			UserStringHandle nestedRun = fixture.String ("run:()V:n_Run");
			fieldStart = fixture.NextFieldRid;
			methodStart = fixture.NextMethodRid;
			fixture.AddVoidMethod ("Run", fixture.EmitLoadStringBody (nestedRun));
			TypeDefinitionHandle nested = fixture.AddType (null, "Nested", fieldStart, methodStart,
				TypeAttributes.NestedPublic | TypeAttributes.Class | TypeAttributes.BeforeFieldInit);
			fixture.Metadata.AddNestedType (nested, myView);

			byte [] source = fixture.Serialize ();
			JniRewriteResult result = Rewrite (source, Mapping (
				"acme.orig.MyView -> a.b.C:\n" +
				"    void onClick(acme.orig.Callback) -> a\n" +
				"    int someField -> x\n" +
				"    void run() -> b\n" +
				"    void <init>(acme.orig.Callback) -> <init>\n" +
				"acme.orig.Callback -> a.b.Cb:\n" +
				"acme.orig.Marker -> a.b.D:\n"));

			Assert.Greater (result.ReplacementCount, 0);

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
				AttributeStringArgs (reader, reader.GetMethodDefinition (ctor).GetCustomAttributes (), fixture.JniConstructorSignatureCtor1),
				"JniConstructorSignatureAttribute's only argument is the descriptor.");

			using var sourceReader = new PEReader (ImmutableArray.Create (source));
			var strings = LoadedStrings (peReader, reader, onClick);
			CollectionAssert.AreEqual (new [] {
				"a." + rewrittenCallbackDescriptor,
				"x.I",
				"a/b/D",
				"a:" + rewrittenCallbackDescriptor + ":n_OnClick_Lacme_orig_Callback_Handler",
				"a:" + rewrittenCallbackDescriptor + ":n_OnClick_Lacme_orig_Callback_Handler\nunused:()V:n_Unused",
				"a:" + rewrittenCallbackDescriptor + ":n_OnClick_Lacme_orig_Callback_Handler\n",
				"this is an ordinary string, untouched",
			}, ValuesOf (strings));

			// The nested type resolves its owner from the enclosing MyView type.
			MethodDefinitionHandle run = FirstMethodOf (reader, nested);
			CollectionAssert.AreEqual (new [] { "b:()V:n_Run" }, ValuesOf (LoadedStrings (peReader, reader, run)));
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

		[Test]
		public void SharedLoadedStringGetsOwnerSpecificReplacements ()
		{
			var fixture = new JniFixtureBuilder ();

			// One deduplicated #US entry, used by two classes that R8 renames differently.
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

			byte [] source = fixture.Serialize ();
			JniRewriteResult result = Rewrite (source, Mapping (
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
		public void IdentifiesUtf8FieldRvaDataStructurally ()
		{
			var fixture = new JniFixtureBuilder ();
			FieldDefinitionHandle nameField = fixture.AddUtf8Field ("onClick");
			FieldDefinitionHandle signatureField = fixture.AddUtf8Field ("(Lacme/orig/Callback;)V");

			using var peReader = new PEReader (ImmutableArray.Create (fixture.Serialize ()));
			MetadataReader reader = peReader.GetMetadataReader ();
			FieldRvaTable table = FieldRvaTable.Read (peReader, reader);

			Assert.AreEqual (2, table.Entries.Count);

			FieldRvaEntry name = table.Get (nameField);
			Assert.IsNotNull (name);
			Assert.IsTrue (name.IsUtf8Datum, "A __utf8_N mapped field must be recognised structurally.");
			Assert.AreEqual ("onClick", name.Utf8Value);

			FieldRvaEntry signature = table.Get (signatureField);
			Assert.IsNotNull (signature);
			Assert.IsTrue (signature.IsUtf8Datum);
			Assert.AreEqual ("(Lacme/orig/Callback;)V", signature.Utf8Value);
		}

		[Test]
		public void RewritesUtf8FieldRvaJniNamesAndSignatures ()
		{
			var fixture = new JniFixtureBuilder ();

			FieldDefinitionHandle nameField = fixture.AddUtf8Field ("onClick");
			FieldDefinitionHandle signatureField = fixture.AddUtf8Field ("(Lacme/orig/Callback;)V");
			FieldDefinitionHandle classNameField = fixture.AddUtf8Field ("acme/orig/Callback");
			FieldDefinitionHandle longNameField = fixture.AddUtf8Field ("run");

			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			int ctorBody = fixture.EmitBody (encoder => {
				encoder.OpCode (ILOpCode.Ldarg_0);
				encoder.LoadString (fixture.String ("acme/orig/MyView"));
				encoder.OpCode (ILOpCode.Pop);
				encoder.OpCode (ILOpCode.Ret);
			});
			fixture.AddVoidMethod (".ctor", ctorBody,
				MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

			int registerBody = fixture.EmitBody (encoder => {
				encoder.OpCode (ILOpCode.Ldsflda);
				encoder.Token (nameField);
				encoder.OpCode (ILOpCode.Ldsflda);
				encoder.Token (signatureField);
				encoder.OpCode (ILOpCode.Pop);
				encoder.OpCode (ILOpCode.Pop);
				encoder.OpCode (ILOpCode.Ldsflda);
				encoder.Token (longNameField);
				encoder.OpCode (ILOpCode.Ldsflda);
				encoder.Token (signatureField);
				encoder.OpCode (ILOpCode.Pop);
				encoder.OpCode (ILOpCode.Pop);
				encoder.OpCode (ILOpCode.Ret);
			});
			fixture.AddVoidMethod ("RegisterNatives", registerBody);

			// A JavaPeerProxy-derived type carries its JNI identity in its .ctor's only ldstr.
			fixture.AddType ("Acme.Orig", "MyViewProxy", fieldStart, methodStart,
				TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, fixture.JavaPeerProxyReference);

			byte [] source = fixture.Serialize ();
			JniRewriteResult result = Rewrite (source, Mapping (
				"acme.orig.MyView -> a.b.C:\n" +
				"    void onClick(acme.orig.Callback) -> a\n" +
				"    void run(acme.orig.Callback) -> aMuchLongerObfuscatedName\n" +
				"acme.orig.Callback -> a.b.Cb:\n"));

			using var peReader = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader reader = peReader.GetMetadataReader ();

			Assert.AreEqual ("a", ReadUtf8Field (peReader, reader, nameField), "The method name is renamed using the owning proxy's JNI class.");
			Assert.AreEqual ("(La/b/Cb;)V", ReadUtf8Field (peReader, reader, signatureField));
			Assert.AreEqual ("a/b/Cb", ReadUtf8Field (peReader, reader, classNameField), "An unreferenced datum that is a known class name is still renamed.");
			Assert.AreEqual ("aMuchLongerObfuscatedName", ReadUtf8Field (peReader, reader, longNameField), "A longer datum is relocated into a wider __utf8_N slot.");

			// Growing a datum appends exactly one new sized type; no existing token moves.
			using var sourceReader = new PEReader (ImmutableArray.Create (source));
			MetadataReader before = sourceReader.GetMetadataReader ();
			Assert.AreEqual (before.GetTableRowCount (TableIndex.TypeDef) + 1, reader.GetTableRowCount (TableIndex.TypeDef));
			AssertTableRowCountsMatch (before, reader, TableIndex.TypeDef, TableIndex.ClassLayout, TableIndex.NestedClass);
		}

		static string ReadUtf8Field (PEReader peReader, MetadataReader reader, FieldDefinitionHandle field)
		{
			FieldDefinition definition = reader.GetFieldDefinition (field);
			int rva = definition.GetRelativeVirtualAddress ();
			Assert.AreNotEqual (0, rva, "Field has no RVA.");

			PEMemoryBlock block = peReader.GetSectionData (rva);
			var bytes = new List<byte> ();
			BlobReader blob = block.GetReader (0, Math.Min (block.Length, 256));
			for (byte b = blob.ReadByte (); b != 0; b = blob.ReadByte ()) {
				bytes.Add (b);
			}
			return System.Text.Encoding.UTF8.GetString (bytes.ToArray ());
		}

		[Test]
		public void FailsWhenASharedUtf8DatumNeedsTwoDifferentNames ()
		{
			var fixture = new JniFixtureBuilder ();

			FieldDefinitionHandle shared = fixture.AddUtf8Field ("go");
			FieldDefinitionHandle signature = fixture.AddUtf8Field ("()V");

			AddProxy (fixture, "acme/orig/P1", shared, signature);
			AddProxy (fixture, "acme/orig/P2", shared, signature);

			var exception = Assert.Throws<JniRewriteException> (() => Rewrite (fixture.Serialize (), Mapping (
				"acme.orig.P1 -> a.b.P1:\n" +
				"    void go() -> z\n" +
				"acme.orig.P2 -> a.b.P2:\n" +
				"    void go() -> q\n")));
			StringAssert.Contains ("shared", exception.Message.ToLowerInvariant ());
		}

		static void AddProxy (JniFixtureBuilder fixture, string jniName, FieldDefinitionHandle nameField, FieldDefinitionHandle signatureField)
		{
			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;

			fixture.AddVoidMethod (".ctor", fixture.EmitBody (encoder => {
				encoder.OpCode (ILOpCode.Ldarg_0);
				encoder.LoadString (fixture.String (jniName));
				encoder.OpCode (ILOpCode.Pop);
				encoder.OpCode (ILOpCode.Ret);
			}), MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

			fixture.AddVoidMethod ("RegisterNatives", fixture.EmitBody (encoder => {
				encoder.OpCode (ILOpCode.Ldsflda);
				encoder.Token (nameField);
				encoder.OpCode (ILOpCode.Ldsflda);
				encoder.Token (signatureField);
				encoder.OpCode (ILOpCode.Pop);
				encoder.OpCode (ILOpCode.Pop);
				encoder.OpCode (ILOpCode.Ret);
			}));

			fixture.AddType ("Acme.Orig", jniName.Replace ('/', '_'), fieldStart, methodStart,
				TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, fixture.JavaPeerProxyReference);
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

				// Two-byte opcodes: ceq (0xFE 0x01) and ldloc (0xFE 0x0C) via LoadLocal (300).
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

			// Every byte except the four-byte ldstr operands is untouched, so branch targets and
			// PDB IL offsets remain valid.
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
		public void RebuildsAnAssemblyWhoseUserStringHeapIsAbsent ()
		{
			var fixture = new JniFixtureBuilder ();

			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			fixture.AddVoidMethod ("Go", fixture.EmitReturnOnlyBody ());
			TypeDefinitionHandle type = fixture.AddType ("Acme.Orig", "NoStrings", fieldStart, methodStart);
			fixture.Metadata.AddCustomAttribute (type, fixture.JniTypeSignatureCtor1, fixture.AttributeBlob ("acme/orig/NoStrings"));

			// MetadataBuilder always emits a (empty) #US stream; drop its stream header so the
			// rewriter is exercised against an assembly that genuinely has no #US heap at all,
			// the way ilasm and some post-processing tools emit them.
			byte [] source = RemoveUserStringStreamHeader (fixture.Serialize ());
			using (var sourcePe = new PEReader (ImmutableArray.Create (source))) {
				Assert.AreEqual (0, sourcePe.GetMetadataReader ().GetHeapSize (HeapIndex.UserString),
					"The fixture must have no #US heap for this test to be meaningful.");
			}

			JniRewriteResult result = Rewrite (source, Mapping ("acme.orig.NoStrings -> a.b.N:\n"));

			using var peReader = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader reader = peReader.GetMetadataReader ();
			Assert.AreEqual ("a/b/N", FirstAttributeStringArg (reader, reader.GetTypeDefinition (type).GetCustomAttributes (), fixture.JniTypeSignatureCtor1));
			Assert.AreEqual ("Go", reader.GetString (reader.GetMethodDefinition (FirstMethodOf (reader, type)).Name));
		}

		/// <summary>
		/// Removes the <c>#US</c> entry from the metadata root's stream header list, leaving every
		/// other stream's (root-relative) offset untouched.
		/// </summary>
		static byte [] RemoveUserStringStreamHeader (byte [] image)
		{
			var result = (byte []) image.Clone ();
			using var peReader = new PEReader (ImmutableArray.Create (image));
			int root = peReader.PEHeaders.MetadataStartOffset;

			int position = root + 4 + 2 + 2 + 4;
			int versionLength = BitConverter.ToInt32 (result, position);
			position += 4 + versionLength + 2;

			int streamCountOffset = position;
			int streamCount = BitConverter.ToUInt16 (result, streamCountOffset);
			position += 2;

			int headersStart = position;
			var kept = new List<byte> ();
			for (int i = 0; i < streamCount; i++) {
				int entryStart = position;
				position += 8;
				int nameStart = position;
				while (result [position] != 0) {
					position++;
				}
				string name = System.Text.Encoding.ASCII.GetString (result, nameStart, position - nameStart);
				position++;
				position = headersStart + (position - headersStart + 3) / 4 * 4;

				if (name != "#US") {
					for (int b = entryStart; b < position; b++) {
						kept.Add (result [b]);
					}
				}
			}

			Assert.AreNotEqual (streamCount * 8, kept.Count, "The fixture is expected to contain a #US stream header.");

			int headersEnd = position;
			for (int i = 0; i < kept.Count; i++) {
				result [headersStart + i] = kept [i];
			}
			for (int i = headersStart + kept.Count; i < headersEnd; i++) {
				result [i] = 0;
			}

			byte [] newCount = BitConverter.GetBytes ((ushort) (streamCount - 1));
			result [streamCountOffset] = newCount [0];
			result [streamCountOffset + 1] = newCount [1];
			return result;
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
			Assert.IsFalse (result.StrongNameSignatureCleared);

			using var sourcePe = new PEReader (ImmutableArray.Create (source));
			using var rewrittenPe = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader before = sourcePe.GetMetadataReader ();
			MetadataReader after = rewrittenPe.GetMetadataReader ();

			Assert.AreEqual (before.GetGuid (before.GetModuleDefinition ().Mvid), after.GetGuid (after.GetModuleDefinition ().Mvid), "MVID must be preserved.");
			Assert.AreEqual (before.MetadataVersion, after.MetadataVersion);
			Assert.AreEqual (before.GetString (before.GetAssemblyDefinition ().Name), after.GetString (after.GetAssemblyDefinition ().Name));
			Assert.AreEqual (before.GetAssemblyDefinition ().Version, after.GetAssemblyDefinition ().Version);
			Assert.AreEqual (before.GetAssemblyDefinition ().HashAlgorithm, after.GetAssemblyDefinition ().HashAlgorithm);
			Assert.AreEqual (sourcePe.PEHeaders.CoffHeader.Machine, rewrittenPe.PEHeaders.CoffHeader.Machine);
			Assert.AreEqual (sourcePe.PEHeaders.CoffHeader.Characteristics, rewrittenPe.PEHeaders.CoffHeader.Characteristics);
			Assert.AreEqual (sourcePe.PEHeaders.CoffHeader.TimeDateStamp, rewrittenPe.PEHeaders.CoffHeader.TimeDateStamp);
			Assert.AreEqual (sourcePe.PEHeaders.PEHeader.Subsystem, rewrittenPe.PEHeaders.PEHeader.Subsystem);
			Assert.AreEqual (sourcePe.PEHeaders.PEHeader.DllCharacteristics, rewrittenPe.PEHeaders.PEHeader.DllCharacteristics);
			Assert.AreEqual (sourcePe.PEHeaders.CorHeader.Flags, rewrittenPe.PEHeaders.CorHeader.Flags);

			var sourceEntries = sourcePe.ReadDebugDirectory ();
			var rewrittenEntries = rewrittenPe.ReadDebugDirectory ();
			Assert.AreEqual (sourceEntries.Length, rewrittenEntries.Length, "Debug directory entry count changed.");
			for (int i = 0; i < sourceEntries.Length; i++) {
				Assert.AreEqual (sourceEntries [i].Type, rewrittenEntries [i].Type);
				Assert.AreEqual (sourceEntries [i].Stamp, rewrittenEntries [i].Stamp);
				Assert.AreEqual (sourceEntries [i].MajorVersion, rewrittenEntries [i].MajorVersion);
				Assert.AreEqual (sourceEntries [i].MinorVersion, rewrittenEntries [i].MinorVersion);
			}

			CodeViewDebugDirectoryData codeView = rewrittenPe.ReadCodeViewDebugDirectoryData (rewrittenEntries [0]);
			Assert.AreEqual (pdbId.Guid, codeView.Guid, "The PDB GUID must still match the portable PDB.");
			Assert.AreEqual ("/some/where/Fixture.pdb", codeView.Path);
			Assert.AreEqual (1, codeView.Age);

			AssertTableRowCountsMatch (before, after);

			// The rewritten string is reachable through the original method token, and the ldstr
			// operand sits at the same IL offset as before.
			var sourceStrings = LoadedStrings (sourcePe, before, go);
			var rewrittenStrings = LoadedStrings (rewrittenPe, after, go);
			Assert.AreEqual (sourceStrings.Count, rewrittenStrings.Count);
			Assert.AreEqual (sourceStrings [0].Key, rewrittenStrings [0].Key, "The ldstr operand moved.");
			Assert.AreEqual ("z.()V", rewrittenStrings [0].Value);
		}

		[Test]
		public void ClearsTheStrongNameSignedFlagButReservesTheSignatureDirectory ()
		{
			const int OriginalStrongNameSignatureSize = 128;

			var fixture = new JniFixtureBuilder {
				Flags = CorFlags.ILOnly | CorFlags.StrongNameSigned,
				StrongNameSignatureSize = OriginalStrongNameSignatureSize,
			};

			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			fixture.AddVoidMethod ("Go", fixture.EmitReturnOnlyBody ());
			TypeDefinitionHandle type = fixture.AddType ("Acme.Orig", "Signed", fieldStart, methodStart);
			fixture.Metadata.AddCustomAttribute (type, fixture.JniTypeSignatureCtor1, fixture.AttributeBlob ("acme/orig/Signed"));

			byte [] source = fixture.Serialize ();
			using (var sourcePe = new PEReader (ImmutableArray.Create (source))) {
				Assert.IsTrue ((sourcePe.PEHeaders.CorHeader.Flags & CorFlags.StrongNameSigned) != 0);
				Assert.AreEqual (OriginalStrongNameSignatureSize, sourcePe.PEHeaders.CorHeader.StrongNameSignatureDirectory.Size);
			}

			JniRewriteResult result = Rewrite (source, Mapping ("acme.orig.Signed -> a.b.S:\n"));
			Assert.IsTrue (result.StrongNameSignatureCleared, "The rewriter must report that it dropped the signature.");

			using var rewrittenPe = new PEReader (ImmutableArray.Create (result.Image));
			CorHeader corHeader = rewrittenPe.PEHeaders.CorHeader;
			Assert.AreEqual (CorFlags.ILOnly, corHeader.Flags, "The StrongNameSigned flag must be cleared, not left stale.");

			// The output must be genuinely delay-signed / re-signable: the original signature
			// directory's size (and a real, non-zero RVA reserving that space in the image) must
			// be preserved, not zeroed out - otherwise there would be nowhere to write a new
			// signature into without another full rewrite.
			Assert.AreEqual (OriginalStrongNameSignatureSize, corHeader.StrongNameSignatureDirectory.Size,
				"The signature directory's reserved size must be preserved so the assembly can be re-signed.");
			Assert.AreNotEqual (0, corHeader.StrongNameSignatureDirectory.RelativeVirtualAddress,
				"The signature directory must still point at reserved space in the image.");
		}

		[Test]
		public void RoundTripsRealAssembliesWithAnEmptyMapping ()
		{
			// Real assemblies exercise the cloner far harder than any hand-built fixture: Win32
			// version resources, generics, P/Invokes, exception handlers, type forwarders,
			// declarative security, embedded resources, and a populated debug directory.
			string [] candidates = {
				typeof (Xamarin.Android.Tasks.JniRemapping.R8Mapping).Assembly.Location,
				typeof (MetadataReader).Assembly.Location,
				typeof (NUnit.Framework.Assert).Assembly.Location,
				typeof (object).Assembly.Location,
				FindLocalRuntimeAssembly ("Mono.Android.dll"),
				FindLocalRuntimeAssembly ("Java.Interop.dll"),
			};

			int checkedCount = 0;
			var skipped = new List<string> ();
			foreach (string path in candidates) {
				if (string.IsNullOrEmpty (path) || !File.Exists (path)) {
					continue;
				}

				byte [] source = File.ReadAllBytes (path);
				JniRewriteResult result;
				try {
					result = Rewrite (source, Mapping ("acme.orig.NothingAtAll -> a.b.C:\n"));
				} catch (JniRewriteException e) {
					// ReadyToRun images and the like are rejected up front rather than silently
					// stripped; that is the documented contract, so record and move on.
					skipped.Add ($"{Path.GetFileName (path)}: {e.Message}");
					continue;
				}

				Assert.AreEqual (0, result.ReplacementCount, $"'{path}' should have no JNI names to rewrite.");
				AssertFaithfulRoundTrip (source, result.Image, path);
				checkedCount++;
			}

			TestContext.Out.WriteLine ($"Round tripped {checkedCount} assembl(ies); skipped: {string.Join ("; ", skipped)}");
			Assert.Greater (checkedCount, 0, "No real assembly was available to round trip.");
		}

		/// <summary>
		/// Locates an assembly from the local build's Android runtime pack, or returns null when
		/// this tree has not produced one.
		/// </summary>
		static string FindLocalRuntimeAssembly (string fileName)
		{
			var directory = new DirectoryInfo (AppContext.BaseDirectory);
			while (directory != null && !Directory.Exists (Path.Combine (directory.FullName, "bin"))) {
				directory = directory.Parent;
			}
			if (directory == null) {
				return null;
			}

			string packs = Path.Combine (directory.FullName, "bin", "Debug", "lib", "packs");
			if (!Directory.Exists (packs)) {
				return null;
			}

			foreach (string candidate in Directory.EnumerateFiles (packs, fileName, SearchOption.AllDirectories)) {
				if (candidate.Contains ("Microsoft.Android.Runtime", StringComparison.Ordinal)) {
					return candidate;
				}
			}
			return null;
		}

		[Test]
		public void RewritesARealAndroidAssembly ()
		{
			string path = FindLocalRuntimeAssembly ("Mono.Android.dll");
			if (path == null) {
				Assert.Ignore ("This tree has not built a Microsoft.Android runtime pack.");
			}

			byte [] source = File.ReadAllBytes (path);
			JniRewriteResult result = Rewrite (source, Mapping (
				"android.app.Activity -> zz.A:\n" +
				"    void onCreate(android.os.Bundle) -> b\n" +
				"android.os.Bundle -> zz.B:\n"));

			Assert.Greater (result.ReplacementCount, 0, "Mono.Android.dll is expected to carry JNI names for android.app.Activity.");

			using var sourcePe = new PEReader (ImmutableArray.Create (source));
			using var rewrittenPe = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader before = sourcePe.GetMetadataReader ();
			MetadataReader after = rewrittenPe.GetMetadataReader ();

			AssertTableRowCountsMatch (before, after);
			Assert.AreEqual (before.GetGuid (before.GetModuleDefinition ().Mvid), after.GetGuid (after.GetModuleDefinition ().Mvid));

			// Every rewritten body must still be a well-formed method body of the same length.
			int methodCount = after.GetTableRowCount (TableIndex.MethodDef);
			for (int rid = 1; rid <= methodCount; rid++) {
				var handle = MetadataTokens.MethodDefinitionHandle (rid);
				int sourceRva = before.GetMethodDefinition (handle).RelativeVirtualAddress;
				int rewrittenRva = after.GetMethodDefinition (handle).RelativeVirtualAddress;
				Assert.AreEqual (sourceRva == 0, rewrittenRva == 0);
				if (sourceRva == 0) {
					continue;
				}
				Assert.AreEqual (sourcePe.GetMethodBody (sourceRva).GetILBytes ().Length,
					rewrittenPe.GetMethodBody (rewrittenRva).GetILBytes ().Length, $"IL length of method {rid} changed.");
			}

			bool sawRenamedActivity = false;
			foreach (CustomAttributeHandle handle in after.CustomAttributes) {
				CustomAttribute attribute = after.GetCustomAttribute (handle);
				BlobReader blob = after.GetBlobReader (attribute.Value);
				if (blob.Length < 3) {
					continue;
				}
				byte [] bytes = blob.ReadBytes (blob.Length);
				if (IndexOfAscii (bytes, "zz/A") >= 0) {
					sawRenamedActivity = true;
					break;
				}
			}
			Assert.IsTrue (sawRenamedActivity, "No custom attribute picked up the renamed android.app.Activity JNI name.");
		}

		static int IndexOfAscii (byte [] haystack, string needle)
		{
			for (int i = 0; i + needle.Length <= haystack.Length; i++) {
				int j = 0;
				while (j < needle.Length && haystack [i + j] == (byte) needle [j]) {
					j++;
				}
				if (j == needle.Length) {
					return i;
				}
			}
			return -1;
		}

		static void AssertFaithfulRoundTrip (byte [] source, byte [] rewritten, string path)
		{
			using var sourcePe = new PEReader (ImmutableArray.Create (source));
			using var rewrittenPe = new PEReader (ImmutableArray.Create (rewritten));
			MetadataReader before = sourcePe.GetMetadataReader ();
			MetadataReader after = rewrittenPe.GetMetadataReader ();

			AssertTableRowCountsMatch (before, after);
			Assert.AreEqual (before.GetGuid (before.GetModuleDefinition ().Mvid), after.GetGuid (after.GetModuleDefinition ().Mvid), $"MVID of '{path}' changed.");
			Assert.AreEqual (before.MetadataVersion, after.MetadataVersion);
			Assert.AreEqual (sourcePe.PEHeaders.CoffHeader.Machine, rewrittenPe.PEHeaders.CoffHeader.Machine);
			Assert.AreEqual (sourcePe.PEHeaders.PEHeader.Subsystem, rewrittenPe.PEHeaders.PEHeader.Subsystem);
			Assert.AreEqual (sourcePe.PEHeaders.PEHeader.ResourceTableDirectory.Size != 0,
				rewrittenPe.PEHeaders.PEHeader.ResourceTableDirectory.Size != 0, $"Win32 resources of '{path}' were dropped.");
			CollectionAssert.AreEqual (ReadWin32Resources (sourcePe), ReadWin32Resources (rewrittenPe),
				$"Win32 resource contents of '{path}' changed.");

			int methodCount = before.GetTableRowCount (TableIndex.MethodDef);
			for (int rid = 1; rid <= methodCount; rid++) {
				var handle = MetadataTokens.MethodDefinitionHandle (rid);
				MethodDefinition sourceMethod = before.GetMethodDefinition (handle);
				MethodDefinition rewrittenMethod = after.GetMethodDefinition (handle);

				Assert.AreEqual (before.GetString (sourceMethod.Name), after.GetString (rewrittenMethod.Name), $"Method {rid} of '{path}' moved.");
				Assert.AreEqual (sourceMethod.Attributes, rewrittenMethod.Attributes);
				Assert.AreEqual (sourceMethod.RelativeVirtualAddress == 0, rewrittenMethod.RelativeVirtualAddress == 0);
				if (sourceMethod.RelativeVirtualAddress == 0) {
					continue;
				}

				MethodBodyBlock sourceBody = sourcePe.GetMethodBody (sourceMethod.RelativeVirtualAddress);
				MethodBodyBlock rewrittenBody = rewrittenPe.GetMethodBody (rewrittenMethod.RelativeVirtualAddress);
				CollectionAssert.AreEqual (sourceBody.GetILBytes (), rewrittenBody.GetILBytes (),
					$"IL of method {rid} of '{path}' changed even though nothing was rewritten.");
				Assert.AreEqual (sourceBody.MaxStack, rewrittenBody.MaxStack);
				Assert.AreEqual (sourceBody.LocalSignature, rewrittenBody.LocalSignature);
				Assert.AreEqual (sourceBody.LocalVariablesInitialized, rewrittenBody.LocalVariablesInitialized);
				Assert.AreEqual (sourceBody.ExceptionRegions.Length, rewrittenBody.ExceptionRegions.Length);
			}

			int typeCount = before.GetTableRowCount (TableIndex.TypeDef);
			for (int rid = 1; rid <= typeCount; rid++) {
				var handle = MetadataTokens.TypeDefinitionHandle (rid);
				Assert.AreEqual (before.GetString (before.GetTypeDefinition (handle).Name),
					after.GetString (after.GetTypeDefinition (handle).Name), $"Type {rid} of '{path}' moved.");
			}

			// Every FieldRVA-mapped data block (static array initializers, and similar) must
			// survive byte-for-byte: a wrong size computed from the field's value type would
			// silently truncate or overrun this comparison.
			FieldRvaTable sourceFieldRva = FieldRvaTable.Read (sourcePe, before);
			FieldRvaTable rewrittenFieldRva = FieldRvaTable.Read (rewrittenPe, after);
			Assert.AreEqual (sourceFieldRva.Entries.Count, rewrittenFieldRva.Entries.Count, $"FieldRVA row count of '{path}' changed.");
			foreach (FieldRvaEntry sourceEntry in sourceFieldRva.Entries) {
				FieldRvaEntry rewrittenEntry = rewrittenFieldRva.Get (sourceEntry.Field);
				Assert.IsNotNull (rewrittenEntry, $"FieldRVA row for field {MetadataTokens.GetToken (sourceEntry.Field):X} of '{path}' disappeared.");
				CollectionAssert.AreEqual (sourceEntry.Data, rewrittenEntry.Data,
					$"FieldRVA data for field {MetadataTokens.GetToken (sourceEntry.Field):X} of '{path}' changed.");
			}

			foreach (ManifestResourceHandle handle in before.ManifestResources) {
				ManifestResource sourceResource = before.GetManifestResource (handle);
				if (!sourceResource.Implementation.IsNil) {
					continue;
				}
				CollectionAssert.AreEqual (
					ReadResource (sourcePe, before, before.GetString (sourceResource.Name)),
					ReadResource (rewrittenPe, after, before.GetString (sourceResource.Name)),
					$"Embedded resource '{before.GetString (sourceResource.Name)}' of '{path}' changed.");
			}
		}

		[Test]
		public void RewrittenAssemblyLoadsAndRunsInTheRuntime ()
		{
			// Metadata that merely parses is not enough: the runtime has to accept the rebuilt PE
			// and JIT code out of it.
			string path = typeof (Xamarin.Android.Tasks.JniRemapping.R8Mapping).Assembly.Location;
			if (string.IsNullOrEmpty (path) || !File.Exists (path)) {
				Assert.Ignore ("The assembly under test is not available on disk.");
			}

			JniRewriteResult result = Rewrite (File.ReadAllBytes (path), Mapping ("acme.orig.NothingAtAll -> a.b.C:\n"));

			string directory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (directory);
			string rewrittenPath = Path.Combine (directory, Path.GetFileName (path));
			File.WriteAllBytes (rewrittenPath, result.Image);

			var context = new System.Runtime.Loader.AssemblyLoadContext (TestName, isCollectible: true);
			try {
				context.Resolving += (loadContext, name) => {
					string candidate = Path.Combine (Path.GetDirectoryName (path), name.Name + ".dll");
					return File.Exists (candidate) ? loadContext.LoadFromAssemblyPath (candidate) : null;
				};

				Assembly assembly = context.LoadFromAssemblyPath (rewrittenPath);
				Type mappingType = assembly.GetType ("Xamarin.Android.Tasks.JniRemapping.R8Mapping", throwOnError: true);
				MethodInfo parse = mappingType.GetMethod ("Parse", BindingFlags.Public | BindingFlags.Static);
				Assert.IsNotNull (parse, "R8Mapping.Parse is missing from the rewritten assembly.");

				object mapping = parse.Invoke (null, new object [] { new StringReader ("acme.orig.Foo -> a.b.C:\n") });
				MethodInfo tryGetRenamedClass = mappingType.GetMethod ("TryGetRenamedClass", BindingFlags.Public | BindingFlags.Instance);
				Assert.IsNotNull (tryGetRenamedClass);

				var arguments = new object [] { "acme/orig/Foo", null };
				Assert.IsTrue ((bool) tryGetRenamedClass.Invoke (mapping, arguments), "The rewritten assembly's code did not run correctly.");
				Assert.AreEqual ("a/b/C", arguments [1]);
			} finally {
				context.Unload ();
			}
		}

		/// <summary>
		/// Flattens the Win32 resource directory into "type/name/language = bytes" entries, so a
		/// relocated <c>.rsrc</c> section can be compared against the original without depending
		/// on the RVAs that legitimately changed.
		/// </summary>
		static List<string> ReadWin32Resources (PEReader peReader)
		{
			var result = new List<string> ();
			DirectoryEntry directory = peReader.PEHeaders.PEHeader.ResourceTableDirectory;
			if (directory.Size == 0) {
				return result;
			}

			byte [] section = peReader.GetSectionData (directory.RelativeVirtualAddress).GetReader (0, directory.Size).ReadBytes (directory.Size);
			WalkWin32Resources (peReader, section, directory.RelativeVirtualAddress, 0, "", result);
			result.Sort (StringComparer.Ordinal);
			return result;
		}

		static void WalkWin32Resources (PEReader peReader, byte [] section, int sectionRva, int directoryOffset, string prefix, List<string> result)
		{
			int namedEntries = BitConverter.ToUInt16 (section, directoryOffset + 12);
			int idEntries = BitConverter.ToUInt16 (section, directoryOffset + 14);
			int entryOffset = directoryOffset + 16;

			for (int i = 0; i < namedEntries + idEntries; i++, entryOffset += 8) {
				uint name = BitConverter.ToUInt32 (section, entryOffset);
				uint offsetToData = BitConverter.ToUInt32 (section, entryOffset + 4);
				string key = prefix + "/" + name.ToString ("X8");

				if ((offsetToData & 0x80000000) != 0) {
					WalkWin32Resources (peReader, section, sectionRva, (int) (offsetToData & 0x7FFFFFFF), key, result);
					continue;
				}

				int dataEntry = (int) offsetToData;
				int dataRva = (int) BitConverter.ToUInt32 (section, dataEntry);
				int size = (int) BitConverter.ToUInt32 (section, dataEntry + 4);
				byte [] data = peReader.GetSectionData (dataRva).GetReader (0, size).ReadBytes (size);
				result.Add (key + " = " + BitConverter.ToString (data));
			}
		}

		[Test]
		public void IlOpcodeTableCoversEveryDefinedOpcode ()
		{
			// A missing entry would make the IL walker throw on a perfectly ordinary assembly.
			var missing = new List<string> ();
			foreach (ILOpCode code in Enum.GetValues (typeof (ILOpCode))) {
				if (code == ILOpCode.Switch) {
					continue; // Variable-length operand, handled explicitly by the scanner.
				}
				if (!Xamarin.Android.Tasks.JniRemapping.IlOpcodeTable.OperandSizes.ContainsKey ((ushort) code)) {
					missing.Add (code.ToString ());
				}
			}

			CollectionAssert.IsEmpty (missing, "IlOpcodeTable is missing operand sizes for these opcodes.");
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

			// A real portable PDB with a sequence point at IL offset 0 of Go().
			var pdbMetadata = new MetadataBuilder ();
			DocumentHandle document = pdbMetadata.AddDocument (
				pdbMetadata.GetOrAddDocumentName ("/src/Fixture.cs"), default, default, default);
			var sequencePoints = new BlobBuilder ();
			sequencePoints.WriteCompressedInteger (0); // LocalSignature: none
			sequencePoints.WriteCompressedInteger (0); // IL offset
			sequencePoints.WriteCompressedInteger (1); // delta lines
			sequencePoints.WriteCompressedInteger (10); // delta columns
			sequencePoints.WriteCompressedInteger (5); // start line
			sequencePoints.WriteCompressedInteger (1); // start column
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

			byte [] source = fixture.Serialize ();
			using (var pdbStream = File.Create (pdbPath)) {
				pdbBlob.WriteContentTo (pdbStream);
			}

			JniRewriteResult result = Rewrite (source, Mapping (
				"acme.orig.Debuggable -> a.b.D:\n" +
				"    void go() -> z\n"));
			File.WriteAllBytes (assemblyPath, result.Image);

			// The unmodified PDB must still be accepted for the rewritten assembly...
			using var peReader = new PEReader (File.OpenRead (assemblyPath));
			Assert.IsTrue (peReader.TryOpenAssociatedPortablePdb (assemblyPath, File.OpenRead, out MetadataReaderProvider provider, out string _),
				"The rewritten assembly no longer matches its unchanged portable PDB.");

			using (provider) {
				MetadataReader pdbReader = provider.GetMetadataReader ();
				MetadataReader reader = peReader.GetMetadataReader ();

				// ...and its sequence points must still address real IL in the rewritten body.
				var points = new List<SequencePoint> (pdbReader.GetMethodDebugInformation (go).GetSequencePoints ());
				Assert.AreEqual (1, points.Count);
				Assert.AreEqual (0, points [0].Offset);
				Assert.AreEqual (5, points [0].StartLine);

				byte [] il = peReader.GetMethodBody (reader.GetMethodDefinition (go).RelativeVirtualAddress).GetILBytes ();
				Assert.Greater (il.Length, points [0].Offset);
				Assert.AreEqual ((byte) ILOpCode.Ldstr, il [0], "The sequence point no longer points at the ldstr it was emitted for.");
				CollectionAssert.AreEqual (new [] { "z.()V" }, ValuesOf (LoadedStrings (peReader, reader, go)));
			}
		}

		[Test]
		public void PreservesMappedFieldDataThatIsNotAJniDatum ()
		{
			var fixture = new JniFixtureBuilder ();

			// A plain C#-style array initializer blob: not a __utf8_N datum, so it must survive
			// byte-for-byte.
			var payload = new byte [] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04 };
			TypeDefinitionHandle enclosing = fixture.EnsurePrivateImplementationDetails ();
			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			TypeDefinitionHandle arrayType = fixture.AddType (null, "__StaticArrayInitTypeSize=8", fieldStart, methodStart,
				TypeAttributes.NestedPrivate | TypeAttributes.ExplicitLayout | TypeAttributes.Sealed | TypeAttributes.AnsiClass,
				fixture.ValueTypeReference);
			fixture.Metadata.AddTypeLayout (arrayType, packingSize: 1, size: (uint) payload.Length);
			fixture.Metadata.AddNestedType (arrayType, enclosing);

			var signature = new BlobBuilder ();
			new BlobEncoder (signature).FieldSignature ().Type (arrayType, isValueType: true);
			int rva = fixture.MappedFieldData.Count;
			fixture.MappedFieldData.WriteBytes (payload);
			FieldDefinitionHandle dataField = fixture.Metadata.AddFieldDefinition (
				FieldAttributes.Static | FieldAttributes.Assembly | FieldAttributes.HasFieldRVA,
				fixture.Metadata.GetOrAddString ("ArrayData"), fixture.Metadata.GetOrAddBlob (signature));
			fixture.Metadata.AddFieldRelativeVirtualAddress (dataField, rva);

			JniRewriteResult result = Rewrite (fixture.Serialize (), Mapping ("acme.orig.Nothing -> a.b.N:\n"));

			using var peReader = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader reader = peReader.GetMetadataReader ();
			int newRva = reader.GetFieldDefinition (dataField).GetRelativeVirtualAddress ();
			Assert.AreNotEqual (0, newRva);
			CollectionAssert.AreEqual (payload, peReader.GetSectionData (newRva).GetReader (0, payload.Length).ReadBytes (payload.Length));
		}

		[Test]
		public void RejectsFieldRvaValueTypeWithoutAnExplicitClassLayoutSize ()
		{
			// A mapped value type with no ClassLayout row (or a zero size) cannot be sized safely:
			// summing its instance fields would be a guess about the CLR's actual layout, and a
			// wrong guess risks truncating - or reading past the end of - the mapped data. The
			// rewriter must refuse rather than take that risk.
			var fixture = new JniFixtureBuilder ();

			TypeDefinitionHandle enclosing = fixture.EnsurePrivateImplementationDetails ();
			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			TypeDefinitionHandle unsizedType = fixture.AddType (null, "__UnsizedBlob", fieldStart, methodStart,
				TypeAttributes.NestedPrivate | TypeAttributes.ExplicitLayout | TypeAttributes.Sealed | TypeAttributes.AnsiClass,
				fixture.ValueTypeReference);
			fixture.Metadata.AddNestedType (unsizedType, enclosing);
			// Deliberately no fixture.Metadata.AddTypeLayout (...) call: the type has no
			// ClassLayout row at all.

			var signature = new BlobBuilder ();
			new BlobEncoder (signature).FieldSignature ().Type (unsizedType, isValueType: true);
			int rva = fixture.MappedFieldData.Count;
			fixture.MappedFieldData.WriteBytes (new byte [] { 0x01, 0x02, 0x03, 0x04 });
			FieldDefinitionHandle dataField = fixture.Metadata.AddFieldDefinition (
				FieldAttributes.Static | FieldAttributes.Assembly | FieldAttributes.HasFieldRVA,
				fixture.Metadata.GetOrAddString ("UnsizedData"), fixture.Metadata.GetOrAddBlob (signature));
			fixture.Metadata.AddFieldRelativeVirtualAddress (dataField, rva);

			byte [] source = fixture.Serialize ();
			var ex = Assert.Throws<JniRewriteException> (() => Rewrite (source, Mapping ("acme.orig.Nothing -> a.b.N:\n")));
			StringAssert.Contains ("ClassLayout", ex.Message);
		}

		[Test]
		public void RejectsAnEmbeddedResourceLengthPrefixThatWouldOverflowTheBoundsCheck ()
		{
			// A length prefix close to int.MaxValue makes "offset + sizeof(int) + size" overflow
			// a 32-bit sum and wrap around to a small (or negative) value; with plain int
			// arithmetic that would slip past the bounds check below instead of being caught by
			// it. The resource must be rejected, not read out of bounds.
			var fixture = new JniFixtureBuilder ();

			fixture.ManagedResources.WriteInt32 (int.MaxValue);
			fixture.Metadata.AddManifestResource (ManifestResourceAttributes.Public, fixture.Metadata.GetOrAddString ("Overflowing.resources"), default, offset: 0);
			TypeDefinitionHandle resourceOwner = fixture.AddType ("Acme.Orig", "ResourceOwner", fixture.NextFieldRid, fixture.NextMethodRid);
			fixture.Metadata.AddCustomAttribute (resourceOwner, fixture.RegisterCtor1, fixture.AttributeBlob ("acme/orig/ResourceOwner"));

			byte [] source = fixture.Serialize ();
			var ex = Assert.Throws<JniRewriteException> (() => Rewrite (source, Mapping ("acme.orig.ResourceOwner -> a.b.R:\n")));
			StringAssert.Contains ("extends past the end of the resources directory", ex.Message);
		}

		[Test]
		public void PreservesPInvokesConstantsPropertiesAndGenerics ()
		{
			var fixture = new JniFixtureBuilder ();

			ModuleReferenceHandle moduleRef = fixture.Metadata.AddModuleReference (fixture.Metadata.GetOrAddString ("libc"));

			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;

			FieldDefinitionHandle constant = fixture.Metadata.AddFieldDefinition (
				FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault,
				fixture.Metadata.GetOrAddString ("Answer"), fixture.Metadata.GetOrAddBlob (Int32FieldSignature ()));
			fixture.Metadata.AddConstant (constant, 42);

			FieldDefinitionHandle backing = fixture.Metadata.AddFieldDefinition (FieldAttributes.Private,
				fixture.Metadata.GetOrAddString ("backing"), fixture.Metadata.GetOrAddBlob (Int32FieldSignature ()));

			MethodDefinitionHandle getter = fixture.Metadata.AddMethodDefinition (
				MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
				MethodImplAttributes.IL, fixture.Metadata.GetOrAddString ("get_Value"),
				fixture.Metadata.GetOrAddBlob (Int32NoArgsMethodSignature ()),
				fixture.EmitBody (encoder => {
					encoder.LoadConstantI4 (0);
					encoder.OpCode (ILOpCode.Ret);
				}),
				MetadataTokens.ParameterHandle (fixture.Metadata.GetRowCount (TableIndex.Param) + 1));

			MethodDefinitionHandle pinvoke = fixture.Metadata.AddMethodDefinition (
				MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.PinvokeImpl,
				MethodImplAttributes.IL | MethodImplAttributes.PreserveSig, fixture.Metadata.GetOrAddString ("Getpid"),
				fixture.Metadata.GetOrAddBlob (Int32NoArgsStaticMethodSignature ()), -1,
				MetadataTokens.ParameterHandle (fixture.Metadata.GetRowCount (TableIndex.Param) + 1));
			fixture.Metadata.AddMethodImport (pinvoke, MethodImportAttributes.CallingConventionCDecl,
				fixture.Metadata.GetOrAddString ("getpid"), moduleRef);

			TypeDefinitionHandle type = fixture.AddType ("Acme.Orig", "Rich`1", fieldStart, methodStart);
			fixture.Metadata.AddCustomAttribute (type, fixture.JniTypeSignatureCtor1, fixture.AttributeBlob ("acme/orig/Rich"));
			fixture.Metadata.AddGenericParameter (type, GenericParameterAttributes.None, fixture.Metadata.GetOrAddString ("T"), 0);

			var propertySignature = new BlobBuilder ();
			new BlobEncoder (propertySignature).PropertySignature ().Parameters (0, r => r.Type ().Int32 (), p => { });
			PropertyDefinitionHandle property = fixture.Metadata.AddProperty (PropertyAttributes.None,
				fixture.Metadata.GetOrAddString ("Value"), fixture.Metadata.GetOrAddBlob (propertySignature));
			fixture.Metadata.AddPropertyMap (type, property);
			fixture.Metadata.AddMethodSemantics (property, MethodSemanticsAttributes.Getter, getter);

			byte [] source = fixture.Serialize ();
			JniRewriteResult result = Rewrite (source, Mapping ("acme.orig.Rich -> a.b.R:\n"));

			using var sourcePe = new PEReader (ImmutableArray.Create (source));
			using var rewrittenPe = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader before = sourcePe.GetMetadataReader ();
			MetadataReader after = rewrittenPe.GetMetadataReader ();

			AssertTableRowCountsMatch (before, after);

			Assert.AreEqual ("a/b/R", FirstAttributeStringArg (after, after.GetTypeDefinition (type).GetCustomAttributes (), fixture.JniTypeSignatureCtor1));

			Constant constantRow = after.GetConstant (after.GetFieldDefinition (constant).GetDefaultValue ());
			Assert.AreEqual (ConstantTypeCode.Int32, constantRow.TypeCode);
			Assert.AreEqual (42, after.GetBlobReader (constantRow.Value).ReadInt32 ());

			MethodImport import = after.GetMethodDefinition (pinvoke).GetImport ();
			Assert.AreEqual ("getpid", after.GetString (import.Name));
			Assert.AreEqual ("libc", after.GetString (after.GetModuleReference (import.Module).Name));
			Assert.AreEqual (MethodImportAttributes.CallingConventionCDecl, import.Attributes);

			PropertyAccessors accessors = after.GetPropertyDefinition (property).GetAccessors ();
			Assert.AreEqual (getter, accessors.Getter);

			GenericParameterHandleCollection genericParameters = after.GetTypeDefinition (type).GetGenericParameters ();
			Assert.AreEqual (1, genericParameters.Count);
			Assert.AreEqual ("T", after.GetString (after.GetGenericParameter (genericParameters [0]).Name));
			Assert.AreEqual (0, after.GetMethodDefinition (pinvoke).RelativeVirtualAddress, "A P/Invoke must keep its zero RVA.");
			Assert.AreNotEqual (0, after.GetMethodDefinition (getter).RelativeVirtualAddress);

			Assert.AreEqual ("backing", after.GetString (after.GetFieldDefinition (backing).Name));
		}

		static BlobBuilder Int32FieldSignature ()
		{
			var signature = new BlobBuilder ();
			new BlobEncoder (signature).FieldSignature ().Int32 ();
			return signature;
		}

		static BlobBuilder Int32NoArgsMethodSignature ()
		{
			var signature = new BlobBuilder ();
			new BlobEncoder (signature).MethodSignature (isInstanceMethod: true)
				.Parameters (0, out ReturnTypeEncoder returnType, out ParametersEncoder _);
			returnType.Type ().Int32 ();
			return signature;
		}

		static BlobBuilder Int32NoArgsStaticMethodSignature ()
		{
			var signature = new BlobBuilder ();
			new BlobEncoder (signature).MethodSignature ()
				.Parameters (0, out ReturnTypeEncoder returnType, out ParametersEncoder _);
			returnType.Type ().Int32 ();
			return signature;
		}
	}
}
