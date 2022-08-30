using System;
using System.Linq;
using Java.Interop.Tools.JavaTypeSystem.Models;
using NUnit.Framework;

namespace Java.Interop.Tools.JavaTypeSystem.Tests
{
	[TestFixture]
	public class JavaTypeCollectionTests
	{
		JavaTypeCollection api;
		
		[OneTimeSetUp]
		public void SetupFixture ()
		{
			api = JavaApiTestHelper.GetLoadedApi ();
			api.ResolveCollection ();
		}

		[Test]
		public void TestResolvedTypes ()
		{
			var type = api.FindType ("android.database.ContentObservable");
			Assert.IsNotNull (type, "type not found");

			var kls = type as JavaClassModel;
			Assert.IsNotNull (kls, "type was not class");
			Assert.IsNotNull (kls.BaseTypeReference, "extends not resolved.");
			Assert.IsNotNull (kls.BaseTypeReference.ReferencedType, "referenced type is not correctly resolved");
		}

		[Test]
		public void ResolveGenericArguments ()
		{
			var type = api.FindType ("java.util.concurrent.ConcurrentHashMap");
			Assert.IsNotNull (type, "type not found");

			var kls = type as JavaClassModel;
			Assert.IsNotNull (kls, "type was not class");

			var method = kls.Methods.OfType<JavaMethodModel> ().First (m => m.Name == "searchEntries");
			Assert.IsNotNull (method, "method not found.");

			var para = method.Parameters [1];
			Assert.AreEqual ("java.util.function.Function<java.util.Map.Entry<K, V>, ? extends U>",
			                 para.TypeModel.ToString (),
			                 "referenced type is not correctly resolved");
		}

		[Test]
		public void IntentServiceHack ()
		{
			// https://github.com/xamarin/java.interop/issues/717
			var api = JavaApiTestHelper.GetLoadedApi ();

			// Create "mono.android.app" package
			var mono_android_app = api.AddPackage ("mono.android.app", "mono/android/app");

			// Remove "android.app.IntentService" type
			var android_app = api.Packages["android.app"];
			var intent_service = android_app.Types.Single (t => t.Name == "IntentService");
			android_app.Types.Remove (intent_service);
			api.RemoveType (intent_service);

			// Create new "mono.android.app.IntentService" type
			var new_intent_service = JavaApiTestHelper.CreateClass (mono_android_app, "IntentService");

			api.AddType (new_intent_service);

			api.ResolveCollection ();

			// Ensure we can resolve the type by either name
			Assert.AreSame (new_intent_service, api.FindType ("mono.android.app.IntentService"));
			Assert.AreSame (new_intent_service, api.FindType ("android.app.IntentService"));
		}

		[Test]
		public void InheritedGenericTypeParameters ()
		{
			// Ensure we can resolve generic type parameters from parent types:
			// public class MyClass<T>
			// {
			//     public class MyNestedClass<U>
			//     {
			//       public void DoT (T value) { }
			//       public void DoU (U value) { }
			//     }
			// }
			var xml = @"
<api api-source='class-parse'>
   <package name='example' jni-name='example'>
      <class abstract='false' deprecated='not deprecated' jni-extends='Ljava/lang/Object;' extends='java.lang.Object' extends-generic-aware='java.lang.Object' final='false' name='MyClass' jni-signature='Lexample/MyClass;' source-file-name='MyClass.java' static='false' visibility='public'>
         <typeParameters>
            <typeParameter name='T' jni-classBound='Ljava/lang/Object;' classBound='java.lang.Object' interfaceBounds='' jni-interfaceBounds='' />
         </typeParameters>
         <constructor deprecated='not deprecated' final='false' name='MyClass' static='false' visibility='public' bridge='false' synthetic='false' jni-signature='()V' />
      </class>
      <class abstract='false' deprecated='not deprecated' jni-extends='Ljava/lang/Object;' extends='java.lang.Object' extends-generic-aware='java.lang.Object' final='false' name='MyClass.MyNestedClass' jni-signature='Lexample/MyClass$MyNestedClass;' source-file-name='MyClass.java' static='false' visibility='public'>
         <typeParameters>
            <typeParameter name='U' jni-classBound='Ljava/lang/Object;' classBound='java.lang.Object' interfaceBounds='' jni-interfaceBounds='' />
         </typeParameters>
         <constructor deprecated='not deprecated' final='false' name='MyClass.MyNestedClass' static='false' visibility='public' bridge='false' synthetic='false' jni-signature='(Lexample/MyClass;)V' />
         <method abstract='false' deprecated='not deprecated' final='false' name='DoT' native='false' return='void' jni-return='V' static='false' synchronized='false' visibility='public' bridge='false' synthetic='false' jni-signature='(Ljava/lang/Object;)V'>
            <parameter name='p0' type='T' jni-type='TT;' />
         </method>
         <method abstract='false' deprecated='not deprecated' final='false' name='DoU' native='false' return='void' jni-return='V' static='false' synchronized='false' visibility='public' bridge='false' synthetic='false' jni-signature='(Ljava/lang/Object;)V'>
            <parameter name='p0' type='U' jni-type='TU;' />
         </method>
      </class>
   </package>
</api>";

			var xapi = JavaApiTestHelper.GetLoadedApi ();
			JavaXmlApiImporter.ParseString (xml, xapi);

			var results = xapi.ResolveCollection ();

			var t = xapi.Packages ["example"].Types.First (_ => _.Name == "MyClass").NestedTypes.First (_ => _.Name == "MyNestedClass") as JavaClassModel;

			Assert.AreEqual (2, t.Methods.Count);

			Assert.IsNotNull (t.Methods.SingleOrDefault (m => m.Name == "DoT"), "Method with generic T not found");
			Assert.IsNotNull (t.Methods.SingleOrDefault (m => m.Name == "DoU"), "Method with generic U not found");
		}

		[Test]
		public void InvalidBaseTypeResolution ()
		{
			var api = new JavaTypeCollection ();

			// Create "my.ns" package
			var my_ns = api.AddPackage ("my.ns", "my/ns");

			// Create new "my.ns.MyObject" type with "my.ns.MyObject" as base type
			var jlo = JavaApiTestHelper.CreateClass (my_ns, "MyObject", javaBaseType: "my.ns.MyObject", javaBaseTypeGeneric: "my.ns.MyObject");

			api.AddType (jlo);

			// Run the resolver
			var results = api.ResolveCollection ();

			// Ensure we marked it as unresolvable
			Assert.AreEqual (1, results.Count);
			Assert.AreEqual (1, results [0].Unresolvables.Count);
			Assert.AreEqual ("The class '[Class] my.ns.MyObject' was removed because the base type 'my.ns.MyObject' is invalid.", results [0].Unresolvables [0].GetDisplayMessage ());

			// Ensure we removed the type from the collection
			Assert.AreEqual (0, api.TypesFlattened.Count);
		}
	}
}
