using System;
using System.Linq;
using MonoDroid.Generation;
using NUnit.Framework;
using Xamarin.Android.Binder;

namespace generatortests
{
	[TestFixture]
	class JavaInteropDefaultInterfaceMethodsTests : DefaultInterfaceMethodsTests
	{
		protected override CodeGenerationTarget Target => CodeGenerationTarget.JavaInterop1;
	}

	[TestFixture]
	class XAJavaInteropDefaultInterfaceMethodsTests : DefaultInterfaceMethodsTests
	{
		protected override CodeGenerationTarget Target => CodeGenerationTarget.XAJavaInterop1;
	}

	abstract class DefaultInterfaceMethodsTests : CodeGeneratorTestBase
	{
		protected override CodeGenerationOptions CreateOptions ()
		{
			var options = base.CreateOptions ();

			options.AssemblyName = "MyAssembly";
			options.SupportDefaultInterfaceMethods = true;

			return options;
		}

		[Test]
		public void WriteInterfaceDefaultMethod ()
		{
			// Create an interface with a default method
			var iface = SupportTypeBuilder.CreateEmptyInterface("java.code.IMyInterface");

			iface.Methods.Add (new TestMethod (iface, "DoSomething").SetDefaultInterfaceMethod ());

			iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.WriteInterfaceDeclaration (iface, string.Empty, new GenerationInfo (null, null, null));

			Assert.AreEqual (GetTargetedExpected (nameof (WriteInterfaceDefaultMethod)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceRedeclaredDefaultMethod ()
		{
			// Create an interface with a default method
			var iface = SupportTypeBuilder.CreateEmptyInterface ("java.code.IMyInterface");
			iface.Methods.Add (new TestMethod (iface, "DoSomething").SetDefaultInterfaceMethod ());
			options.SymbolTable.AddType (iface);

			// Create a second interface that inherits the first, declaring the method as not default
			var iface2 = SupportTypeBuilder.CreateEmptyInterface ("java.code.IMyInterface2");
			iface2.AddImplementedInterface ("java.code.IMyInterface");
			iface2.Methods.Add (new TestMethod (iface, "DoSomething"));

			iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());
			iface2.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.WriteInterfaceDeclaration (iface2, string.Empty, new GenerationInfo (null, null, null));

			// IMyInterface2 should generate the method as abstract, not a default method
			Assert.AreEqual (GetExpected (nameof (WriteInterfaceRedeclaredDefaultMethod)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceDefaultProperty ()
		{
			// Create an interface with a default method
			var iface = SupportTypeBuilder.CreateEmptyInterface ("java.code.IMyInterface");
			var prop = SupportTypeBuilder.CreateProperty (iface, "Value", "int", options);

			prop.Getter.IsInterfaceDefaultMethod = true;
			prop.Setter.IsInterfaceDefaultMethod = true;

			iface.Properties.Add (prop);

			iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.WriteInterfaceDeclaration (iface, string.Empty, new GenerationInfo (null, null, null));

			Assert.AreEqual (GetTargetedExpected (nameof (WriteInterfaceDefaultProperty)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceDefaultPropertyGetterOnly ()
		{
			// Create an interface with a default method
			var iface = SupportTypeBuilder.CreateEmptyInterface ("java.code.IMyInterface");
			var prop = SupportTypeBuilder.CreateProperty (iface, "Value", "int", options);

			prop.Getter.IsInterfaceDefaultMethod = true;
			prop.Setter = null;

			iface.Properties.Add (prop);

			iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.WriteInterfaceDeclaration (iface, string.Empty, new GenerationInfo (null, null, null));

			Assert.AreEqual (GetTargetedExpected (nameof (WriteInterfaceDefaultPropertyGetterOnly)), writer.ToString ().NormalizeLineEndings ());
		}


		[Test]
		public void WriteDefaultInterfaceMethodInvoker ()
		{
			// Create an interface with a default method
			var iface = SupportTypeBuilder.CreateEmptyInterface ("java.code.IMyInterface");

			iface.Methods.Add (new TestMethod (iface, "DoDeclaration"));
			iface.Methods.Add (new TestMethod (iface, "DoDefault").SetDefaultInterfaceMethod ());

			iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceInvoker (iface, string.Empty);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteDefaultInterfaceMethodInvoker)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteSealedOverriddenDefaultMethod ()
		{
			// Create an interface with a default method
			var iface = SupportTypeBuilder.CreateEmptyInterface ("java.code.IMyInterface");
			iface.Methods.Add (new TestMethod (iface, "DoSomething").SetDefaultInterfaceMethod ());
			options.SymbolTable.AddType (iface);

			// Create a type that inherits the interface, overriding the method as final
			var klass = new TestClass ("java.code.IMyInterface", "java.code.MyClass");
			klass.AddImplementedInterface ("java.code.IMyInterface");
			klass.Methods.Add (new TestMethod (iface, "DoSomething").SetFinal ());

			iface.Validate (options, new GenericParameterDefinitionList (), generator.Context);
			klass.Validate (options, new GenericParameterDefinitionList (), generator.Context);

			klass.FixupMethodOverrides (options);

			generator.Context.ContextTypes.Push (klass);
			generator.WriteClass (klass, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// The method should not be marked as 'virtual sealed'
			Assert.False (writer.ToString ().Contains ("virtual sealed"));
		}

		[Test]
		public void WriteInterfaceRedeclaredChainDefaultMethod ()
		{
			// Fix a case where a property declared in this hierarchy was generated as "override" instead of "virtual"
			// public interface MyInterface { default int getValue () { return 0; } }
			// public class MyClass implements MyInterface { }
			// public class MySecondClass extends MyClass { @Override public int getValue () { return 1; } }
			var gens = ParseApiDefinition (@"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/EmptyOverrideClass;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='true' deprecated='not deprecated' final='false' name='DefaultMethodsInterface' static='false' visibility='public' jni-signature='Lcom/xamarin/android/DefaultMethodsInterface;'>
			      <method abstract='false' deprecated='not deprecated' final='false' name='foo' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			      <method abstract='false' deprecated='not deprecated' final='false' name='getBar' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			      <method abstract='false' deprecated='not deprecated' final='false' name='setBar' jni-signature='(I)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public'>
				<parameter name='p0' type='int' jni-type='I'></parameter>
			      </method>
			    </interface>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='EmptyOverrideClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/EmptyOverrideClass;'>
			      <implements name='com.xamarin.android.DefaultMethodsInterface' name-generic-aware='com.xamarin.android.DefaultMethodsInterface' jni-type='Lcom/xamarin/android/DefaultMethodsInterface;'></implements>
			      <constructor deprecated='not deprecated' final='false' name='EmptyOverrideClass' jni-signature='()V' bridge='false' static='false' type='com.xamarin.android.EmptyOverrideClass' synthetic='false' visibility='public'></constructor>
			    </class>
			    <class abstract='false' deprecated='not deprecated' extends='com.xamarin.android.EmptyOverrideClass' extends-generic-aware='com.xamarin.android.EmptyOverrideClass' jni-extends='Lcom/xamarin/android/EmptyOverrideClass;' final='false' name='ImplementedChainOverrideClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/ImplementedChainOverrideClass;'>
			      <constructor deprecated='not deprecated' final='false' name='ImplementedChainOverrideClass' jni-signature='()V' bridge='false' static='false' type='com.xamarin.android.ImplementedChainOverrideClass' synthetic='false' visibility='public'></constructor>
			      <method abstract='false' deprecated='not deprecated' final='false' name='getBar' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			      <method abstract='false' deprecated='not deprecated' final='false' name='setBar' jni-signature='(I)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public'>
				<parameter name='p0' type='int' jni-type='I'></parameter>
			      </method>
			    </class>
			  </package>
			</api>");

			var klass = (ClassGen)gens.First (g => g.Name == "ImplementedChainOverrideClass");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteClass (klass, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.True (writer.ToString ().Contains ("public virtual unsafe int Bar"));
		}

		[Test]
		public void WriteStaticInterfaceMethod ()
		{
			// Create an interface with a static method
			var iface = SupportTypeBuilder.CreateEmptyInterface ("java.code.IMyInterface");
			iface.Methods.Add (new TestMethod (iface, "DoSomething").SetStatic ());

			iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.WriteInterface (iface, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));

			Assert.AreEqual (GetTargetedExpected (nameof (WriteStaticInterfaceMethod)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteStaticInterfaceProperty ()
		{
			// Create an interface with a static property
			var iface = SupportTypeBuilder.CreateEmptyInterface ("java.code.IMyInterface");
			var prop = SupportTypeBuilder.CreateProperty (iface, "Value", "int", options);

			prop.Getter.IsStatic = true;
			prop.Getter.IsVirtual = false;
			prop.Setter.IsStatic = true;
			prop.Setter.IsVirtual = false;

			iface.Properties.Add (prop);

			iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.WriteInterfaceDeclaration (iface, string.Empty, new GenerationInfo (null, null, null));

			Assert.AreEqual (GetTargetedExpected (nameof (WriteStaticInterfaceProperty)), writer.ToString ().NormalizeLineEndings ());
		}

		readonly string nested_interface_api = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/EmptyOverrideClass;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='true' deprecated='not deprecated' final='false' name='Parent' static='false' visibility='public' jni-signature='Lcom/xamarin/android/Parent;'>
			      <method abstract='true' deprecated='not deprecated' final='false' name='getBar' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			    </interface>
			    <interface abstract='true' deprecated='not deprecated' final='false' name='Parent.Child' static='false' visibility='public' jni-signature='Lcom/xamarin/android/Parent$Child;'>
			      <method abstract='true' deprecated='not deprecated' final='false' name='getBar' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			    </interface>
			  </package>
			</api>";

		[Test]
		public void WriteUnnestedInterfaceTypes ()
		{
			// Ensure we don't break the original un-nested interface types
			var gens = ParseApiDefinition (nested_interface_api);

			var parent_iface = gens.OfType<InterfaceGen> ().Single ();

			parent_iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.WriteInterface (parent_iface, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));

			Assert.AreEqual (GetTargetedExpected (nameof (WriteUnnestedInterfaceTypes)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteNestedInterfaceTypes ()
		{
			// Traditionally this would have created namespace.IParent and namespace.IParentChild
			// With nested types this creates namespace.IParent and namespace.IParent.IChild
			options.SupportNestedInterfaceTypes = true;

			var gens = ParseApiDefinition (nested_interface_api);

			var parent_iface = gens.OfType<InterfaceGen> ().Single ();

			parent_iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.WriteInterface (parent_iface, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));

			Assert.AreEqual (GetTargetedExpected (nameof (WriteNestedInterfaceTypes)), writer.ToString ().NormalizeLineEndings ());
		}
	}
}
