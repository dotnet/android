using System;
using System.Linq;
using System.Xml.Linq;
using Java.Interop.Tools.Generator.Transformation;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class SealedProtectedFixupsTests
	{
		private CodeGenerationOptions options = new CodeGenerationOptions ();

		[Test]
		public void FixProtectedMethod ()
		{
			var klass = CreateSealedClass ();

			var method = SupportTypeBuilder.CreateMethod (klass, "ToString", options);

			klass.Methods.Add (method);

			method.Visibility = "protected";
			method.IsOverride = false;

			SealedProtectedFixups.Fixup (new [] { (GenBase)klass }.ToList ());

			Assert.AreEqual ("private", method.Visibility);
		}

		[Test]
		public void FixProtectedProperty ()
		{
			var klass = CreateSealedClass ();

			var method = SupportTypeBuilder.CreateProperty (klass, "Handle", "int", options);

			klass.Properties.Add (method);

			method.Getter.Visibility = "protected";
			method.Getter.IsOverride = false;

			method.Setter.Visibility = "protected";
			method.Setter.IsOverride = false;

			SealedProtectedFixups.Fixup (new [] { (GenBase) klass }.ToList ());

			Assert.AreEqual ("private", method.Getter.Visibility);
			Assert.AreEqual ("private", method.Setter.Visibility);
		}

		[Test]
		public void FixProtectedField ()
		{
			var klass = CreateSealedClass ();

			var field = new Field {
				Name = "MyConstant",
				TypeName = "int",
				Visibility = "protected"
			};

			klass.Fields.Add (field);

			SealedProtectedFixups.Fixup (new [] { (GenBase) klass }.ToList ());

			Assert.AreEqual ("private", field.Visibility);
		}

		[Test]
		public void FixProtectedType ()
		{
			var klass = CreateSealedClass ();

			var type = SupportTypeBuilder.CreateClass ("my.example.class.inner", options);
			type.Visibility = "protected";

			klass.NestedTypes.Add (type);

			SealedProtectedFixups.Fixup (new [] { (GenBase) klass }.ToList ());

			Assert.AreEqual ("private", type.Visibility);
		}

		private ClassGen CreateSealedClass ()
		{
			var klass = SupportTypeBuilder.CreateClass ("my.example.class", options);
			klass.IsFinal = true;
			return klass;
		}
	}
}
