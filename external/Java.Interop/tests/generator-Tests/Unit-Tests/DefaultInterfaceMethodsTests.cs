using System;
using System.Linq;
using MonoDroid.Generation;
using NUnit.Framework;
using Xamarin.Android.Binder;

namespace generatortests
{
#if TODO_JAVA_INTEROP1
	[TestFixture]
	class JavaInteropDefaultInterfaceMethodsTests : DefaultInterfaceMethodsTests
	{
		protected override CodeGenerationTarget Target => CodeGenerationTarget.JavaInterop1;
	}
#endif  // TODO_JAVA_INTEROP1

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
			var iface = SupportTypeBuilder.CreateEmptyInterface ("java.code.IMyInterface");

			iface.Methods.Add (new TestMethod (iface, "DoSomething").SetDefaultInterfaceMethod ());

			iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.WriteType (iface, string.Empty, new GenerationInfo (null, null, null));

			AssertTargetedExpected (nameof (WriteInterfaceDefaultMethod), writer.ToString ());
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

			generator.WriteType (iface2, string.Empty, new GenerationInfo (null, null, null));

			// IMyInterface2 should generate the method as abstract, not a default method
			AssertTargetedExpected (nameof (WriteInterfaceRedeclaredDefaultMethod), writer.ToString ());
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

			generator.WriteType (iface, string.Empty, new GenerationInfo (null, null, null));

			AssertTargetedExpected (nameof (WriteInterfaceDefaultProperty), writer.ToString ());
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

			generator.WriteType (iface, string.Empty, new GenerationInfo (null, null, null));

			AssertTargetedExpected (nameof (WriteInterfaceDefaultPropertyGetterOnly), writer.ToString ());
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
			generator.WriteType (iface, string.Empty, new GenerationInfo (null, null, null));
			generator.Context.ContextTypes.Pop ();

			AssertTargetedExpected (nameof (WriteDefaultInterfaceMethodInvoker), writer.ToString ());
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
			generator.WriteType (klass, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));
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
			generator.WriteType (klass, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));
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

			generator.WriteType (iface, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));

			AssertTargetedExpected (nameof (WriteStaticInterfaceMethod), writer.ToString ());
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

			generator.WriteType (iface, string.Empty, new GenerationInfo (null, null, null));

			AssertTargetedExpected (nameof (WriteStaticInterfaceProperty), writer.ToString ());
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

		readonly string nested_class_api = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/EmptyOverrideClass;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='true' deprecated='not deprecated' final='false' name='Parent' static='false' visibility='public' jni-signature='Lcom/xamarin/android/Parent;'>
			      <method abstract='true' deprecated='not deprecated' final='false' name='getBar' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			    </interface>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;'  final='false' name='Parent.Child' static='false' visibility='public' jni-signature='Lcom/xamarin/android/Parent$Child;' />
			  </package>
			</api>";

		[Test]
		public void WriteUnnestedInterfaceTypes ()
		{
			// Ensure we don't break the original un-nested interface types
			var gens = ParseApiDefinition (nested_interface_api);

			var parent_iface = gens.OfType<InterfaceGen> ().Single ();

			parent_iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.WriteType (parent_iface, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));

			AssertTargetedExpected (nameof (WriteUnnestedInterfaceTypes), writer.ToString ());
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

			generator.WriteType (parent_iface, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));

			AssertTargetedExpected (nameof (WriteNestedInterfaceTypes), writer.ToString ());
		}

		[Test]
		public void WriteNestedInterfaceClass ()
		{
			// Traditionally this would have created namespace.IParent and namespace.IParentChild
			// With nested types this creates namespace.IParent and namespace.IParent.IChild
			options.SupportNestedInterfaceTypes = true;

			var gens = ParseApiDefinition (nested_class_api);

			var parent_iface = gens.OfType<InterfaceGen> ().Single ();

			parent_iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.WriteType (parent_iface, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));

			AssertTargetedExpected (nameof (WriteNestedInterfaceClass), writer.ToString ());
		}

		[Test]
		public void DontWriteInterfaceConstsClass ()
		{
			// If SupportInterfaceConstants is true we no longer write the legacy
			// XXXXConsts class that has been [Obsolete (iseeror: true)] for a while.
			options.SupportInterfaceConstants = true;

			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/EmptyOverrideClass;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='true' deprecated='not deprecated' final='false' name='Parent' static='false' visibility='public' jni-signature='Lcom/xamarin/android/Parent;'>
			      <field deprecated='not deprecated' final='true' name='ACCEPT_HANDOVER' jni-signature='Ljava/lang/String;' static='true' transient='false' type='java.lang.String' type-generic-aware='java.lang.String' value='&quot;android.permission.ACCEPT_HANDOVER&quot;' visibility='public' volatile='false'></field>
			    </interface>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.OfType<InterfaceGen> ().Single ();

			iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.WriteType (iface, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));

			Assert.False (writer.ToString ().Contains ("class ParentConsts"));
		}

		[Test]
		public void ObsoleteInterfaceAlternativeClass ()
		{
			// If SupportInterfaceConstants and SupportDefaultInterfaceMethods is true we want to
			// [Obsolete] the members of the "interface alternative" class so we can eventually remove it.
			options.SupportInterfaceConstants = true;

			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/EmptyOverrideClass;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='true' deprecated='not deprecated' final='false' name='Parent' static='false' visibility='public' jni-signature='Lcom/xamarin/android/Parent;'>
			      <field deprecated='not deprecated' final='true' name='ACCEPT_HANDOVER' jni-signature='Ljava/lang/String;' static='true' transient='false' type='java.lang.String' type-generic-aware='java.lang.String' value='&quot;android.permission.ACCEPT_HANDOVER&quot;' visibility='public' volatile='false'></field>
			      <field deprecated='deprecated' final='true' name='ALREADY_OBSOLETE' jni-signature='Ljava/lang/String;' static='true' transient='false' type='java.lang.String' type-generic-aware='java.lang.String' value='&quot;android.permission.ACCEPT_HANDOVER&quot;' visibility='public' volatile='false'></field>
			      <field deprecated='not deprecated' final='true' name='API_NAME' jni-signature='Ljava/lang/String;' static='true' transient='false' type='java.lang.String' type-generic-aware='java.lang.String' visibility='public' volatile='false'></field>
			      <method abstract='false' deprecated='not deprecated' final='false' name='comparing' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='true' synchronized='false' synthetic='false' visibility='public' />
			      <method abstract='false' deprecated='deprecated' final='false' name='comparingOld' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='true' synchronized='false' synthetic='false' visibility='public' />
			    </interface>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.OfType<InterfaceGen> ().Single ();

			iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.WriteType (iface, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));

			AssertTargetedExpected (nameof (ObsoleteInterfaceAlternativeClass), writer.ToString ());
		}

		[Test]
		public void RespectNoAlternativesForInterfaces ()
		{
			// If an interface is marked with no-alternatives='true', do
			// not generate any legacy alternative classes for it
			options.SupportInterfaceConstants = true;

			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/EmptyOverrideClass;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface no-alternatives='true' abstract='true' deprecated='not deprecated' final='false' name='Parent' static='false' visibility='public' jni-signature='Lcom/xamarin/android/Parent;'>
			      <field deprecated='not deprecated' final='true' name='ACCEPT_HANDOVER' jni-signature='Ljava/lang/String;' static='true' transient='false' type='java.lang.String' type-generic-aware='java.lang.String' value='&quot;android.permission.ACCEPT_HANDOVER&quot;' visibility='public' volatile='false'></field>
			      <field deprecated='deprecated' final='true' name='ALREADY_OBSOLETE' jni-signature='Ljava/lang/String;' static='true' transient='false' type='java.lang.String' type-generic-aware='java.lang.String' value='&quot;android.permission.ACCEPT_HANDOVER&quot;' visibility='public' volatile='false'></field>
			      <field deprecated='not deprecated' final='true' name='API_NAME' jni-signature='Ljava/lang/String;' static='true' transient='false' type='java.lang.String' type-generic-aware='java.lang.String' visibility='public' volatile='false'></field>
			      <method abstract='false' deprecated='not deprecated' final='false' name='comparing' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='true' synchronized='false' synthetic='false' visibility='public' />
			      <method abstract='false' deprecated='deprecated' final='false' name='comparingOld' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='true' synchronized='false' synthetic='false' visibility='public' />
			    </interface>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.OfType<InterfaceGen> ().Single ();

			iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.WriteType (iface, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));

			Assert.False (writer.ToString ().Contains ("class ParentConsts"));
			Assert.False (writer.ToString ().Contains ("class Parent"));
		}

		[Test]
		public void DontInvalidateInterfaceDueToStaticOrDefaultMethods ()
		{
			// This interface contains a static and a default interface method that cannot
			// be bound due to an unknown return type. However the user doesn't have to
			// provide an implementation for these methods, so it's ok to bind the interface.

			var xml = @"<api>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='true' deprecated='not deprecated' final='false' name='Parent' static='false' visibility='public' jni-signature='Lcom/xamarin/android/Parent;'>
			      <method abstract='true' deprecated='not deprecated' name='normalMethod' jni-signature='()I' return='int' static='false' transient='false' visibility='public' volatile='false'></method>
			      <method abstract='false' deprecated='not deprecated' name='staticMethod' jni-signature='()Lfoo/bar/baz;' return='Lfoo/bar/baz;' static='true' transient='false' visibility='public' volatile='false'></method>
			      <method abstract='false' deprecated='not deprecated' name='defaultMethod' jni-signature='()Lfoo/bar/baz;' return='Lfoo/bar/baz;' static='false' transient='false' visibility='public' volatile='false'></method>
			    </interface>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.OfType<InterfaceGen> ().Single ();

			var result = iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			// Inteface should pass validation despite invalid static/default methods
			Assert.True (result);

			generator.WriteType (iface, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));

			var generated = writer.ToString ();

			Assert.True (generated.Contains ("interface IParent"));
			Assert.True (generated.Contains ("NormalMethod"));
			Assert.False (generated.Contains ("StaticMethod"));
			Assert.False (generated.Contains ("DefaultMethod"));
		}

		[Test]
		public void GenerateProperNestedInterfaceSignatures ()
		{
			// https://github.com/dotnet/java-interop/issues/661
			// Ensure that when we write the invoker type for a nested default interface method
			// we use `/` to denote nested as needed by Type.GetType ()
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/EmptyOverrideClass;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class extends='java.lang.Object' abstract='true' deprecated='not deprecated' final='false' name='Application' static='true' visibility='public' jni-signature='Landroid/app/Application$ActivityLifecycleCallbacks;' />
			    <interface abstract='true' deprecated='not deprecated' final='false' name='Application.ActivityLifecycleInterface' static='true' visibility='public' jni-signature='Landroid/app/Application$ActivityLifecycleCallbacks;'>
			      <method abstract='false' deprecated='not deprecated' final='false' name='onActivityDestroyed' jni-signature='(Landroid/app/Activity;)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public'>
			        <parameter name='activity' type='int' jni-type='I' not-null='true'></parameter>
			      </method>
			    </interface>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens[1].NestedTypes.OfType<InterfaceGen> ().Single ();

			generator.WriteType (iface, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));

			var generated = writer.ToString ();

			Assert.True (generated.Contains ("GetOnActivityDestroyed_IHandler:Com.Xamarin.Android.Application/IActivityLifecycleInterface, MyAssembly"));
			Assert.False (generated.Contains ("GetOnActivityDestroyed_IHandler:Com.Xamarin.Android.Application.IActivityLifecycleInterface, MyAssembly"));
		}

		[Test]
		public void WriteInterfaceFieldAsDimProperty ()
		{
			// Ensure we write interface fields that are not constant, and thus must be written as properties
			var xml = @"<api>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='true' deprecated='not deprecated' final='false' name='MyInterface' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyInterface;'>
			      <field deprecated='not deprecated' final='true' name='EGL_NATIVE_VISUAL_ID' jni-signature='I' static='true' transient='false' type='int' type-generic-aware='int' value='12334' visibility='public' volatile='false'></field>
			      <field deprecated='not deprecated' final='false' name='EGL_NO_SURFACE' jni-signature='I' static='true' transient='false' type='int' type-generic-aware='int' visibility='public' volatile='false'></field>
			    </interface>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.OfType<InterfaceGen> ().Single ();

			var result = iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());
			Assert.True (result);

			generator.WriteType (iface, string.Empty, new GenerationInfo (string.Empty, string.Empty, "MyAssembly"));

			var generated = writer.ToString ();

			AssertTargetedExpected (nameof (WriteInterfaceFieldAsDimProperty), writer.ToString ());
		}

		[Test]
		public void CompatVirtualMethod_Interface ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='true' deprecated='not deprecated' final='false' name='Cursor' static='false' visibility='public' jni-signature='Landroid/database/Cursor;'>
			      <method abstract='true' deprecated='not deprecated' final='false' name='getNotificationUri' jni-signature='()Ljava/lang/Object;' bridge='false' native='false' return='java.lang.Object' jni-return='Ljava/lang/Object;' static='false' synchronized='false' synthetic='false' visibility='public' compatVirtualMethod='true' />
			    </interface>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var klass = gens.Single (g => g.Name == "ICursor");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.True (writer.ToString ().NormalizeLineEndings ().Contains ("catch (Java.Lang.NoSuchMethodError) { throw new Java.Lang.AbstractMethodError (__id); }".NormalizeLineEndings ()), $"was: `{writer}`");
		}

		[Test]
		public void FixDefaultInterfaceMethodStackOverflow ()
		{
			// The bug was that this causes a stack overflow, but this also tests
			// that the OverriddenInterfaceMethod is now correctly set in the interface method.
			var xml = """
			<api>
			  <package name='io.grpc' jni-name='io/grpc'>
			    <interface abstract="true" deprecated="not deprecated" final="false" name="InternalConfigurator" static="false" visibility="public" jni-signature="Lio/grpc/InternalConfigurator;">
			      <implements name="io.grpc.Configurator" name-generic-aware="io.grpc.Configurator" jni-type="Lio/grpc/Configurator;" />
			    </interface>
			    <interface abstract="true" deprecated="not deprecated" final="false" name="Configurator" static="false" visibility="" jni-signature="Lio/grpc/Configurator;">
			      <method abstract="false" deprecated="not deprecated" final="false" name="configureChannelBuilder" jni-signature="V" bridge="false" native="false" return="void" jni-return="V" static="false" synchronized="false" synthetic="false" visibility="public" />
			    </interface>
			  </package>
			</api>
			""";

			var gens = ParseApiDefinition (xml);

			foreach (var iface in gens.OfType<InterfaceGen> ()) {
				generator.Context.ContextTypes.Push (iface);
				generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
				generator.Context.ContextTypes.Pop ();
			}

			var klass1 = gens.Single (g => g.Name == "IInternalConfigurator");
			var klass2 = gens.Single (g => g.Name == "IConfigurator");

			Assert.AreEqual (1, klass1.Methods.Count);
			Assert.AreEqual (1, klass2.Methods.Count);

			Assert.AreNotSame (klass1.Methods [0], klass2.Methods [0]);
			Assert.AreSame (klass1.Methods [0].OverriddenInterfaceMethod, klass2.Methods [0]);
		}
	}
}
