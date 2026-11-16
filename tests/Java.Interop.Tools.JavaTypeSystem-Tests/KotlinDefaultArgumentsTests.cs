using System;
using System.Text;
using System.Xml;
using NUnit.Framework;

namespace Java.Interop.Tools.JavaTypeSystem.Tests
{
	[TestFixture]
	public class KotlinDefaultArgumentsTests
	{
		[Test]
		public void AttributesSurviveRoundTrip ()
		{
			var xml = @"<api>
  <package name='com.example' jni-name='com/example'>
    <class abstract='false' deprecated='not deprecated' jni-extends='Ljava/lang/Object;' extends='java.lang.Object' extends-generic-aware='java.lang.Object' final='true' name='Foo' jni-signature='Lcom/example/Foo;' static='false' visibility='public'>
      <constructor deprecated='not deprecated' final='false' name='Foo' static='false' visibility='public' bridge='false' synthetic='false' jni-signature='(I)V' kotlin-default-jni-signature='(IILkotlin/jvm/internal/DefaultConstructorMarker;)V' kotlin-default-parameters='0'>
        <parameter name='a' type='int' jni-type='I' />
      </constructor>
      <method abstract='false' deprecated='not deprecated' final='true' name='bar' native='false' return='void' jni-return='V' static='false' synchronized='false' visibility='public' bridge='false' synthetic='false' jni-signature='(I)V' kotlin-default-jni-signature='(Lcom/example/Foo;IILjava/lang/Object;)V' kotlin-default-owner='com/example/Foo' kotlin-default-parameters='0'>
        <parameter name='a' type='int' jni-type='I' />
      </method>
    </class>
  </package>
</api>";

			var xapi = JavaApiTestHelper.GetLoadedApi ();
			JavaXmlApiImporter.ParseString (xml, xapi);
			xapi.ResolveCollection ();

			var sb = new StringBuilder ();
			using (var xw = XmlWriter.Create (sb))
				JavaXmlApiExporter.Save (xapi, xw);
			var output = sb.ToString ();

			StringAssert.Contains ("kotlin-default-jni-signature=\"(IILkotlin/jvm/internal/DefaultConstructorMarker;)V\"", output);
			StringAssert.Contains ("kotlin-default-jni-signature=\"(Lcom/example/Foo;IILjava/lang/Object;)V\"", output);
			StringAssert.Contains ("kotlin-default-owner=\"com/example/Foo\"", output);
			StringAssert.Contains ("kotlin-default-parameters=\"0\"", output);
		}
	}
}
