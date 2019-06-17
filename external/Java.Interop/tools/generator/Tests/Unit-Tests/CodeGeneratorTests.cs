using generatortests.Unit_Tests;
using MonoDroid.Generation;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
				CodeGenerationTarget = Target
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
		public void WriteCharSequenceEnumerator ()
		{
			generator.WriteCharSequenceEnumerator (writer, string.Empty, options);

			Assert.AreEqual (GetExpected (nameof (WriteCharSequenceEnumerator)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClass ()
		{
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);

			options.ContextTypes.Push (@class);
			generator.WriteClass (@class, writer, string.Empty, options, new GenerationInfo ("", "", "MyAssembly"));
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClass)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassAbstractMembers ()
		{
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);

			options.ContextTypes.Push (@class);
			generator.WriteClassAbstractMembers (@class, writer, string.Empty, options);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteClassAbstractMembers)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassConstructors ()
		{
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);

			options.ContextTypes.Push (@class);
			generator.WriteClassConstructors (@class, writer, string.Empty, options);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassConstructors)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassInvoker ()
		{
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);

			options.ContextTypes.Push (@class);
			generator.WriteClassInvoker (@class, writer, string.Empty, options);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassInvoker)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassInvokerMembers ()
		{
			// This test should generate all the members (members is empty)
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);
			var members = new HashSet<string> ();

			options.ContextTypes.Push (@class);
			generator.WriteClassInvokerMembers (@class, writer, string.Empty, options, members);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassInvokerMembers)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassMethodInvokers ()
		{
			// This test should generate all the methods (members is empty)
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);
			var members = new HashSet<string> ();

			options.ContextTypes.Push (@class);
			generator.WriteClassMethodInvokers (@class, @class.Methods, writer, string.Empty, options, members, null);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassMethodInvokers)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassMethodInvokersWithSkips ()
		{
			// This test should skip the first Method because members contains the Method
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);
			var members = new HashSet<string> (new [] { @class.Methods [0].Name });

			options.ContextTypes.Push (@class);
			generator.WriteClassMethodInvokers (@class, @class.Methods, writer, string.Empty, options, members, null);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassMethodInvokersWithSkips)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassMethods ()
		{
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);

			options.ContextTypes.Push (@class);
			generator.WriteClassMethods (@class, writer, string.Empty, options);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassMethods)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassProperties ()
		{
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);

			options.ContextTypes.Push (@class);
			generator.WriteClassProperties (@class, writer, string.Empty, options);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassProperties)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassPropertyInvokers ()
		{
			// This test should generate all the properties (members is empty)
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);
			var members = new HashSet<string> ();

			options.ContextTypes.Push (@class);
			generator.WriteClassPropertyInvokers (@class, @class.Properties, writer, string.Empty, options, members);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassPropertyInvokers)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassPropertyInvokersWithSkips ()
		{
			// This test should skip the first Property because members contains the Property
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);
			var members = new HashSet<string> (new [] { @class.Properties [0].Name });

			options.ContextTypes.Push (@class);
			generator.WriteClassPropertyInvokers (@class, @class.Properties, writer, string.Empty, options, members);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassPropertyInvokersWithSkips)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteCtor ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var ctor = new TestCtor (@class, "Object");

			options.ContextTypes.Push (@class);
			generator.WriteConstructor (ctor, writer, string.Empty, options, true, @class);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteCtor)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteCtorDeprecated ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var ctor = new TestCtor (@class, "Object")
				.SetDeprecated ("This constructor is obsolete")
				.SetCustomAttributes ("[MyAttribute]")
				.SetAnnotation ("[global::Android.Runtime.IntDefinition (null, JniField = \"xamarin/test/SomeObject.SOME_VALUE\")]");

			options.ContextTypes.Push (@class);
			generator.WriteConstructor (ctor, writer, string.Empty, options, true, @class);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteCtorDeprecated)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteCtorWithStringOverload ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var ctor = new TestCtor (@class, "Object");

			ctor.Parameters.Add (new Parameter ("mystring", "java.lang.CharSequence", "Java.Lang.ICharSequence", false));
			ctor.Validate (options, null);

			options.ContextTypes.Push (@class);
			generator.WriteConstructor (ctor, writer, string.Empty, options, true, @class);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteCtorWithStringOverload)), writer.ToString ().NormalizeLineEndings ());
		}

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

		[Test]
		public void WriteInterface ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);
			var gen_info = new GenerationInfo (null, null, null);

			options.ContextTypes.Push (iface);
			generator.WriteInterface (iface, writer, string.Empty, options, gen_info);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteInterface)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceDeclaration ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			options.ContextTypes.Push (iface);
			generator.WriteInterfaceDeclaration (iface, writer, string.Empty, options);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceDeclaration)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceExtensionMethods ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			options.ContextTypes.Push (iface);
			generator.WriteInterfaceExtensionMethods (iface, writer, string.Empty, options);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceExtensionMethods)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceEventArgs ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			options.ContextTypes.Push (iface);
			generator.WriteInterfaceEventArgs (iface, iface.Methods[0], writer, string.Empty, options);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceEventArgs)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceEventHandler ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			options.ContextTypes.Push (iface);
			generator.WriteInterfaceEventHandler (iface, writer, string.Empty, options);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceEventHandler)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceEventHandlerImpl ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			options.ContextTypes.Push (iface);
			generator.WriteInterfaceEventHandlerImpl (iface, writer, string.Empty, options);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceEventHandlerImpl)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceEventHandlerImplContent ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);
			var handlers = new List<string> ();

			options.ContextTypes.Push (iface);
			generator.WriteInterfaceEventHandlerImplContent (iface, iface.Methods[0], writer, string.Empty, options, true, string.Empty, handlers);
			options.ContextTypes.Pop ();

			Assert.AreEqual (1, handlers.Count);
			Assert.AreEqual ("GetCountForKey", handlers [0]);
			Assert.AreEqual (GetExpected (nameof (WriteInterfaceEventHandlerImplContent)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceExtensionsDeclaration ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			options.ContextTypes.Push (iface);
			generator.WriteInterfaceExtensionsDeclaration (iface, writer, string.Empty, options, "java.code.DeclaringType");
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceExtensionsDeclaration)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceInvoker ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			options.ContextTypes.Push (iface);
			generator.WriteInterfaceInvoker (iface, writer, string.Empty, options);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteInterfaceInvoker)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceListenerEvent ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			options.ContextTypes.Push (iface);
			generator.WriteInterfaceListenerEvent (iface, writer, string.Empty, options, "MyName", "MyNameSpec", "MyMethodName", "MyFullDelegateName", true, "MyWrefSuffix", "Add", "Remove");
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceListenerEvent)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceListenerProperty ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			options.ContextTypes.Push (iface);
			generator.WriteInterfaceListenerProperty (iface, writer, string.Empty, options, "MyName", "MyNameSpec", "MyMethodName", "MyConnectorFmt", "MyFullDelegateName");
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceListenerProperty)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceMethodInvokers ()
		{
			// This test should generate all the methods (members is empty)
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);
			var members = new HashSet<string> ();

			options.ContextTypes.Push (iface);
			generator.WriteInterfaceMethodInvokers (iface, iface.Methods, writer, string.Empty, options, members);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceMethodInvokers)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceMethodInvokersWithSkips ()
		{
			// This test should skip the first Method because members contains the Method
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);
			var members = new HashSet<string> (new [] { iface.Methods [0].Name });

			options.ContextTypes.Push (iface);
			generator.WriteInterfaceMethodInvokers (iface, iface.Methods, writer, string.Empty, options, members);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceMethodInvokersWithSkips)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceMethods ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			options.ContextTypes.Push (iface);
			generator.WriteInterfaceMethods (iface, writer, string.Empty, options);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceMethods)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceProperties ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			options.ContextTypes.Push (iface);
			generator.WriteInterfaceProperties (iface, writer, string.Empty, options);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceProperties)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfacePropertyInvokers ()
		{
			// This test should generate all the properties (members is empty)
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);
			var members = new HashSet<string> ();

			options.ContextTypes.Push (iface);
			generator.WriteInterfacePropertyInvokers (iface, iface.Properties, writer, string.Empty, options, members);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfacePropertyInvokers)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfacePropertyInvokersWithSkips ()
		{
			// This test should skip the first Property because members contains the Property
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);
			var members = new HashSet<string> (new [] { iface.Properties [0].Name });

			options.ContextTypes.Push (iface);
			generator.WriteInterfacePropertyInvokers (iface, iface.Properties, writer, string.Empty, options, members);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfacePropertyInvokersWithSkips)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteParameterListCallArgs ()
		{
			var list = SupportTypeBuilder.CreateParameterList (options);

			generator.WriteParameterListCallArgs (list, writer, string.Empty, options, false);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteParameterListCallArgs)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteParameterListCallArgsForInvoker ()
		{
			var list = SupportTypeBuilder.CreateParameterList (options);

			generator.WriteParameterListCallArgs (list, writer, string.Empty, options, true);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteParameterListCallArgsForInvoker)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteProperty ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.WriteProperty (@class.Properties.First (), @class, writer, string.Empty, options);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteProperty)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WritePropertyAbstractDeclaration ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.WritePropertyAbstractDeclaration (@class.Properties.First (), writer, string.Empty, options, @class);

			Assert.AreEqual (GetExpected (nameof (WritePropertyAbstractDeclaration)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WritePropertyCallbacks ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.WritePropertyCallbacks (@class.Properties.First (), writer, string.Empty, options, @class);

			Assert.AreEqual (GetExpected (nameof (WritePropertyCallbacks)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WritePropertyDeclaration ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.WritePropertyDeclaration (@class.Properties.First (), writer, string.Empty, options, @class, "ObjectAdapter");

			Assert.AreEqual (GetExpected (nameof (WritePropertyDeclaration)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WritePropertyStringVariant ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.WritePropertyStringVariant (@class.Properties.First (), writer, string.Empty);

			Assert.AreEqual (GetExpected (nameof (WritePropertyStringVariant)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WritePropertyInvoker ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			options.ContextTypes.Push (@class);
			generator.WritePropertyInvoker (@class.Properties.First (), writer, string.Empty, options, @class);
			options.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WritePropertyInvoker)), writer.ToString ().NormalizeLineEndings ());
		}

		// Get the test results from "Common" for tests with the same results regardless of Target
		string GetExpected (string testName)
		{
			var root = Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location);

			return File.ReadAllText (Path.Combine (root, "Unit-Tests", "CodeGeneratorExpectedResults", "Common", $"{testName}.txt")).NormalizeLineEndings ();
		}

		// Get the test results from "JavaInterop1" or "XamarinAndroid" for tests with the different results per Target
		string GetTargetedExpected (string testName)
		{
			var target = Target.ToString ();
			var root = Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location);

			return File.ReadAllText (Path.Combine (root, "Unit-Tests", "CodeGeneratorExpectedResults", target, $"{testName}.txt")).NormalizeLineEndings ();
		}
	}
}
