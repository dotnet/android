using MonoDroid.Generation;
using NUnit.Framework;
using System.IO;
using System.Text;

namespace generatortests
{
	abstract class CodeGeneratorTests
	{
		protected CodeGenerator generator;
		protected StringBuilder builder;
		protected StringWriter writer;
		protected CodeGenerationOptions options;

		[SetUp]
		public void SetUp ()
		{
			builder = new StringBuilder ();
			writer = new StringWriter (builder);
			options = new CodeGenerationOptions {
				CodeGenerationTarget = Target,
			};
			generator = options.CodeGenerator;
		}

		[TearDown]
		public void TearDown ()
		{
			writer.Dispose ();
		}

		protected abstract Xamarin.Android.Binder.CodeGenerationTarget Target { get; }

		[Test]
		public void WriteEnumifiedField ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("int", "bar").SetEnumified ();
			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList ()), "field.Validate failed!");
			generator.WriteField (field, writer, string.Empty, options, @class);

			StringAssert.Contains ("[global::Android.Runtime.GeneratedEnum]", builder.ToString (), "Should contain GeneratedEnumAttribute!");
		}

		[Test]
		public void WriteDeprecatedField ()
		{
			var comment = "Don't use this!";
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("int", "bar").SetConstant ("1234").SetDeprecated (comment);
			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList ()), "field.Validate failed!");
			generator.WriteField (field, writer, string.Empty, options, @class);

			StringAssert.Contains ($"[Obsolete (\"{comment}\")]", builder.ToString (), "Should contain ObsoleteAttribute!");
		}

		[Test]
		public void WriteProtectedField ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("int", "bar").SetVisibility ("protected");
			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList ()), "field.Validate failed!");
			generator.WriteField (field, writer, string.Empty, options, @class);

			StringAssert.Contains ("protected int bar {", builder.ToString (), "Property should be protected!");
		}

		[Test]
		public void WriteDeprecatedMethod ()
		{
			var comment = "Don't use this!";
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetDeprecated (comment);
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList ()), "method.Validate failed!");
			generator.WriteMethod (method, writer, string.Empty, options, @class, true);

			StringAssert.Contains ($"[Obsolete (@\"{comment}\")]", builder.ToString (), "Should contain ObsoleteAttribute!");
		}

		[Test]
		public void WritedMethodWithManagedReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar", @return: "int").SetManagedReturn ("long");
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList ()), "method.Validate failed!");
			generator.WriteMethod (method, writer, string.Empty, options, @class, true);

			StringAssert.Contains ("public virtual unsafe long bar ()", builder.ToString (), "Should contain return long!");
		}

		[Test]
		public void WritedMethodWithEnumifiedReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar", @return: "int").SetManagedReturn ("int").SetReturnEnumified ();
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList ()), "method.Validate failed!");
			generator.WriteMethod (method, writer, string.Empty, options, @class, true);

			StringAssert.Contains ("[return:global::Android.Runtime.GeneratedEnum]", builder.ToString (), "Should contain GeneratedEnumAttribute!");
		}
	}
}
