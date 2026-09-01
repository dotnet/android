using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class GenerateProguardConfigurationTests : BaseTest
	{
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
		public void WritesOriginalKeepRulesAndReachabilityManifest ()
		{
			string path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			string assembly = Path.Combine (path, "Linked.dll");
			string mapping = Path.Combine (path, "mapping.txt");
			string proguard = Path.Combine (path, "proguard.cfg");
			string rewriteManifest = Path.Combine (path, "rewrite.txt");
			string manifest = Path.Combine (path, "reachability.txt");

			var fixture = new JniFixtureBuilder ();
			BlobHandle findClassSignature = AddLegacyJniMethodSignature (fixture, findClass: true);
			BlobHandle memberLookupSignature = AddLegacyJniMethodSignature (fixture, findClass: false);
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
			var localSignature = new BlobBuilder ();
			new BlobEncoder (localSignature).LocalVariableSignature (1).AddVariable ().Type ().Int32 ();
			StandaloneSignatureHandle locals = fixture.Metadata.AddStandaloneSignature (fixture.Metadata.GetOrAddBlob (localSignature));

			int fieldStart = fixture.NextFieldRid;
			var field = fixture.Metadata.AddFieldDefinition (
				FieldAttributes.Public,
				fixture.Metadata.GetOrAddString ("Count"),
				fixture.Metadata.GetOrAddBlob (new byte [] { 0x06, 0x08 }));

			int methodStart = fixture.NextMethodRid;
			var constructor = fixture.AddVoidMethod (".ctor", fixture.EmitReturnOnlyBody (),
				MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
			var firstOverload = fixture.AddVoidMethod ("OnClick", fixture.EmitReturnOnlyBody ());
			var secondOverload = fixture.AddVoidMethod ("OnClickWithIndex", fixture.EmitReturnOnlyBody ());
			fixture.AddVoidMethod ("DirectLookup", fixture.EmitBody (encoder => {
				encoder.LoadString (fixture.String ("a/b/E"));
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (findClassReference);
				encoder.StoreLocal (0);
				encoder.LoadLocal (0);
				encoder.LoadString (fixture.String ("x"));
				encoder.LoadString (fixture.String ("()V"));
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (getMethodReference);
				encoder.OpCode (ILOpCode.Pop);
				encoder.LoadString (fixture.String ("g"));
				encoder.OpCode (ILOpCode.Pop);
				encoder.OpCode (ILOpCode.Ret);
			}, locals));

			var type = fixture.AddType ("Managed", "MyView", fieldStart, methodStart);
			var property = fixture.Metadata.AddProperty (
				PropertyAttributes.None,
				fixture.Metadata.GetOrAddString ("Enabled"),
				fixture.Metadata.GetOrAddBlob (new byte [] { 0x28, 0x00, 0x08 }));
			fixture.Metadata.AddPropertyMap (type, property);
			var @event = fixture.Metadata.AddEvent (
				EventAttributes.None,
				fixture.Metadata.GetOrAddString ("Listener"),
				fixture.ValueTypeReference);
			fixture.Metadata.AddEventMap (type, @event);

			int interfaceMethodStart = fixture.NextMethodRid;
			var interfaceMethod = fixture.Metadata.AddMethodDefinition (
				MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot,
				MethodImplAttributes.IL,
				fixture.Metadata.GetOrAddString ("Invoke"),
				fixture.Metadata.GetOrAddBlob (new byte [] { 0x20, 0x00, 0x01 }),
				0,
				default);
			var interfaceType = fixture.AddType (
				"Managed",
				"ICallback",
				fixture.NextFieldRid,
				interfaceMethodStart,
				TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);

			fixture.Metadata.AddCustomAttribute (type, fixture.RegisterCtor1, fixture.AttributeBlob ("a/b/C"));
			fixture.Metadata.AddCustomAttribute (constructor, fixture.RegisterCtor3, fixture.AttributeBlob (".ctor", "()V", ""));
			fixture.Metadata.AddCustomAttribute (firstOverload, fixture.RegisterCtor3, fixture.AttributeBlob ("a", "(La/b/D;)V", ""));
			fixture.Metadata.AddCustomAttribute (secondOverload, fixture.RegisterCtor3, fixture.AttributeBlob ("b", "(La/b/D;I)V", ""));
			fixture.Metadata.AddCustomAttribute (field, fixture.RegisterCtor1, fixture.AttributeBlob ("c"));
			fixture.Metadata.AddCustomAttribute (property, fixture.RegisterCtor1, fixture.AttributeBlob ("d"));
			fixture.Metadata.AddCustomAttribute (@event, fixture.RegisterCtor1, fixture.AttributeBlob ("e"));
			fixture.Metadata.AddCustomAttribute (interfaceType, fixture.RegisterCtor1, fixture.AttributeBlob ("a/b/F"));
			fixture.Metadata.AddCustomAttribute (interfaceMethod, fixture.RegisterCtor3, fixture.AttributeBlob ("y", "()V", ""));
			File.WriteAllBytes (assembly, fixture.Serialize ());

			File.WriteAllText (mapping, """
				acme.orig.MyView -> a.b.C:
				    void <init>() -> <init>
				    void onClick(android.view.View) -> a
				    void onClick(android.view.View,int) -> b
				    int count -> c
				    boolean enabled -> d
				    java.lang.Object listener -> e
				android.view.View -> a.b.D:
				acme.orig.DirectTarget -> a.b.E:
				    void run() -> x
				acme.orig.ICallback -> a.b.F:
				    void invoke() -> y
				acme.orig.Synthetic -> g:

				""");
			string expectedManifest = """
				C	acme/orig/DirectTarget
				C	acme/orig/ICallback
				C	acme/orig/MyView
				C	android/view/View
				F	acme/orig/MyView	count
				F	acme/orig/MyView	enabled
				F	acme/orig/MyView	listener
				M	acme/orig/DirectTarget	run():void
				M	acme/orig/ICallback	invoke():void
				M	acme/orig/MyView	<init>():void
				M	acme/orig/MyView	onClick(android.view.View):void
				M	acme/orig/MyView	onClick(android.view.View,int):void

				""".ReplaceLineEndings ();
			File.WriteAllText (rewriteManifest, expectedManifest);

			var task = new GenerateProguardConfiguration {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				LinkedAssemblies = new [] { new TaskItem (assembly) },
				OutputFile = proguard,
				R8MappingFile = mapping,
				R8RewriteManifestFile = rewriteManifest,
				R8ReachabilityManifestFile = manifest,
			};

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			Assert.AreEqual ("""
				-keep,allowobfuscation class acme.orig.DirectTarget
				-keepclassmembers,allowobfuscation class acme.orig.DirectTarget {
				   *** run(...);
				}

				-keep,allowobfuscation class acme.orig.ICallback
				-keepclassmembers,allowobfuscation class acme.orig.ICallback {
				   *** invoke(...);
				}

				-keep,allowobfuscation class acme.orig.MyView
				-keepclassmembers,allowobfuscation class acme.orig.MyView {
				   *** count;
				   *** enabled;
				   *** listener;
				   *** onClick(...);
				   <init>(...);
				}

				-keep,allowobfuscation class android.view.View
				-keepclassmembers,allowobfuscation class android.view.View {
				}


				""".ReplaceLineEndings (), File.ReadAllText (proguard));
			Assert.AreEqual (expectedManifest, File.ReadAllText (manifest));
		}

		[Test]
		public void MissingRewriteManifestUsesXA4327 ()
		{
			string path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			string mapping = Path.Combine (path, "mapping.txt");
			File.WriteAllText (mapping, "acme.orig.MyView -> a.b.C:\n");
			var errors = new List<BuildErrorEventArgs> ();
			var task = new GenerateProguardConfiguration {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				LinkedAssemblies = [],
				OutputFile = Path.Combine (path, "proguard.cfg"),
				R8MappingFile = mapping,
				R8RewriteManifestFile = Path.Combine (path, "missing-rewrite-manifest.txt"),
			};

			Assert.IsFalse (task.Execute (), "A missing rewrite manifest should fail the task.");
			Assert.That (errors, Has.Count.EqualTo (1));
			Assert.AreEqual ("XA4327", errors [0].Code);
		}
	}
}
