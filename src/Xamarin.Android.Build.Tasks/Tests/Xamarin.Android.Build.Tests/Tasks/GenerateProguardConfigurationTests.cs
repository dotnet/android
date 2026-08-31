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
			fixture.Metadata.AddAssemblyReference (
				fixture.Metadata.GetOrAddString ("Mono.Android"),
				new Version (1, 0, 0, 0),
				default,
				default,
				default,
				default);

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

			fixture.Metadata.AddCustomAttribute (type, fixture.RegisterCtor1, fixture.AttributeBlob ("a/b/C"));
			fixture.Metadata.AddCustomAttribute (constructor, fixture.RegisterCtor3, fixture.AttributeBlob (".ctor", "()V", ""));
			fixture.Metadata.AddCustomAttribute (firstOverload, fixture.RegisterCtor3, fixture.AttributeBlob ("a", "(La/b/D;)V", ""));
			fixture.Metadata.AddCustomAttribute (secondOverload, fixture.RegisterCtor3, fixture.AttributeBlob ("b", "(La/b/D;I)V", ""));
			fixture.Metadata.AddCustomAttribute (field, fixture.RegisterCtor1, fixture.AttributeBlob ("c"));
			fixture.Metadata.AddCustomAttribute (property, fixture.RegisterCtor1, fixture.AttributeBlob ("d"));
			fixture.Metadata.AddCustomAttribute (@event, fixture.RegisterCtor1, fixture.AttributeBlob ("e"));
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
				# ACW for Fixture
				-keep,allowobfuscation class acme.orig.MyView
				-keepclassmembers,allowobfuscation class acme.orig.MyView {
				   <init>(...);
				   *** onClick(...);
				   *** onClick(...);
				   *** count;
				   *** enabled;
				   *** listener;
				}


				""", File.ReadAllText (proguard));
			Assert.AreEqual ("""
				C	acme/orig/MyView
				F	acme/orig/MyView	count
				F	acme/orig/MyView	enabled
				F	acme/orig/MyView	listener
				M	acme/orig/MyView	<init>()
				M	acme/orig/MyView	onClick(android.view.View)
				M	acme/orig/MyView	onClick(android.view.View,int)

				""", File.ReadAllText (manifest));
		}
	}
}
