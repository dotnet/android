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
	}
}
