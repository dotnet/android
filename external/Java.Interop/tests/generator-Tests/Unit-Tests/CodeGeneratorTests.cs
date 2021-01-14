using System;
using System.Collections.Generic;
using System.Linq;
using MonoDroid.Generation;
using NUnit.Framework;
using Xamarin.Android.Binder;

namespace generatortests
{
	[TestFixture]
	class JavaInteropCodeGeneratorTests : CodeGeneratorTests
	{
		protected override CodeGenerationTarget Target => CodeGenerationTarget.JavaInterop1;

		[Test]
		public void WriteKotlinUnsignedTypeMethodsClass ()
		{
			var @class = new TestClass ("Object", "java.code.MyClass");

			@class.AddMethod (SupportTypeBuilder.CreateMethod (@class, "Echo", options, "uint", false, false, new Parameter ("value", "uint", "uint", false)));
			@class.AddMethod (SupportTypeBuilder.CreateMethod (@class, "Echo", options, "ushort", false, false, new Parameter ("value", "ushort", "ushort", false)));
			@class.AddMethod (SupportTypeBuilder.CreateMethod (@class, "Echo", options, "ulong", false, false, new Parameter ("value", "ulong", "ulong", false)));
			@class.AddMethod (SupportTypeBuilder.CreateMethod (@class, "Echo", options, "ubyte", false, false, new Parameter ("value", "ubyte", "byte", false)));

			// Kotlin methods with unsigned types are name-mangled and don't support virtual
			foreach (var m in @class.Methods)
				m.IsVirtual = false;

			generator.Context.ContextTypes.Push (@class);
			generator.WriteType (@class, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteKotlinUnsignedTypeMethodsClass)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteKotlinUnsignedTypePropertiesClass ()
		{
			var @class = new TestClass ("Object", "java.code.MyClass");

			@class.Properties.Add (SupportTypeBuilder.CreateProperty (@class, "UIntProp", "uint", options, false, false));
			@class.Properties.Add (SupportTypeBuilder.CreateProperty (@class, "UShortProp", "ushort", options, false, false));
			@class.Properties.Add (SupportTypeBuilder.CreateProperty (@class, "ULongProp", "ulong", options, false, false));
			@class.Properties.Add (SupportTypeBuilder.CreateProperty (@class, "UByteProp", "ubyte", options, false, false));

			// Kotlin methods with unsigned types are name-mangled and don't support virtual
			foreach (var m in @class.Properties) {
				m.Getter.IsVirtual = false;
				m.Setter.IsVirtual = false;
			}

			generator.Context.ContextTypes.Push (@class);
			generator.WriteType (@class, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteKotlinUnsignedTypePropertiesClass)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteKotlinUnsignedArrayTypeMethodsClass ()
		{
			var @class = new TestClass ("Object", "java.code.MyClass");

			@class.AddMethod (SupportTypeBuilder.CreateMethod (@class, "Echo", options, "uint[]", false, false, new Parameter ("value", "uint[]", "uint[]", false)));
			@class.AddMethod (SupportTypeBuilder.CreateMethod (@class, "Echo", options, "ushort[]", false, false, new Parameter ("value", "ushort[]", "ushort[]", false)));
			@class.AddMethod (SupportTypeBuilder.CreateMethod (@class, "Echo", options, "ulong[]", false, false, new Parameter ("value", "ulong[]", "ulong[]", false)));
			@class.AddMethod (SupportTypeBuilder.CreateMethod (@class, "Echo", options, "ubyte[]", false, false, new Parameter ("value", "ubyte[]", "byte[]", false)));

			// Kotlin methods with unsigned types are name-mangled and don't support virtual
			foreach (var m in @class.Methods)
				m.IsVirtual = false;

			generator.Context.ContextTypes.Push (@class);
			generator.WriteType (@class, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteKotlinUnsignedArrayTypeMethodsClass)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteKotlinUnsignedArrayTypePropertiesClass ()
		{
			var @class = new TestClass ("Object", "java.code.MyClass");

			@class.Properties.Add (SupportTypeBuilder.CreateProperty (@class, "UIntProp", "uint[]", options, false, false));
			@class.Properties.Add (SupportTypeBuilder.CreateProperty (@class, "UShortProp", "ushort[]", options, false, false));
			@class.Properties.Add (SupportTypeBuilder.CreateProperty (@class, "ULongProp", "ulong[]", options, false, false));
			@class.Properties.Add (SupportTypeBuilder.CreateProperty (@class, "UByteProp", "ubyte[]", options, false, false));

			// Kotlin methods with unsigned types are name-mangled and don't support virtual
			foreach (var m in @class.Properties) {
				m.Getter.IsVirtual = false;
				m.Setter.IsVirtual = false;
			}

			generator.Context.ContextTypes.Push (@class);
			generator.WriteType (@class, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteKotlinUnsignedArrayTypePropertiesClass)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void ManagedOverrideMethod_Virtual ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='not deprecated' final='true' name='DoStuff' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' managedOverride='virtual'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var klass = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.True (writer.ToString ().Contains ("public virtual unsafe int DoStuff ()"));
		}

		[Test]
		public void ManagedOverrideMethod_Override ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='not deprecated' final='true' name='DoStuff' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' managedOverride='override'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var klass = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.True (writer.ToString ().Contains ("public override unsafe int DoStuff ()"));
		}

		[Test]
		public void ManagedOverrideProperty_Virtual ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='not deprecated' final='true' name='getName' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' managedOverride='virtual'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var klass = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.True (writer.ToString ().Contains ("public virtual unsafe int Name {"));
		}

		[Test]
		public void ManagedOverrideProperty_Override ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='not deprecated' final='true' name='getName' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' managedOverride='override'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var klass = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.True (writer.ToString ().Contains ("public override unsafe int Name {"));
		}

		[Test]
		public void WriteDuplicateInterfaceEventArgs ()
		{
			// If we have 2 methods that would each create the same EventArgs class,
			// make sure we combine them into 1 class with both members instead.
			var iface = SupportTypeBuilder.CreateEmptyInterface ("java.code.AnimatorListener");

			var method1 = SupportTypeBuilder.CreateMethod (iface, "OnAnimationEnd", options, "boolean", false, true, new Parameter ("param1", "int", "int", false));
			var method2 = SupportTypeBuilder.CreateMethod (iface, "OnAnimationEnd", options, "boolean", false, true, new Parameter ("param1", "int", "int", false), new Parameter ("param2", "int", "int", false));

			iface.Methods.Add (method1);
			iface.Methods.Add (method2);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteDuplicateInterfaceEventArgs)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void SupportedOSPlatform ()
		{
			// We do not write [SupportedOSPlatform] for JavaInterop, only XAJavaInterop
			var klass = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);
			klass.ApiAvailableSince = 30;

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			StringAssert.DoesNotContain ("[global::System.Runtime.Versioning.SupportedOSPlatformAttribute (\"android30.0\")]", builder.ToString (), "Should contain SupportedOSPlatform!");
		}
	}

	[TestFixture]
	class XAJavaInteropCodeGeneratorTests : CodeGeneratorTests
	{
		protected override CodeGenerationTarget Target => CodeGenerationTarget.XAJavaInterop1;


		// Disabled until we can properly build .NET 5/6 assemblies in our XA tree.
		//[Test]
		//public void SupportedOSPlatform ()
		//{
		//	var klass = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);
		//	klass.ApiAvailableSince = 30;

		//	generator.Context.ContextTypes.Push (klass);
		//	generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
		//	generator.Context.ContextTypes.Pop ();

		//	StringAssert.Contains ("[global::System.Runtime.Versioning.SupportedOSPlatformAttribute (\"android30.0\")]", builder.ToString (), "Should contain SupportedOSPlatform!");
		//}
	}

	[TestFixture]
	class XamarinAndroidCodeGeneratorTests : CodeGeneratorTests
	{
		protected override CodeGenerationTarget Target => CodeGenerationTarget.XamarinAndroid;

		[Test]
		public void WriteClassConstructors ()
		{
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);

			generator.Context.ContextTypes.Push (@class);
			generator.WriteClassConstructors (@class, string.Empty);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassConstructors)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassHandle ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");

			generator.WriteClassHandle (@class, string.Empty, false);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassHandle)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassInvoker ()
		{
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);

			generator.Context.ContextTypes.Push (@class);
			generator.WriteClassInvoker (@class, string.Empty);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassInvoker)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassInvokerHandle ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");

			generator.WriteClassInvokerHandle (@class, string.Empty, "Com.MyPackage.Foo");

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassInvokerHandle)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassInvokerMembers ()
		{
			// This test should generate all the members (members is empty)
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);
			var members = new HashSet<string> ();

			generator.Context.ContextTypes.Push (@class);
			generator.WriteClassInvokerMembers (@class, string.Empty, members);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassInvokerMembers)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassMethodInvokers ()
		{
			// This test should generate all the methods (members is empty)
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);
			var members = new HashSet<string> ();

			generator.Context.ContextTypes.Push (@class);
			generator.WriteClassMethodInvokers (@class, @class.Methods, string.Empty, members, null);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassMethodInvokers)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassMethodInvokersWithSkips ()
		{
			// This test should skip the first Method because members contains the Method
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);
			var members = new HashSet<string> (new [] { @class.Methods [0].Name });

			generator.Context.ContextTypes.Push (@class);
			generator.WriteClassMethodInvokers (@class, @class.Methods, string.Empty, members, null);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassMethodInvokersWithSkips)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassMethods ()
		{
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);

			generator.Context.ContextTypes.Push (@class);
			generator.WriteClassMethods (@class, string.Empty);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassMethods)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassProperties ()
		{
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);

			generator.Context.ContextTypes.Push (@class);
			generator.WriteImplementedProperties (@class.Properties, string.Empty, @class.IsFinal, @class);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassProperties)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassPropertyInvokers ()
		{
			// This test should generate all the properties (members is empty)
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);
			var members = new HashSet<string> ();

			generator.Context.ContextTypes.Push (@class);
			generator.WriteClassPropertyInvokers (@class, @class.Properties, string.Empty, members);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassPropertyInvokers)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassPropertyInvokersWithSkips ()
		{
			// This test should skip the first Property because members contains the Property
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);
			var members = new HashSet<string> (new [] { @class.Properties [0].Name });

			generator.Context.ContextTypes.Push (@class);
			generator.WriteClassPropertyInvokers (@class, @class.Properties, string.Empty, members);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClassPropertyInvokersWithSkips)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteCtor ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var ctor = new TestCtor (@class, "Object");

			generator.Context.ContextTypes.Push (@class);
			generator.WriteConstructor (ctor, string.Empty, true, @class);
			generator.Context.ContextTypes.Pop ();

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

			generator.Context.ContextTypes.Push (@class);
			generator.WriteConstructor (ctor, string.Empty, true, @class);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteCtorDeprecated)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteCtorWithStringOverload ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var ctor = new TestCtor (@class, "Object");

			ctor.Parameters.Add (new Parameter ("mystring", "java.lang.CharSequence", "Java.Lang.ICharSequence", false));
			ctor.Validate (options, null, new CodeGeneratorContext ());

			generator.Context.ContextTypes.Push (@class);
			generator.WriteConstructor (ctor, string.Empty, true, @class);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteCtorWithStringOverload)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteEnumifiedField ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("int", "bar").SetEnumified ();
			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteField (field, string.Empty, @class);

			StringAssert.Contains ("[global::Android.Runtime.GeneratedEnum]", builder.ToString (), "Should contain GeneratedEnumAttribute!");
		}

		[Test]
		public void WriteDeprecatedField ()
		{
			var comment = "Don't use this!";
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("int", "bar").SetConstant ("1234").SetDeprecated (comment, true);
			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteField (field, string.Empty, @class);

			StringAssert.Contains ($"[Obsolete (\"{comment}\", error: true)]", builder.ToString (), "Should contain ObsoleteAttribute!");
		}

		[Test]
		public void WriteProtectedField ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("int", "bar").SetVisibility ("protected");
			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteField (field, string.Empty, @class);

			StringAssert.Contains ("protected int bar {", builder.ToString (), "Property should be protected!");
		}

		[Test]
		public void WriteFieldConstant ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar").SetConstant ();

			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteField (field, string.Empty, @class);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteFieldConstant)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteFieldConstantWithIntValue ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("int", "bar").SetConstant ("1234");

			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteField (field, string.Empty, @class);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteFieldConstantWithIntValue)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteFieldConstantWithStringValue ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar").SetConstant ("\"hello\"");

			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteField (field, string.Empty, @class);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteFieldConstantWithStringValue)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteFieldGetBody ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar");

			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteFieldGetBody (field, string.Empty, @class);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteFieldGetBody)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteFieldIdField ()
		{
			var field = new TestField ("java.lang.String", "bar");

			generator.WriteFieldIdField (field, string.Empty);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteFieldIdField)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteFieldInt ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("int", "bar");

			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteField (field, string.Empty, @class);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteFieldInt)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteFieldSetBody ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar");

			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteFieldSetBody (field, string.Empty, @class);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteFieldSetBody)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteFieldString ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar");

			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteField (field, string.Empty, @class);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteFieldString)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteDeprecatedMethod ()
		{
			var comment = "Don't use this!";
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetDeprecated (comment);
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			StringAssert.Contains ($"[Obsolete (@\"{comment}\")]", builder.ToString (), "Should contain ObsoleteAttribute!");
		}

		[Test]
		public void WritedMethodWithManagedReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar", @return: "int").SetManagedReturn ("long");
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			StringAssert.Contains ("public virtual unsafe long bar ()", builder.ToString (), "Should contain return long!");
		}

		[Test]
		public void WritedMethodWithEnumifiedReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar", @return: "int").SetManagedReturn ("int").SetReturnEnumified ();
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			StringAssert.Contains ("[return:global::Android.Runtime.GeneratedEnum]", builder.ToString (), "Should contain GeneratedEnumAttribute!");
		}

		[Test]
		public void WriteInterfaceInvoker ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceInvoker (iface, string.Empty);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteInterfaceInvoker)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceListenerEvent ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceListenerEvent (iface, string.Empty, "MyName", "MyNameSpec", "MyMethodName", "MyFullDelegateName", true, "MyWrefSuffix", "Add", "Remove");
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceListenerEvent)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceListenerEventWithHandlerArgument ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceListenerEvent (iface, string.Empty, "MyName", "MyNameSpec", "MyMethodName", "MyFullDelegateName", true, "MyWrefSuffix", "AddMyName", "RemoveMyName", true);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceListenerEventWithHandlerArgument)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceListenerProperty ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceListenerProperty (iface, string.Empty, "MyName", "MyNameSpec", "MyMethodName", "MyConnectorFmt", "MyFullDelegateName");
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceListenerProperty)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceMethodInvokers ()
		{
			// This test should generate all the methods (members is empty)
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);
			var members = new HashSet<string> ();

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceMethodInvokers (iface, iface.Methods, string.Empty, members);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceMethodInvokers)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceMethodInvokersWithSkips ()
		{
			// This test should skip the first Method because members contains the Method
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);
			var members = new HashSet<string> (new [] { iface.Methods [0].Name });

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceMethodInvokers (iface, iface.Methods, string.Empty, members);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceMethodInvokersWithSkips)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceMethods ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceMethods (iface, string.Empty);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceMethods)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceProperties ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceProperties (iface, string.Empty);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceProperties)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfacePropertyInvokers ()
		{
			// This test should generate all the properties (members is empty)
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);
			var members = new HashSet<string> ();

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfacePropertyInvokers (iface, iface.Properties, string.Empty, members);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfacePropertyInvokers)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfacePropertyInvokersWithSkips ()
		{
			// This test should skip the first Property because members contains the Property
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);
			var members = new HashSet<string> (new [] { iface.Properties [0].Name });

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfacePropertyInvokers (iface, iface.Properties, string.Empty, members);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfacePropertyInvokersWithSkips)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteMethodAbstractWithVoidReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetAbstract ();

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteMethodAbstractWithVoidReturn)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteMethodAsyncifiedWithIntReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar", @return: "int").SetAsyncify ();

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteMethodAsyncifiedWithIntReturn)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteMethodAsyncifiedWithVoidReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetAsyncify ();

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteMethodAsyncifiedWithVoidReturn)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteMethodBody ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar");

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethodBody (method, string.Empty, @class);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteMethodBody)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteMethodFinalWithVoidReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetFinal ();

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteMethodFinalWithVoidReturn)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteMethodIdField ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar");

			generator.WriteMethodIdField (method, string.Empty);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteMethodIdField)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteMethodProtected ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetVisibility ("protected");

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteMethodProtected)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteMethodStaticWithVoidReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetStatic ();

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteMethodStaticWithVoidReturn)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteMethodWithIntReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar", @return: "int");

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteMethodWithIntReturn)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteMethodWithStringReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar", @return: "java.lang.String");

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteMethodWithStringReturn)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteMethodWithVoidReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar");

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteMethodWithVoidReturn)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteMethodWithInvalidJavaName ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "has-hyp$hen");

			method.Name = "nohyphen";

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			var result = writer.ToString ().NormalizeLineEndings ();

			// Ensure we escape hyphens/dollar signs in callback names
			Assert.False (result.Contains ("cb_has-hyp$hen"));
			Assert.True (result.Contains ("cb_has_x45_hyp_x36_hen"));
		}

		[Test]
		public void WriteMethodWithInvalidParameterName ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "DoStuff");

			method.Parameters.Add (new Parameter ("$this", "byte[]", "byte[]", false));

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			var result = writer.ToString ().NormalizeLineEndings ();

			// Ensure we escape dollar signs
			Assert.False (result.Contains ("$this"));
		}

		[Test]
		public void WriteInterfaceExtensionsDeclaration ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceExtensionsDeclaration (iface, string.Empty, "java.code.DeclaringType");
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceExtensionsDeclaration)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceDeclaration ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceDeclaration (iface, string.Empty, new GenerationInfo (null, null, null));
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteInterfaceDeclaration)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteProperty ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.WriteProperty (@class.Properties.First (), @class, string.Empty);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteProperty)), writer.ToString ().NormalizeLineEndings ());
		}
	}

	abstract class CodeGeneratorTests : CodeGeneratorTestBase
	{
		[Test]
		public void WriteCharSequenceEnumerator ()
		{
			generator.WriteCharSequenceEnumerator (string.Empty);

			Assert.AreEqual (GetExpected (nameof (WriteCharSequenceEnumerator)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClass ()
		{
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);

			generator.Context.ContextTypes.Push (@class);
			generator.WriteType (@class, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteClass)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteClassAbstractMembers ()
		{
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);

			generator.Context.ContextTypes.Push (@class);
			generator.WriteClassAbstractMembers (@class, string.Empty);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteClassAbstractMembers)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterface ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);
			var gen_info = new GenerationInfo (null, null, null);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, gen_info);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteInterface)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceExtensionMethods ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceExtensionMethods (iface, string.Empty);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceExtensionMethods)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceEventArgs ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceEventArgs (iface, iface.Methods [0], string.Empty);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceEventArgs)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceEventHandler ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceEventHandler (iface, string.Empty);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceEventHandler)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceEventHandlerImpl ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceEventHandlerImpl (iface, string.Empty);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceEventHandlerImpl)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceEventHandlerImplContent ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);
			var handlers = new List<string> ();

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceEventHandlerImplContent (iface, iface.Methods [0], string.Empty, true, string.Empty, handlers);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (1, handlers.Count);
			Assert.AreEqual ("GetCountForKey", handlers [0]);
			Assert.AreEqual (GetExpected (nameof (WriteInterfaceEventHandlerImplContent)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteParameterListCallArgs ()
		{
			var list = SupportTypeBuilder.CreateParameterList (options);

			generator.WriteParameterListCallArgs (list, string.Empty, false);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteParameterListCallArgs)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteParameterListCallArgsForInvoker ()
		{
			var list = SupportTypeBuilder.CreateParameterList (options);

			generator.WriteParameterListCallArgs (list, string.Empty, true);

			Assert.AreEqual (GetTargetedExpected (nameof (WriteParameterListCallArgsForInvoker)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WritePropertyAbstractDeclaration ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.WritePropertyAbstractDeclaration (@class.Properties.First (), string.Empty, @class);

			Assert.AreEqual (GetExpected (nameof (WritePropertyAbstractDeclaration)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WritePropertyCallbacks ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.WritePropertyCallbacks (@class.Properties.First (), string.Empty, @class);

			Assert.AreEqual (GetExpected (nameof (WritePropertyCallbacks)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WritePropertyDeclaration ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.WritePropertyDeclaration (@class.Properties.First (), string.Empty, @class, "ObjectAdapter");

			Assert.AreEqual (GetExpected (nameof (WritePropertyDeclaration)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WritePropertyStringVariant ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.WritePropertyStringVariant (@class.Properties.First (), string.Empty);

			Assert.AreEqual (GetExpected (nameof (WritePropertyStringVariant)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WritePropertyInvoker ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.Context.ContextTypes.Push (@class);
			generator.WritePropertyInvoker (@class.Properties.First (), string.Empty, @class);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetExpected (nameof (WritePropertyInvoker)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WritePropertyExplicitInterfaceParameterName ()
		{
			// Fix a case where we were not overriding a property setter's parameter name ("p0")
			// to "value", resulting in invalid C#.  Like:
			// public string MyProperty {
			//   set => my_property = p0;
			// }
			var gens = ParseApiDefinition (@"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' final='false' name='Long' static='false' visibility='public' jni-signature='Ljava/lang/Long;' />
			  </package>

			  <package name='com.google.android.material.datepicker' jni-name='com/google/android/material/datepicker'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='SingleDateSelector' static='false' visibility='public' jni-signature='Lcom/google/android/material/datepicker/SingleDateSelector;'>
			      <implements name='com.google.android.material.datepicker.DateSelector' name-generic-aware='com.google.android.material.datepicker.DateSelector&lt;java.lang.Long&gt;' jni-type='Lcom/google/android/material/datepicker/DateSelector&lt;Ljava/lang/Long;&gt;;'>
			      </implements>
			      <method abstract='false' deprecated='not deprecated' final='false' name='getSelection' jni-signature='()Ljava/lang/Long;' bridge='false' native='false' return='java.lang.Long' jni-return='Ljava/lang/Long;' static='false' synchronized='false' synthetic='false' visibility='public'>
			      </method>
			      <method abstract='false' deprecated='not deprecated' final='false' name='setSelection' jni-signature='(Ljava/lang/Long;)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public'>
				<parameter name='selection' type='java.lang.Long' jni-type='Ljava/lang/Long;'>
				</parameter>
			      </method>
			    </class>
			    <interface abstract='true' deprecated='not deprecated' final='false' name='DateSelector' static='false' visibility='public' jni-signature='Lcom/google/android/material/datepicker/DateSelector;'>
			      <typeParameters>
				<typeParameter name='S' classBound='java.lang.Object' jni-classBound='Ljava/lang/Object;'></typeParameter>
			      </typeParameters>
			      <method abstract='true' deprecated='not deprecated' final='false' name='getSelection' jni-signature='()Ljava/lang/Object;' bridge='false' native='false' return='S' jni-return='TS;' static='false' synchronized='false' synthetic='false' visibility='public'>
			      </method>
			      <method abstract='true' deprecated='not deprecated' final='false' name='setSelection' jni-signature='(Ljava/lang/Object;)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public'>
				<parameter name='p0' type='S' jni-type='TS;'>
				</parameter>
			      </method>
			    </interface>
			  </package>
			</api>");

			var @class = gens.OfType<ClassGen> ().First (c => c.Name == "SingleDateSelector");

			generator.Context.ContextTypes.Push (@class);
			generator.WriteType (@class, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			var result = writer.ToString ().NormalizeLineEndings ();
			Assert.False (result.Contains ("p0"));
		}

		[Test]
		public void WriteClassExternalBase ()
		{
			// Tests the case where a class inherits from a class that is not in the same assembly.
			// Specifically, the internal class_ref field does NOT need the new modifier.
			//  - This prevents a CS0109 warning from being generated.

			options.SymbolTable.AddType (new TestClass (null, "Java.Lang.Object"));

			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options, "Java.Lang.Object");
			@class.Validate (options, new GenericParameterDefinitionList (), generator.Context);

			generator.Context.ContextTypes.Push (@class);
			generator.WriteType (@class, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			var result = writer.ToString ().NormalizeLineEndings ();
			Assert.True (result.Contains ("internal static IntPtr class_ref".NormalizeLineEndings ()));
			Assert.False (result.Contains ("internal static new IntPtr class_ref".NormalizeLineEndings ()));
		}

		[Test]
		public void WriteClassInternalBase ()
		{
			// Tests the case where a class inherits from Java.Lang.Object and is in the same assembly.
			// Specifically, the internal class_ref field does need the new modifier.
			// - This prevents a CS0108 warning from being generated.

			options.SymbolTable.AddType (new TestClass (null, "Java.Lang.Object"));

			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options, "Java.Lang.Object");
			@class.Validate (options, new GenericParameterDefinitionList (), generator.Context);

			// FromXml is set to true when a class is set to true when the api.xml contains an entry for the class. 
			// Therefore, if a class's base has FromXml set to true, the class and its base will be in the same C# assembly. 
			@class.BaseGen.FromXml = true;  

			generator.Context.ContextTypes.Push (@class);
			generator.WriteType (@class, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			var result = writer.ToString ().NormalizeLineEndings ();
			Assert.True (result.Contains ("internal static new IntPtr class_ref".NormalizeLineEndings ()));
			Assert.False (result.Contains ("internal static IntPtr class_ref".NormalizeLineEndings ()));
		}
	}
}
