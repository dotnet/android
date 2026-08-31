using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class GenerateProguardConfigurationTests : BaseTest
	{
		[Test]
		public void WritesOriginalKeepRulesAndReachabilityManifest ()
		{
			string path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			string assembly = Path.Combine (path, "Linked.dll");
			string mapping = Path.Combine (path, "mapping.txt");
			string proguard = Path.Combine (path, "proguard.cfg");
			string manifest = Path.Combine (path, "reachability.txt");

			var fixture = new JniFixtureBuilder ();
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
			fixture.AddVoidMethod ("DirectLookup", fixture.EmitLoadStringBody (
				fixture.String ("a/b/E"),
				fixture.String ("x"),
				fixture.String ("()V")));

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

				""");

			var task = new GenerateProguardConfiguration {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				LinkedAssemblies = new [] { new TaskItem (assembly) },
				OutputFile = proguard,
				R8MappingFile = mapping,
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
			Assert.AreEqual ("""
				C	acme/orig/DirectTarget
				C	acme/orig/ICallback
				C	acme/orig/MyView
				C	android/view/View
				F	acme/orig/MyView	count
				F	acme/orig/MyView	enabled
				F	acme/orig/MyView	listener
				M	acme/orig/DirectTarget	run()
				M	acme/orig/ICallback	invoke()
				M	acme/orig/MyView	<init>()
				M	acme/orig/MyView	onClick(android.view.View)
				M	acme/orig/MyView	onClick(android.view.View,int)

				""".ReplaceLineEndings (), File.ReadAllText (manifest));
		}
	}
}
