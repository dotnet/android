using System;
using System.Xml.Linq;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class XmlApiImporterTests
	{
		[Test]
		public void CreateClass_EnsureValidName ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><class name=\"$3\" /></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"));

			Assert.AreEqual ("_3", klass.Name);
		}

		[Test]
		public void CreateCtor_EnsureValidName ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><class name=\"test\"><constructor name=\"$3\" /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"));

			Assert.AreEqual ("_3", klass.Ctors[0].Name);
		}

		[Test]
		public void CreateField_StudlyCaseName ()
		{
			var xml = XDocument.Parse ("<field name=\"_DES_EDE_CBC\" />");
			var field = XmlApiImporter.CreateField (xml.Root);

			Assert.AreEqual ("DesEdeCbc", field.Name);
		}

		[Test]
		public void CreateField_EnsureValidName ()
		{
			var xml = XDocument.Parse ("<field name=\"_3DES_EDE_CBC\" />");
			var field = XmlApiImporter.CreateField (xml.Root);

			Assert.AreEqual ("_3desEdeCbc", field.Name);
		}

		[Test]
		public void CreateField_HandleDollarSign ()
		{
			var xml = XDocument.Parse ("<field name=\"A$3\" />");
			var field = XmlApiImporter.CreateField (xml.Root);

			Assert.AreEqual ("A_3", field.Name);
		}

		[Test]
		public void CreateField_HandleDollarSignNumber ()
		{
			var xml = XDocument.Parse ("<field name=\"$3\" />");
			var field = XmlApiImporter.CreateField (xml.Root);

			Assert.AreEqual ("_3", field.Name);
		}

		[Test]
		public void CreateInterface_EnsureValidName ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><interface name=\"$3\" /></package>");
			var iface = XmlApiImporter.CreateInterface (xml.Root, xml.Root.Element ("interface"));

			Assert.AreEqual ("I_3", iface.Name);
		}

		[Test]
		public void CreateMethod_EnsureValidName ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><class name=\"test\"><method name=\"$3\" /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"));

			Assert.AreEqual ("_3", klass.Methods [0].Name);
		}

		[Test]
		public void CreateMethod_EnsureValidNameHyphen ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><class name=\"test\"><method name=\"-3\" /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"));

			Assert.AreEqual ("_3", klass.Methods [0].Name);
		}

		[Test]
		public void CreateMethod_EnsureKotlinImplFix ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><class name=\"test\"><method name=\"add-impl\" /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"));

			Assert.AreEqual ("Add", klass.Methods [0].Name);
		}

		[Test]
		public void CreateMethod_EnsureKotlinHashcodeFix ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><class name=\"test\"><method name=\"add-h4F1V8i\" /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"));

			Assert.AreEqual ("Add", klass.Methods [0].Name);
		}

		[Test]
		public void CreateParameter_EnsureValidName ()
		{
			var xml = XDocument.Parse ("<parameter name=\"$3\" />");
			var p = XmlApiImporter.CreateParameter (xml.Root);

			Assert.AreEqual ("_3", p.Name);
		}
	}
}
