using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using NUnit.Framework;
using generator.SourceWriters;

namespace generatortests
{
	[TestFixture]
	public class GenBaseTests
	{
		CodeGenerationOptions options = new CodeGenerationOptions ();

		[Test]
		public void InheritedFieldsAndPropertiesRequireNew ()
		{
			var parent = SupportTypeBuilder.CreateClass ("java.Base", options);
			var derived = new DerivedTestClass (parent);
			var field = SupportTypeBuilder.CreateField ("Count", options, "int");

			Assert.True (new BoundFieldAsProperty (derived, field, options).IsShadow);
			Assert.True (new BoundField (derived, field.SetConstant ("1"), options).IsShadow);
		}

		[Test]
		public void HiddenVirtualMethodRetainsNewSlot ()
		{
			var parent = SupportTypeBuilder.CreateClass ("java.Base", options);
			parent.Methods.Add (SupportTypeBuilder.CreateMethod (parent, "DoWork", options));
			var derived = new DerivedTestClass (parent);
			var method = SupportTypeBuilder.CreateMethod (derived, "DoWork", options);
			var writer = new BoundMethod (derived, method, options, generateCallbacks: false);

			Assert.True (writer.IsShadow);
			Assert.True (writer.IsVirtual);
			Assert.False (writer.IsOverride);
		}

		[Test]
		public void InheritedInterfaceMethodRequiresNew ()
		{
			var parent = SupportTypeBuilder.CreateEmptyInterface ("java.Parent");
			parent.Methods.Add (SupportTypeBuilder.CreateMethod (parent, "Close", options));
			var derived = SupportTypeBuilder.CreateEmptyInterface ("java.Derived");
			derived.Interfaces.Add (parent);
			var method = SupportTypeBuilder.CreateMethod (derived, "Close", options);

			Assert.True (new BoundInterfaceMethodDeclaration (derived, method, "", options).IsShadow);
		}

		[Test]
		public void NestedCallbackHidesEnclosingBaseCallback ()
		{
			var parent = SupportTypeBuilder.CreateClass ("java.Base", options);
			parent.Methods.Add (SupportTypeBuilder.CreateMethod (parent, "DoWork", options));
			var nested = new DerivedTestClass (parent, "java.Base.Nested");
			var method = SupportTypeBuilder.CreateMethod (nested, "DoWork", options);

			Assert.True (new MethodCallback (nested, method, options, null, false).IsShadow);
		}

		[Test]
		public void DifferentOverloadDoesNotRequireNew ()
		{
			var parent = SupportTypeBuilder.CreateClass ("java.Base", options);
			parent.Methods.Add (SupportTypeBuilder.CreateMethod (parent, "DoWork", options));
			var derived = new DerivedTestClass (parent);
			var method = SupportTypeBuilder.CreateMethod (derived, "DoWork", options, parameters: new Parameter ("value", "int", "int", false));

			Assert.False (derived.RequiresNew (method.AdjustedName, method));
		}

		sealed class DerivedTestClass : TestClass
		{
			readonly ClassGen parent;

			public DerivedTestClass (ClassGen parent, string name = "java.Derived") : base (parent.FullName, name)
			{
				this.parent = parent;
			}

			public override ClassGen BaseGen => parent;
		}

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

		[Test]
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
