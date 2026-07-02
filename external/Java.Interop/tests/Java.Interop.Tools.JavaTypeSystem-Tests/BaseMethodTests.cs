using System;
using System.Linq;
using System.Text;
using System.Xml;
using Java.Interop.Tools.JavaTypeSystem.Models;
using NUnit.Framework;

namespace Java.Interop.Tools.JavaTypeSystem.Tests
{
	[TestFixture]
	public class BaseMethodTests
	{
		JavaTypeCollection api;

		[OneTimeSetUp]
		public void SetupFixture ()
		{
			api = JavaApiTestHelper.GetLoadedApi ();
			api.ResolveCollection ();
		}

		[Test]
		public void InstantiatedGenericArgumentName ()
		{
			var kls = api.FindType ("android.database.ContentObservable") as JavaClassModel;
			var method = kls.Methods.First (m => m.Name == "registerObserver");
			Assert.IsNotNull (method, "registerObserver() not found.");

			var para = method.Parameters.FirstOrDefault ();
			Assert.IsNotNull (para, "Expected parameter, not found.");
			Assert.AreEqual (method.Parameters.First (), method.Parameters.Last (), "There should be only one parameter.");
			Assert.AreEqual ("T", para.InstantiatedGenericArgumentName, "InstantiatedGenericArgumentName mismatch");
		}

		[Test]
		public void AncestralOverrides ()
		{
			string xml = @"<api>
  <package name='XXX'>
    <class abstract='true' deprecated='not deprecated' extends='android.app.ExpandableListActivity' extends-generic-aware='android.app.ExpandableListActivity' final='false' name='SherlockExpandableListActivity' static='false' visibility='public' jni-signature='Landroid/app/ExpandableListActivity;'>
      <method abstract='false' deprecated='not deprecated' final='false' name='addContentView' native='false' return='void' static='false' synchronized='false' visibility='public' jni-signature='(Landroid/view/View;Landroid/view/ViewGroup$LayoutParams;)V'>
        <parameter name = 'view' type='android.view.View' jni-type='Landroid/view/View;'>
        </parameter>
        <parameter name = 'params' type='android.view.ViewGroup.LayoutParams' jni-type='Landroid/view/ViewGroup$LayoutParams;'>
        </parameter>
      </method>
    </class>
  </package>
</api>";

			var xapi = JavaApiTestHelper.GetLoadedApi ();
			JavaXmlApiImporter.ParseString (xml, xapi);

			xapi.ResolveCollection ();

			var t = xapi.Packages ["XXX"].Types.First (_ => _.Name == "SherlockExpandableListActivity");
			var m = t.Methods.First (_ => _.Name == "addContentView");

			Assert.IsNotNull (m.BaseMethod, "base method not found");
		}

		[Test]
		public void GenericConstructors ()
		{
			string xml = @"<api>
		  <package name='XXX'>
		    <class abstract='true' deprecated='not deprecated' final='false' name='GenericConstructors' static='false' visibility='public' jni-signature='Landroid/app/GenericConstructors' extends='java.lang.Object' extends-generic-aware='java.lang.Object'>
		      <constructor deprecated='not deprecated' final='false' name='GenericConstructors' static='false' visibility='public' jni-signature='(LTTE;)V'>
		        <typeParameters>
		          <typeParameter name='E' interfaceBounds='' jni-interfaceBounds='' />
		        </typeParameters>
		        <parameter name = 'e' type='E' jni-type='TE;'>
		        </parameter>
		      </constructor>
		    </class>
		  </package>
		</api>";

			var xapi = JavaApiTestHelper.GetLoadedApi ();
			JavaXmlApiImporter.ParseString (xml, xapi);

			var results = xapi.ResolveCollection ();

			var t = xapi.Packages ["XXX"].Types.First (_ => _.Name == "GenericConstructors") as JavaClassModel;
			var m = t.Constructors.FirstOrDefault ();
			Assert.IsNotNull (m.TypeParameters, "constructor not found");
		}

		[Test]
		public void PreferRealApi ()
		{
			// Our base method is abstract, and our derived method is not.  However their is also a "matching" non-abstract
			// base method that is "synthetic, bridge".  If this method is chosen as the base then we will not write the derived
			// method because it is not "different" from the base.  (They are both not-abstract.)  In reality, we need to
			// match to the "not-synthetic, not-bridge" method that *is* abstract, so that the derived method is different
			// enough to get written to the output.
			// See "JavaXmlApiExporter.SaveMethod ()" for what constitutes "different" enough to be written.
			string xml = @"<api>
  <package name='com.google.crypto.tink.streamingaead' jni-name='com/google/crypto/tink/streamingaead'>
  
    <class abstract='true' deprecated='not deprecated' jni-extends='Ljava/lang/Object;' extends='java.lang.Object' extends-generic-aware='java.lang.Object' final='false' name='StreamingAeadKey' jni-signature='Lcom/google/crypto/tink/streamingaead/StreamingAeadKey;' source-file-name='StreamingAeadKey.java' static='false' visibility='public'>
      <method abstract='false' deprecated='not deprecated' final='false' name='getParameters' native='false' return='java.lang.Object' jni-return='Ljava/lang/Object;' static='false' synchronized='false' visibility='public' bridge='true' synthetic='true' jni-signature='()Ljava/lang/Object;' />
      <method abstract='true' deprecated='not deprecated' final='false' name='getParameters' native='false' return='java.lang.Object' jni-return='Ljava/lang/Object;' static='false' synchronized='false' visibility='public' bridge='false' synthetic='false' jni-signature='()Ljava/lang/Object;' />
    </class>
    
    <class abstract='false' deprecated='not deprecated' jni-extends='Lcom/google/crypto/tink/streamingaead/StreamingAeadKey;' extends='com.google.crypto.tink.streamingaead.StreamingAeadKey' extends-generic-aware='com.google.crypto.tink.streamingaead.StreamingAeadKey' final='true' name='AesGcmHkdfStreamingKey' jni-signature='Lcom/google/crypto/tink/streamingaead/AesGcmHkdfStreamingKey;' source-file-name='AesGcmHkdfStreamingKey.java' static='false' visibility='public'>
      <method abstract='false' deprecated='not deprecated' final='false' name='getParameters' native='false' return='java.lang.Object' jni-return='Ljava/lang/Object;' static='false' synchronized='false' visibility='public' bridge='false' synthetic='false' jni-signature='()Ljava/lang/Object;' />
    </class>
  
  </package>
</api>";

			var xapi = JavaApiTestHelper.GetLoadedApi ();
			JavaXmlApiImporter.ParseString (xml, xapi);

			var results = xapi.ResolveCollection ();

			var t = xapi.Packages ["com.google.crypto.tink.streamingaead"].Types.First (_ => _.Name == "AesGcmHkdfStreamingKey") as JavaClassModel;
			var m = t.Methods.FirstOrDefault ();

			// The non-synthetic, non-bridge, abstract base method should be chosen
			Assert.IsFalse (m.BaseMethod.IsSynthetic);
			Assert.IsFalse (m.BaseMethod.IsBridge);
			Assert.IsTrue (m.BaseMethod.IsAbstract);

			var sb = new StringBuilder ();

			// Write the results out to XML
			using (var xw = XmlWriter.Create (sb))
				JavaXmlApiExporter.Save (xapi, xw);

			// Read results back in to ensure AesGcmHkdfStreamingKey.getParameters was output
			var new_collection = JavaXmlApiImporter.ParseString (sb.ToString ());

			var t2 = new_collection.Packages ["com.google.crypto.tink.streamingaead"].Types.First (_ => _.Name == "AesGcmHkdfStreamingKey") as JavaClassModel;
			var m2 = t.Methods.FirstOrDefault ();

			Assert.IsNotNull (m2);
		}
	}
}
