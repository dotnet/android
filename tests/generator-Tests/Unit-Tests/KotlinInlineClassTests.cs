using System.Linq;
using System.Xml.Linq;
using Java.Interop.Tools.Generator;
using MonoDroid.Generation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class KotlinInlineClassTests
	{
		CodeGenerationOptions opt = new CodeGenerationOptions ();

		[TestMethodAttribute]
		public void CreateClass_ReadsKotlinInlineClassAttributes ()
		{
			var xml = XDocument.Parse (
				"<package name='com.example' jni-name='com/example'>" +
				"<class name='MyColor' kotlin-inline-class='true' kotlin-inline-class-underlying-jni-type='J' />" +
				"</package>");

			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), opt);

			Assert.IsTrue (klass.IsKotlinInlineClass);
			Assert.AreEqual ("J", klass.KotlinInlineClassUnderlyingJniType);
		}

		[TestMethodAttribute]
		public void CreateClass_DefaultsForNonInlineClass ()
		{
			var xml = XDocument.Parse (
				"<package name='com.example' jni-name='com/example'>" +
				"<class name='Plain' />" +
				"</package>");

			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), opt);

			Assert.IsFalse (klass.IsKotlinInlineClass);
		}

		[TestMethodAttribute]
		public void CreateParameter_ReadsKotlinInlineClassJniType ()
		{
			var xml = XDocument.Parse (
				"<parameter name='c' type='long' jni-type='J' kotlin-inline-class-jni-type='Lcom/example/MyColor;' />");

			var p = XmlApiImporter.CreateParameter (xml.Root, opt);

			Assert.AreEqual ("Lcom/example/MyColor;", p.KotlinInlineClassJniType);
		}

		[TestMethodAttribute]
		public void CreateMethod_ReadsKotlinInlineClassReturnJniType ()
		{
			var xml = XDocument.Parse (
				"<package name='com.example' jni-name='com/example'>" +
				"<class name='Widgets'>" +
				"<method name='pad' return='long' jni-return='J' static='true' final='false' " +
				"kotlin-inline-class-return-jni-type='Lcom/example/MyDp;' />" +
				"</class></package>");

			var pkg = xml.Root;
			var classElem = pkg.Element ("class");
			var klass = XmlApiImporter.CreateClass (pkg, classElem, opt);
			var method = klass.Methods.First ();

			Assert.AreEqual ("Lcom/example/MyDp;", method.KotlinInlineClassReturnJniType);
		}

		[TestMethodAttribute]
		public void Parameter_Clone_PreservesKotlinInlineClassJniType ()
		{
			var xml = XDocument.Parse (
				"<parameter name='c' type='long' jni-type='J' kotlin-inline-class-jni-type='Lcom/example/MyColor;' />");

			var p = XmlApiImporter.CreateParameter (xml.Root, opt);
			var clone = p.Clone ();

			Assert.AreEqual ("Lcom/example/MyColor;", clone.KotlinInlineClassJniType);
		}

		[TestMethodAttribute]
		public void TypeNameUtilities_JniSignatureToJavaTypeName_HandlesReferenceTypes ()
		{
			Assert.AreEqual ("com.example.MyColor",
				TypeNameUtilities.JniSignatureToJavaTypeName ("Lcom/example/MyColor;"));
			Assert.AreEqual ("java.lang.String",
				TypeNameUtilities.JniSignatureToJavaTypeName ("Ljava/lang/String;"));
		}

		[TestMethodAttribute]
		public void TypeNameUtilities_JniSignatureToJavaTypeName_RejectsPrimitivesAndArrays ()
		{
			Assert.IsNull (TypeNameUtilities.JniSignatureToJavaTypeName ("J"));
			Assert.IsNull (TypeNameUtilities.JniSignatureToJavaTypeName (""));
			Assert.IsNull (TypeNameUtilities.JniSignatureToJavaTypeName (null));
			// Array types intentionally not supported by inline-class projection.
			Assert.IsNull (TypeNameUtilities.JniSignatureToJavaTypeName ("[Lcom/example/MyColor;"));
		}
	}
}
