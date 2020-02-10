using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class GenBaseTests
	{
		CodeGenerationOptions options = new CodeGenerationOptions ();

		[Test]
		public void PropertyRequiresNew ()
		{
			var c = SupportTypeBuilder.CreateClassWithProperty ("MyClass", "java.myClass", "Handle", "int", options);
			Assert.True (c.RequiresNew (c.Properties.First ()));

			c.Properties.First ().Name = "GetHashCode";
			Assert.True (c.RequiresNew (c.Properties.First ()));

			c.Properties.First ().Name = "GetType";
			Assert.True (c.RequiresNew (c.Properties.First ()));

			c.Properties.First ().Name = "ToString";
			Assert.True (c.RequiresNew (c.Properties.First ()));

			c.Properties.First ().Name = "Equals";
			Assert.True (c.RequiresNew (c.Properties.First ()));

			c.Properties.First ().Name = "ReferenceEquals";
			Assert.True (c.RequiresNew (c.Properties.First ()));

			c.Properties.First ().Name = "Handle2";
			Assert.False (c.RequiresNew (c.Properties.First ()));
		}

		[Test]
		public void ToStringRequiresNew () => TestParameterlessMethods ("ToString");

		[Test]
		public void GetTypeRequiresNew () => TestParameterlessMethods ("GetType");

		[Test]
		public void GetHashCodeRequiresNew () => TestParameterlessMethods ("GetHashCode");

		[Test]
		public void StaticEqualsCodeRequiresNew () => TestStaticMethodsWithTwoParameters ("Equals");

		[Test]
		public void ReferenceEqualsEqualsCodeRequiresNew () => TestStaticMethodsWithTwoParameters ("ReferenceEquals");

		[Test]
		public void HandleAlwaysRequiresNew ()
		{
			// The same name as a property always requires new, no matter the parameters
			var c = SupportTypeBuilder.CreateClass ("java.myClass", options);
			var m = SupportTypeBuilder.CreateMethod (c, "Handle", options);

			// Yes
			Assert.True (c.RequiresNew (m.Name, m));

			// Yes, even with parameters
			m.Parameters.Add (new Parameter ("value", "int", "int", false));

			Assert.True (c.RequiresNew (m.Name, m));
		}

		[Test]
		public void TestEqualsMethodsWithOneParameter ()
		{
			var c = SupportTypeBuilder.CreateClass ("java.myClass", options);
			var m = SupportTypeBuilder.CreateMethod (c, "Equals", options, "void", false, false);

			// No because 0 parameters
			Assert.False (c.RequiresNew (m.Name, m));

			var p0 = new Parameter ("p0", "object", "object", false);
			m.Parameters.Add (p0);

			// Yes
			Assert.True (c.RequiresNew (m.Name, m));

			// No because parameter is wrong type
			var p1 = new Parameter ("p1", "string", "string", false);
			m = SupportTypeBuilder.CreateMethod (c, "Equals", options, "void", true, false, p1);

			Assert.False (c.RequiresNew (m.Name, m));
		}

		void TestParameterlessMethods (string name)
		{
			var c = SupportTypeBuilder.CreateClass ("java.myClass", options);
			var m = SupportTypeBuilder.CreateMethod (c, name, options);

			// Yes
			Assert.True (c.RequiresNew (m.Name, m));

			// No because > 0 parameters
			m.Parameters.Add (new Parameter ("value", "int", "int", false));

			Assert.False (c.RequiresNew (m.Name, m));
		}

		void TestStaticMethodsWithTwoParameters (string name)
		{
			var c = SupportTypeBuilder.CreateClass ("java.myClass", options);

			var p0 = new Parameter ("p0", "object", "object", false);
			var p1 = new Parameter ("p1", "object", "object", false);
			var m = SupportTypeBuilder.CreateMethod (c, name, options, "void", true, false, p0, p1);

			// Yes
			Assert.True (c.RequiresNew (m.Name, m));

			// No because != 2 parameters
			m.Parameters.Add (new Parameter ("value", "int", "int", false));
			Assert.False (c.RequiresNew (m.Name, m));

			// No because parameter is wrong type
			var p2 = new Parameter ("p1", "string", "string", false);
			m = SupportTypeBuilder.CreateMethod (c, name, options, "void", true, false, p0, p2);

			Assert.False (c.RequiresNew (m.Name, m));
		}
	}
}
