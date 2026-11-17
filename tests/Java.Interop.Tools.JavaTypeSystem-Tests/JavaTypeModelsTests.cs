using System;
using System.IO;
using System.Linq;
using System.Xml;
using Java.Interop.Tools.JavaTypeSystem.Models;
using NUnit.Framework;

namespace Java.Interop.Tools.JavaTypeSystem.Tests
{
	[TestFixture]
	public class JavaTypeModelsTests
	{
		JavaTypeCollection api;
		
		[OneTimeSetUp]
		public void SetupFixture ()
		{
			api = JavaApiTestHelper.GetLoadedApi ();
		}
		
		[Test]
		public void TestToString ()
		{
			var pkg = api.Packages["android.database"];
			Assert.AreEqual ("[Package] android.database", pkg.ToString ());

			var kls = pkg.Types.First (t => t.FullName == "android.database.ContentObservable");
			Assert.AreEqual ("[Class] android.database.ContentObservable", kls.ToString ());
		}

		[Test]
		public void AnnotatedVisibility ()
		{
			// Ensure the 'annotated-visibility' attribute gets passed through
			using var sw = new StringWriter ();

			using (var xml = XmlWriter.Create (sw))
				JavaXmlApiExporter.Save (api, xml);

			var doc = new XmlDocument { XmlResolver = null };
			using var sreader = new StringReader (sw.ToString ());
			using var reader = XmlReader.Create (sreader, new XmlReaderSettings { XmlResolver = null });
			doc.Load (reader);

			var annotated_class = doc.SelectSingleNode ("/api/package/class[@name='StateListAnimator']");
			Assert.AreEqual ("TESTS", annotated_class.Attributes ["annotated-visibility"].InnerText);

			var annotated_ctor = annotated_class ["constructor"];
			Assert.AreEqual ("TESTS", annotated_ctor.Attributes ["annotated-visibility"].InnerText);

			var annotated_method = annotated_class ["method"];
			Assert.AreEqual ("TESTS", annotated_method.Attributes ["annotated-visibility"].InnerText);

			var annotated_interface = doc.SelectSingleNode ("/api/package/interface[@name='DrmStore.ConstraintsColumns']");
			Assert.AreEqual ("TESTS", annotated_interface.Attributes ["annotated-visibility"].InnerText);

			var annotated_field = annotated_interface ["field"];
			Assert.AreEqual ("TESTS", annotated_field.Attributes ["annotated-visibility"].InnerText);
		}

		[Test]
		public void KotlinValueClassAttributes ()
		{
			var xml = """
				<api>
				  <package name='com.example' jni-name='com/example'>
				    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='true' name='MyColor' jni-signature='Lcom/example/MyColor;' static='false' visibility='public' kotlin-inline-class='true' kotlin-inline-class-underlying-jni-type='J' />
				    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='true' name='Widgets' jni-signature='Lcom/example/Widgets;' static='false' visibility='public'>
				      <method abstract='false' deprecated='not deprecated' final='true' name='tint-abc' jni-signature='(J)J' native='false' return='long' jni-return='J' static='false' synchronized='false' visibility='public' managedName='Tint' kotlin-inline-class-return-jni-type='Lcom/example/MyColor;'>
				        <parameter name='color' type='long' jni-type='J' kotlin-inline-class-jni-type='Lcom/example/MyColor;' />
				      </method>
				    </class>
				  </package>
				</api>
				""";
			var xapi = JavaApiTestHelper.GetLoadedApi ();
			JavaXmlApiImporter.ParseString (xml, xapi);
			xapi.ResolveCollection ();

			using var sw = new StringWriter ();
			using (var xw = XmlWriter.Create (sw))
				JavaXmlApiExporter.Save (xapi, xw);
			var output = sw.ToString ();

			StringAssert.Contains ("kotlin-inline-class=\"true\"", output);
			StringAssert.Contains ("kotlin-inline-class-underlying-jni-type=\"J\"", output);
			StringAssert.Contains ("managedName=\"Tint\"", output);
			StringAssert.Contains ("kotlin-inline-class-return-jni-type=\"Lcom/example/MyColor;\"", output);
			StringAssert.Contains ("kotlin-inline-class-jni-type=\"Lcom/example/MyColor;\"", output);
		}
	}
}
