using MonoDroid.Generation;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace generatortests
{
	[TestFixture]
	public class XmlTests
	{
		XDocument xml;
		XElement package;
		List<XmlClassGen> javaLangClasses;

		[SetUp]
		public void SetUp ()
		{
			using (var reader = new StringReader (@"<api>
	<package name=""java.lang"">
		<class abstract=""false"" deprecated=""not deprecated"" final=""false"" name=""Object"" static=""false"" visibility=""public"">
		</class>
		<class abstract=""false"" deprecated=""not deprecated"" extends=""java.lang.Object"" extends-generic-aware=""java.lang.Object""
			final=""true"" name=""String"" static=""false"" visibility=""public"">
		</class>
	</package>
	<package name=""com.mypackage"">
		<class abstract=""false"" deprecated=""not deprecated"" final=""false"" name=""foo"" static=""false"" visibility=""public"">
			<constructor deprecated=""not deprecated"" final=""false"" name=""foo"" static=""false"" visibility=""public"" />
			<method abstract=""false"" deprecated=""not deprecated"" final=""false"" name=""bar"" native=""false"" return=""void"" static=""false"" synchronized=""false"" visibility=""public"" managedReturn=""System.Void"" />
			<method abstract=""false"" deprecated=""not deprecated"" final=""false"" name=""barWithParams"" native=""false"" return=""java.lang.String"" static=""false"" synchronized=""false"" visibility=""public"" managedReturn=""System.String"">
				<parameter name=""a"" type=""boolean"" />
				<parameter name=""b"" type=""int"" />
				<parameter name=""c"" type=""double"" />
			</method>
			<method abstract=""false"" deprecated=""not deprecated"" final=""false"" name=""unknownTypes"" native=""false"" return=""void"" static=""false"" synchronized=""false"" visibility=""public"" managedReturn=""System.Void"">
				<parameter name=""unknown"" type=""my.package.Unknown"" />
			</method>
			<method abstract=""false"" deprecated=""not deprecated"" final=""false"" name=""unknownTypesReturn"" native=""false"" return=""my.package.Unknown"" static=""false"" synchronized=""false"" visibility=""public"" managedReturn=""System.Object"">
				<parameter name=""unknown"" type=""my.package.Unknown"" />
			</method>
	  		<field deprecated=""not deprecated"" final=""true"" name=""value"" static=""true"" transient=""false"" type=""int"" type-generic-aware=""int"" visibility=""public"" volatile=""false"" value=""1234"" />
		</class>
		<interface abstract=""true"" deprecated=""not deprecated"" final=""false"" name=""service"" static=""false"" visibility=""public"" />
	</package>
</api>")) {
				xml = XDocument.Load (reader);
			}

			//Configure java.lang package
			var javaLang = xml.Element ("api").Element ("package");
			javaLangClasses = new List<XmlClassGen> ();
			foreach (var @class in javaLang.Elements("class")) {
				javaLangClasses.Add (new XmlClassGen (javaLang, @class));
			}

			package = (XElement)javaLang.NextNode;
		}

		[Test]
		public void Class ()
		{
			var element = package.Element ("class");
			var @class = new XmlClassGen (package, element);
			Assert.AreEqual ("public", @class.Visibility);
			Assert.AreEqual ("Foo", @class.Name);
			Assert.AreEqual ("com.mypackage.foo", @class.JavaName);
			Assert.AreEqual ("Lcom/mypackage/foo;", @class.JniName);
			Assert.IsFalse (@class.IsAbstract);
			Assert.IsFalse (@class.IsFinal);
			Assert.IsFalse (@class.IsDeprecated);
			Assert.IsNull (@class.DeprecatedComment);
		}

		[Test]
		public void Method ()
		{
			var element = package.Element ("class");
			var @class = new XmlClassGen (package, element);
			var method = new XmlMethod (@class, element.Element ("method"));
			Assert.IsTrue (method.Validate (new CodeGenerationOptions (), new GenericParameterDefinitionList ()), "method.Validate failed!");

			Assert.AreEqual ("public", method.Visibility);
			Assert.AreEqual ("void", method.Return);
			Assert.AreEqual ("System.Void", method.ReturnType);
			Assert.AreEqual ("Bar", method.Name);
			Assert.AreEqual ("bar", method.JavaName);
			Assert.AreEqual ("()V", method.JniSignature);
			Assert.IsFalse (method.IsAbstract);
			Assert.IsFalse (method.IsFinal);
			Assert.IsFalse (method.IsStatic);
			Assert.IsNull (method.Deprecated);
		}

		[Test]
		public void Method_Matches_True ()
		{
			var element = package.Element ("class");
			var @class = new XmlClassGen (package, element);
			var unknownTypes = element.Elements ("method").Where (e => e.Attribute ("name").Value == "unknownTypes").First ();
			var methodA = new XmlMethod (@class, unknownTypes);
			var methodB = new XmlMethod (@class, unknownTypes);
			Assert.IsTrue (methodA.Matches (methodB), "Methods should match!");
		}

		[Test]
		public void Method_Matches_False ()
		{
			var element = package.Element ("class");
			var @class = new XmlClassGen (package, element);
			var unknownTypesA = element.Elements ("method").Where (e => e.Attribute ("name").Value == "unknownTypes").First ();
			var unknownTypesB = element.Elements ("method").Where (e => e.Attribute ("name").Value == "unknownTypesReturn").First ();
			unknownTypesB.Attribute ("name").Value = "unknownTypes";
			var methodA = new XmlMethod (@class, unknownTypesA);
			var methodB = new XmlMethod (@class, unknownTypesB);
			//Everything the same besides return type
			Assert.IsFalse (methodA.Matches (methodB), "Methods should not match!");
		}

		[Test]
		public void MethodWithParameters ()
		{
			var element = package.Element ("class");
			var @class = new XmlClassGen (package, element);
			var method = new XmlMethod (@class, element.Elements ("method").Where (e => e.Attribute ("name").Value == "barWithParams").First ());
			Assert.IsTrue (method.Validate (new CodeGenerationOptions (), new GenericParameterDefinitionList ()), "method.Validate failed!");
			Assert.AreEqual ("(ZID)Ljava/lang/String;", method.JniSignature);
			Assert.AreEqual ("java.lang.String", method.Return);
			Assert.AreEqual ("System.String", method.ManagedReturn);

			var parameter = method.Parameters [0];
			Assert.AreEqual ("a", parameter.Name);
			Assert.AreEqual ("bool", parameter.Type);
			Assert.AreEqual ("boolean", parameter.JavaType);
			Assert.AreEqual ("Z", parameter.JniType);

			parameter = method.Parameters [1];
			Assert.AreEqual ("b", parameter.Name);
			Assert.AreEqual ("int", parameter.Type);
			Assert.AreEqual ("int", parameter.JavaType);
			Assert.AreEqual ("I", parameter.JniType);

			parameter = method.Parameters [2];
			Assert.AreEqual ("c", parameter.Name);
			Assert.AreEqual ("double", parameter.Type);
			Assert.AreEqual ("double", parameter.JavaType);
			Assert.AreEqual ("D", parameter.JniType);
		}

		[Test]
		public void Ctor ()
		{
			var element = package.Element ("class");
			var @class = new XmlClassGen (package, element);
			var ctor = new XmlCtor (@class, element.Element ("constructor"));
			Assert.IsTrue (ctor.Validate (new CodeGenerationOptions (), new GenericParameterDefinitionList ()), "ctor.Validate failed!");

			Assert.AreEqual ("public", ctor.Visibility);
			Assert.AreEqual ("foo", ctor.Name);
			Assert.AreEqual ("()V", ctor.JniSignature);
			Assert.IsNull (ctor.Deprecated);
		}

		[Test]
		public void Field ()
		{
			var element = package.Element ("class");
			var @class = new XmlClassGen (package, element);
			var field = new XmlField (element.Element ("field"));
			Assert.IsTrue (field.Validate (new CodeGenerationOptions (), new GenericParameterDefinitionList ()), "field.Validate failed!");

			Assert.AreEqual ("Value", field.Name);
			Assert.AreEqual ("value", field.JavaName);
			Assert.AreEqual ("1234", field.Value);
			Assert.AreEqual ("int", field.TypeName);
			Assert.IsTrue (field.IsStatic);
			Assert.IsTrue (field.IsConst);
		}

		[Test]
		public void Interface ()
		{
			var element = package.Element ("interface");
			var @interface = new XmlInterfaceGen (package, element);
			Assert.IsTrue (@interface.Validate (new CodeGenerationOptions (), new GenericParameterDefinitionList ()), "interface.Validate failed!");

			Assert.AreEqual ("public", @interface.Visibility);
			Assert.AreEqual ("IService", @interface.Name);
			Assert.AreEqual ("com.mypackage.service", @interface.JavaName);
			Assert.AreEqual ("Lcom/mypackage/service;", @interface.JniName);
		}
	}
}
