using System;
using System.Xml.Linq;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class XmlApiImporterTests
	{
		CodeGenerationOptions opt = new CodeGenerationOptions ();

		[Test]
		public void CreateClass_EnsureValidName ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><class name=\"$3\" /></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), opt);

			Assert.AreEqual ("_3", klass.Name);
		}

		[Test]
		public void CreateClass_CorrectApiSince ()
		{
			var xml = XDocument.Parse ("<package name='com.example.test' jni-name='com/example/test'><class name='myclass' api-since='7' /></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), opt);

			Assert.AreEqual (7, klass.ApiAvailableSince);
		}

		[Test]
		public void CreateClass_CorrectApiSinceFromPackage ()
		{
			// Make sure we inherit it from <package>.
			var xml = XDocument.Parse ("<package name='com.example.test' jni-name='com/example/test' api-since='7'><class name='myclass' /></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), opt);

			Assert.AreEqual (7, klass.ApiAvailableSince);
		}

		[Test]
		public void CreateClass_CorrectApiSinceOverridePackage ()
		{
			// Make sure we inherit it from <package>.
			var xml = XDocument.Parse ("<package name='com.example.test' jni-name='com/example/test' api-since='7'><class name='myclass' api-since='9' /></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), opt);

			Assert.AreEqual (9, klass.ApiAvailableSince);
		}

		[Test]
		public void CreateCtor_EnsureValidName ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><class name=\"test\"><constructor name=\"$3\" /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), opt);

			Assert.AreEqual ("_3", klass.Ctors[0].Name);
		}

		[Test]
		public void CreateCtor_CorrectApiSince ()
		{
			var xml = XDocument.Parse ("<package name='com.example.test' jni-name='com/example/test'><class name='test'><constructor name='ctor' api-since='7' /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), opt);

			Assert.AreEqual (7, klass.Ctors [0].ApiAvailableSince);
		}

		[Test]
		public void CreateCtor_CorrectApiSinceFromClass ()
		{
			var xml = XDocument.Parse ("<package name='com.example.test' jni-name='com/example/test'><class name='test' api-since='7'><constructor name='ctor' /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), opt);

			Assert.AreEqual (7, klass.Ctors [0].ApiAvailableSince);
		}

		[Test]
		public void CreateField_StudlyCaseName ()
		{
			var klass = new TestClass ("object", "MyNamespace.MyType");
			var xml = XDocument.Parse ("<field name=\"_DES_EDE_CBC\" />");
			var field = XmlApiImporter.CreateField (klass, xml.Root);

			Assert.AreEqual ("DesEdeCbc", field.Name);
		}

		[Test]
		public void CreateField_EnsureValidName ()
		{
			var klass = new TestClass ("object", "MyNamespace.MyType");
			var xml = XDocument.Parse ("<field name=\"_3DES_EDE_CBC\" />");
			var field = XmlApiImporter.CreateField (klass, xml.Root);

			Assert.AreEqual ("_3desEdeCbc", field.Name);
		}

		[Test]
		public void CreateField_HandleDollarSign ()
		{
			var klass = new TestClass ("object", "MyNamespace.MyType");
			var xml = XDocument.Parse ("<field name=\"A$3\" />");
			var field = XmlApiImporter.CreateField (klass, xml.Root);

			Assert.AreEqual ("A_3", field.Name);
		}

		[Test]
		public void CreateField_HandleDollarSignNumber ()
		{
			var klass = new TestClass ("object", "MyNamespace.MyType");
			var xml = XDocument.Parse ("<field name=\"$3\" />");
			var field = XmlApiImporter.CreateField (klass, xml.Root);

			Assert.AreEqual ("_3", field.Name);
		}

		[Test]
		public void CreateField_CorrectApiVersion ()
		{
			var klass = new TestClass ("object", "MyNamespace.MyType");
			var xml = XDocument.Parse ("<field name='$3' api-since='7' />");
			var field = XmlApiImporter.CreateField (klass, xml.Root);

			Assert.AreEqual (7, field.ApiAvailableSince);
		}

		[Test]
		public void CreateField_CorrectApiVersionFromClass ()
		{
			var klass = new TestClass ("object", "MyNamespace.MyType") { ApiAvailableSince = 7 };
			var xml = XDocument.Parse ("<field name='$3' />");
			var field = XmlApiImporter.CreateField (klass, xml.Root);

			Assert.AreEqual (7, field.ApiAvailableSince);
		}

		[Test]
		public void CreateInterface_EnsureValidName ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><interface name=\"$3\" /></package>");
			var iface = XmlApiImporter.CreateInterface (xml.Root, xml.Root.Element ("interface"), opt);

			Assert.AreEqual ("I_3", iface.Name);
		}

		[Test]
		public void CreateInterface_CorrectApiSince ()
		{
			var xml = XDocument.Parse ("<package name='com.example.test' jni-name='com/example/test'><interface name='myclass' api-since='7' /></package>");
			var iface = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("interface"), opt);

			Assert.AreEqual (7, iface.ApiAvailableSince);
		}

		[Test]
		public void CreateInterface_CorrectApiSinceFromPackage ()
		{
			// Make sure we inherit it from <package>.
			var xml = XDocument.Parse ("<package name='com.example.test' jni-name='com/example/test' api-since='7'><interface name='myclass' /></package>");
			var iface = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("interface"), opt);

			Assert.AreEqual (7, iface.ApiAvailableSince);
		}

		[Test]
		public void CreateInterface_CorrectApiSinceOverridePackage ()
		{
			// Make sure we inherit it from <package>.
			var xml = XDocument.Parse ("<package name='com.example.test' jni-name='com/example/test' api-since='7'><interface name='myclass' api-since='9' /></package>");
			var iface = XmlApiImporter.CreateInterface (xml.Root, xml.Root.Element ("interface"), opt);

			Assert.AreEqual (9, iface.ApiAvailableSince);
		}

		[Test]
		public void CreateMethod_EnsureValidName ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><class name=\"test\"><method name=\"$3\" /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), opt);

			Assert.AreEqual ("_3", klass.Methods [0].Name);
		}

		[Test]
		public void CreateMethod_EnsureValidNameHyphen ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><class name=\"test\"><method name=\"-3\" /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), opt);

			Assert.AreEqual ("_3", klass.Methods [0].Name);
		}

		[Test]
		public void CreateMethod_CorrectApiSince ()
		{
			var xml = XDocument.Parse ("<package name='com.example.test' jni-name='com/example/test'><class name='test'><method name='-3' api-since='7' /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), opt);

			Assert.AreEqual (7, klass.Methods [0].ApiAvailableSince);
		}

		[Test]
		public void CreateMethod_CorrectApiSinceFromClass ()
		{
			var xml = XDocument.Parse ("<package name='com.example.test' jni-name='com/example/test'><class name='test' api-since='7'><method name='-3' /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), opt);

			Assert.AreEqual (7, klass.Methods [0].ApiAvailableSince);
		}

		[Test]
		public void CreateParameter_EnsureValidName ()
		{
			var xml = XDocument.Parse ("<parameter name=\"$3\" />");
			var p = XmlApiImporter.CreateParameter (xml.Root);

			Assert.AreEqual ("_3", p.Name);
		}

		[Test]
		public void CreateParameter_NotNull ()
		{
			var xml = XDocument.Parse ("<parameter name=\"sender\" not-null=\"true\" />");
			var p = XmlApiImporter.CreateParameter (xml.Root);

			Assert.True (p.NotNull);
		}
	}
}
