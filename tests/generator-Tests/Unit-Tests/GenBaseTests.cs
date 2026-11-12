using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class GenBaseTests
	{
		CodeGenerationOptions options = new CodeGenerationOptions ();

		[TestMethodAttribute]
		public void PropertyRequiresNew ()
		{
			var c = SupportTypeBuilder.CreateClassWithProperty ("MyClass", "java.myClass", "Handle", "int", options);
			Assert.IsTrue (c.RequiresNew (c.Properties.First ()));

			c.Properties.First ().Name = "GetHashCode";
			Assert.IsTrue (c.RequiresNew (c.Properties.First ()));

			c.Properties.First ().Name = "GetType";
			Assert.IsTrue (c.RequiresNew (c.Properties.First ()));

			c.Properties.First ().Name = "ToString";
			Assert.IsTrue (c.RequiresNew (c.Properties.First ()));

			c.Properties.First ().Name = "Equals";
			Assert.IsTrue (c.RequiresNew (c.Properties.First ()));

			c.Properties.First ().Name = "ReferenceEquals";
			Assert.IsTrue (c.RequiresNew (c.Properties.First ()));

			c.Properties.First ().Name = "Handle2";
			Assert.IsFalse (c.RequiresNew (c.Properties.First ()));
		}

		[TestMethodAttribute]
		public void ToStringRequiresNew () => TestParameterlessMethods ("ToString");

		[TestMethodAttribute]
		public void GetTypeRequiresNew () => TestParameterlessMethods ("GetType");

		[TestMethodAttribute]
		public void GetHashCodeRequiresNew () => TestParameterlessMethods ("GetHashCode");

		[TestMethodAttribute]
		public void StaticEqualsCodeRequiresNew () => TestStaticMethodsWithTwoParameters ("Equals");

		[TestMethodAttribute]
		public void ReferenceEqualsEqualsCodeRequiresNew () => TestStaticMethodsWithTwoParameters ("ReferenceEquals");

		[TestMethodAttribute]
		public void HandleAlwaysRequiresNew ()
		{
			// The same name as a property always requires new, no matter the parameters
			var c = SupportTypeBuilder.CreateClass ("java.myClass", options);
			var m = SupportTypeBuilder.CreateMethod (c, "Handle", options);

			// Yes
			Assert.IsTrue (c.RequiresNew (m.Name, m));

			// Yes, even with parameters
			m.Parameters.Add (new Parameter ("value", "int", "int", false));

			Assert.IsTrue (c.RequiresNew (m.Name, m));
		}

		[TestMethodAttribute]
		public void TestEqualsMethodsWithOneParameter ()
		{
			var c = SupportTypeBuilder.CreateClass ("java.myClass", options);
			var m = SupportTypeBuilder.CreateMethod (c, "Equals", options, "void", false, false);

			// No because 0 parameters
			Assert.IsFalse (c.RequiresNew (m.Name, m));

			var p0 = new Parameter ("p0", "object", "object", false);
			m.Parameters.Add (p0);

			// Yes
			Assert.IsTrue (c.RequiresNew (m.Name, m));

			// No because parameter is wrong type
			var p1 = new Parameter ("p1", "string", "string", false);
			m = SupportTypeBuilder.CreateMethod (c, "Equals", options, "void", true, false, p1);

			Assert.IsFalse (c.RequiresNew (m.Name, m));
		}

		[TestMethodAttribute]
		public void TestMethodClone_GenericArguments ()
		{
			var c = SupportTypeBuilder.CreateClass ("java.myClass", options);
			var m = SupportTypeBuilder.CreateMethod (c, "DoStuff", options);

			m.GenericArguments = new GenericParameterDefinitionList {
				new GenericParameterDefinition ("T", null)
			};

			var clone = m.Clone (c);

			Assert.AreEqual (1, clone.GenericArguments.Count);
			Assert.AreEqual ("T", clone.GenericArguments [0].Name);
		}

		void TestParameterlessMethods (string name)
		{
			var c = SupportTypeBuilder.CreateClass ("java.myClass", options);
			var m = SupportTypeBuilder.CreateMethod (c, name, options);

			// Yes
			Assert.IsTrue (c.RequiresNew (m.Name, m));

			// No because > 0 parameters
			m.Parameters.Add (new Parameter ("value", "int", "int", false));

			Assert.IsFalse (c.RequiresNew (m.Name, m));
		}

		void TestStaticMethodsWithTwoParameters (string name)
		{
			var c = SupportTypeBuilder.CreateClass ("java.myClass", options);

			var p0 = new Parameter ("p0", "object", "object", false);
			var p1 = new Parameter ("p1", "object", "object", false);
			var m = SupportTypeBuilder.CreateMethod (c, name, options, "void", true, false, p0, p1);

			// Yes
			Assert.IsTrue (c.RequiresNew (m.Name, m));

			// No because != 2 parameters
			m.Parameters.Add (new Parameter ("value", "int", "int", false));
			Assert.IsFalse (c.RequiresNew (m.Name, m));

			// No because parameter is wrong type
			var p2 = new Parameter ("p1", "string", "string", false);
			m = SupportTypeBuilder.CreateMethod (c, name, options, "void", true, false, p0, p2);

			Assert.IsFalse (c.RequiresNew (m.Name, m));
		}
	}
}
