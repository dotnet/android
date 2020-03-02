using System;
using System.Linq;
using System.Xml.Linq;
using Java.Interop.Tools.Generator.Transformation;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class KotlinFixupsTests
	{
		[Test]
		public void CreateMethod_EnsureKotlinImplFix ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><class name=\"test\"><method name=\"add-impl\" final=\"false\" /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), new CodeGenerationOptions ());

			KotlinFixups.Fixup (new [] { (GenBase)klass }.ToList ());

			Assert.AreEqual ("Add", klass.Methods [0].Name);
			Assert.IsTrue (klass.Methods [0].IsFinal);
			Assert.IsFalse (klass.Methods [0].IsVirtual);
		}

		[Test]
		public void CreateMethod_EnsureKotlinHashcodeFix ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><class name=\"test\"><method name=\"add-h-_1V8i\" final=\"false\" /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), new CodeGenerationOptions ());

			KotlinFixups.Fixup (new [] { (GenBase) klass }.ToList ());

			Assert.AreEqual ("Add", klass.Methods [0].Name);
			Assert.IsTrue (klass.Methods [0].IsFinal);
			Assert.IsFalse (klass.Methods [0].IsVirtual);
		}
	}
}
