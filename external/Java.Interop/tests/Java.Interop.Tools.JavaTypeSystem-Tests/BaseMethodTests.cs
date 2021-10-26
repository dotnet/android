using System;
using System.Linq;
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
	}
}
