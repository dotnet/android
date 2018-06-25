using System;
using System.IO;
using System.Linq;
using System.Xml;
using NUnit.Framework;

namespace Xamarin.Android.Tools.ApiXmlAdjuster.Tests
{
	[TestFixture]
	public class OverrideMarkerTest
	{
		JavaApi api;
		
		[TestFixtureSetUp]
		public void SetupFixture ()
		{
			api = JavaApiTestHelper.GetLoadedApi ();
			api.Resolve ();
			api.CreateGenericInheritanceMapping ();
			api.MarkOverrides ();
		}
		
		[Test]
		public void InstantiatedGenericArgumentName ()
		{
			var kls = api.FindNonGenericType ("android.database.ContentObservable") as JavaClass;
			var method = kls.Members.OfType<JavaMethod> ().First (m => m.Name == "registerObserver");
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
			using (var xr = XmlReader.Create (new StringReader (xml)))
				xapi.Load (xr, false);
			xapi.Resolve ();
			xapi.CreateGenericInheritanceMapping ();
			xapi.MarkOverrides ();
			var t = xapi.Packages.First (_ => _.Name == "XXX").Types.First (_ => _.Name == "SherlockExpandableListActivity");
			var m = t.Members.OfType<JavaMethod> ().First (_ => _.Name == "addContentView");
			Assert.IsNotNull (m.BaseMethod, "base method not found");
		}

		[Test]
		public void GenericConstructors ()
		{
			string xml = @"<api>
  <package name='XXX'>
    <class abstract='true' deprecated='not deprecated' final='false' name='GenericConstructors' static='false' visibility='public' jni-signature='Landroid/app/GenericConstructors'>
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
			var xapi = new JavaApi ();
			using (var xr = XmlReader.Create (new StringReader (xml)))
				xapi.Load (xr, false);
			xapi.StripNonBindables ();
			xapi.Resolve ();
			xapi.CreateGenericInheritanceMapping ();
			xapi.MarkOverrides ();
			xapi.FindDefects ();
			var sw = new StringWriter ();
			using (var xw = XmlWriter.Create (sw))
				xapi.Save (xw);
			xapi = new JavaApi ();
			using (var xr = XmlReader.Create (new StringReader (sw.ToString ())))
				xapi.Load (xr, true);
			var t = xapi.Packages.First (_ => _.Name == "XXX").Types.First (_ => _.Name == "GenericConstructors");
			var m = t.Members.OfType<JavaConstructor> ().FirstOrDefault ();
			Assert.IsNotNull (m.TypeParameters, "constructor not found");
		}
	}
}

