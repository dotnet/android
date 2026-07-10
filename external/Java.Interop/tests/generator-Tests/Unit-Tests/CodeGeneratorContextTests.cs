using System;
using System.Linq;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class CodeGeneratorContextTests
	{
		[Test]
		public void GetContextTypeMember ()
		{
			var klass = new TestClass ("Object", "java.code.MyClass");

			klass.AddMethod (SupportTypeBuilder.CreateMethod (klass, "Echo", new CodeGenerationOptions (), "uint", false, false, new Parameter ("value", "uint", "uint", false)));
			klass.AddField (new TestField ("string", "Foo"));

			var context = new CodeGeneratorContext ();
			context.ContextTypes.Push (klass);

			Assert.AreEqual ("java.code.MyClass", context.GetContextTypeMember ());

			context.ContextMethod = klass.Methods.Single ();

			Assert.AreEqual ("java.code.MyClass.Echo (uint)", context.GetContextTypeMember ());

			context.ContextMethod = null;
			context.ContextField = klass.Fields.Single ();

			Assert.AreEqual ("java.code.MyClass.Foo", context.GetContextTypeMember ());

			context.ContextMethod = klass.Methods.Single ();
			context.ContextField = null;
			context.ContextTypes.Clear ();

			Assert.AreEqual ("Echo (uint)", context.GetContextTypeMember ());
		}
	}
}
