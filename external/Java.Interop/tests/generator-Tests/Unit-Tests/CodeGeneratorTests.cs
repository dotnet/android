using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using generator.SourceWriters;
using Java.Interop.Tools.Generator;
using MonoDroid.Generation;
using NUnit.Framework;
using Xamarin.Android.Binder;
using Xamarin.SourceWriter;

namespace generatortests
{
	abstract class AnyJavaInteropCodeGeneratorTests : CodeGeneratorTests
	{
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

			AssertOriginalTargetExpected (nameof (WriteKotlinUnsignedTypeMethodsClass), writer.ToString ());
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

			AssertOriginalTargetExpected (nameof (WriteKotlinUnsignedTypePropertiesClass), writer.ToString ());
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

			AssertOriginalTargetExpected (nameof (WriteKotlinUnsignedArrayTypeMethodsClass), writer.ToString ());
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

			AssertOriginalTargetExpected (nameof (WriteKotlinUnsignedArrayTypePropertiesClass), writer.ToString ());
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

			Assert.True (writer.ToString ().Contains ("public virtual unsafe int DoStuff ()"), $"was: `{writer.ToString ()}`");
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

			Assert.True (writer.ToString ().Contains ("public override unsafe int DoStuff ()"), $"was: `{writer.ToString ()}`");
		}

		[Test]
		public void ManagedOverrideMethod_None ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='not deprecated' final='false' name='DoStuff' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' managedOverride='none'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var klass = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// This would contain 'virtual' if the 'managedOverride' was not working
			Assert.True (writer.ToString ().Contains ("public unsafe int DoStuff ()"), $"was: `{writer}`");
		}

		[Test]
		public void ManagedOverrideInterfaceMethod_Reabstract ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyInterface' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyInterface;'>
			      <method abstract='true' deprecated='not deprecated' final='false' name='DoStuff' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' managedOverride='reabstract'></method>
			    </interface>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "IMyInterface");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// This would not contain 'abstract' if the 'managedOverride' was not working
			Assert.True (writer.ToString ().Contains ("abstract int DoStuff ()"), $"was: `{writer}`");
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

			Assert.True (writer.ToString ().Contains ("public virtual unsafe int Name {"), $"was: `{writer.ToString ()}`");
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

			Assert.True (writer.ToString ().Contains ("public override unsafe int Name {"), $"was: `{writer.ToString ()}`");
		}

		[Test]
		public void ManagedOverrideProperty_None ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='not deprecated' final='false' name='getName' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' managedOverride='none'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var klass = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// This would contain 'virtual' if the 'managedOverride' was not working
			Assert.True (writer.ToString ().Contains ("public unsafe int Name {"), $"was: `{writer}`");
		}

		[Test]
		public void ManagedOverrideInterfaceProperty_Reabstract ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyInterface' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyInterface;'>
			      <method abstract='true' deprecated='not deprecated' final='false' name='getName' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' managedOverride='reabstract'></method>
			    </interface>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "IMyInterface");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// This would not contain 'abstract' if the 'managedOverride' was not working
			Assert.True (writer.ToString ().Contains ("abstract int Name {"), $"was: `{writer}`");
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

			AssertOriginalExpected (nameof (WriteDuplicateInterfaceEventArgs), writer.ToString ());
		}

		[Test]
		public void InheritedInterfaceAsClass ()
		{
			// This is a somewhat cheating way to repro a real issue.
			// The real issue is:
			// - Binding an interface which implements 'android.provider.BaseColumns'
			// - android.provider.BaseColumns is an interface in android.jar
			// - Mono.Android.dll has both:
			//   - [Register ("android.provider.BaseColumns")] public abstract class BaseColumns
			//   - [Register ("android.provider.BaseColumns")] public interface IBaseColumns
			// Our Java type resolution is "last one wins" and happens to pick the class instead of
			// the interface. So our code is trying to bind an interface that implements a class.
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' final='false' name='MyConsts' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			    <interface abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <implements name='com.xamarin.android.MyConsts' name-generic-aware='com.xamarin.android.MyConsts' jni-type='Lcom/xamarin/android/MyConsts;'></implements>
			    </interface>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);

			// Disable "BuildingCoreAssembly"
			(gens.Single (g => g.Name == "Object") as ClassGen).FromXml = false;

			var klass = gens.Single (g => g.Name == "IMyClass");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.Pass ("WriteType did not NRE");
		}

		[Test]
		public void ExplicitInterfaceMetadata_InterfaceMethod ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyInterface' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyInterface;'>
			      <method abstract='true' deprecated='not deprecated' final='false' name='countAffectedRows' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' explicitInterface='IRowCounter'></method>
			    </interface>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "IMyInterface");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// Ensure explicit interface was written
			Assert.True (writer.ToString ().Contains ("int IRowCounter.CountAffectedRows ("), $"was: `{writer}`");
		}

		[Test]
		public void ExplicitInterfaceMetadata_InterfaceProperty ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyInterface' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyInterface;'>
			      <method abstract='true' deprecated='not deprecated' final='false' name='getAge' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' explicitInterface='IHasAge'></method>
			    </interface>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "IMyInterface");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// Ensure explicit interface was written
			Assert.True (writer.ToString ().Contains ("int IHasAge.Age {"), $"was: `{writer}`");
		}

		[Test]
		public void ExplicitInterfaceMetadata_ClassMethod ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='not deprecated' final='false' name='countAffectedRows' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' explicitInterface='IRowCounter'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// Ensure explicit interface was written
			Assert.True (writer.ToString ().Contains ("int IRowCounter.CountAffectedRows ("), $"was: `{writer}`");
		}

		[Test]
		public void ExplicitInterfaceMetadata_ClassProperty ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='not deprecated' final='false' name='getAge' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' explicitInterface='IHasAge'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// Ensure explicit interface was written
			Assert.True (writer.ToString ().Contains ("int IHasAge.Age {"), $"was: `{writer}`");
		}

		[Test]
		public void ExplicitInterfaceMetadata_AbstractClassMethod ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='true' deprecated='not deprecated' final='false' name='countAffectedRows' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' explicitInterface='IRowCounter'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// Ensure explicit interface was written
			Assert.True (writer.ToString ().Contains ("abstract int IRowCounter.CountAffectedRows ("), $"was: `{writer}`");
		}

		[Test]
		public void ExplicitInterfaceMetadata_AbstractClassProperty ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='true' deprecated='not deprecated' final='false' name='getAge' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' explicitInterface='IHasAge'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// Ensure explicit interface was written
			Assert.True (writer.ToString ().Contains ("abstract int IHasAge.Age {"), $"was: `{writer}`");
		}

		[Test]
		public void ObsoleteBoundMethodAbstractDeclaration ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='true' deprecated='This is so old!' final='false' name='countAffectedRows' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// Ensure [Obsolete] was written
			Assert.True (writer.ToString ().Contains ("[global::System.Obsolete (@\"This is so old!\")]"), writer.ToString ());
		}

		[Test]
		public void ObsoletedOSPlatformAttributeSupport ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='This is a class deprecated since 25!' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;' deprecated-since='25'>
			      <field deprecated='This is a field deprecated since 25!' final='true' name='ACCEPT_HANDOVER' jni-signature='Ljava/lang/String;' static='true' transient='false' type='java.lang.String' type-generic-aware='java.lang.String' value='&quot;android.permission.ACCEPT_HANDOVER&quot;' visibility='public' volatile='false' deprecated-since='25'></field>
			      <constructor deprecated='This is a constructor deprecated since 25!' final='false' name='MyClass' jni-signature='()V' bridge='false' static='false' type='com.xamarin.android.MyClass' synthetic='false' visibility='public' deprecated-since='25'></constructor>
			      <method abstract='true' deprecated='This is a method deprecated since 25!' final='false' name='countAffectedRows' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' deprecated-since='25'></method>
			      <method abstract='false' deprecated='This is a property getter deprecated since 25!' final='false' name='getCount' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' deprecated-since='25'></method>
			      <method abstract='false' deprecated='This is a property setter deprecated since 25!' final='false' name='setCount' jni-signature='(I)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public' deprecated-since='25'>
					<parameter name='count' type='int' jni-type='I'></parameter>
				  </method>
			    </class>
			  </package>
			</api>";

			options.UseObsoletedOSPlatformAttributes = true;

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// Ensure [ObsoletedOSPlatform] was written
			Assert.True (writer.ToString ().Contains ("[global::System.Runtime.Versioning.ObsoletedOSPlatform (\"android25.0\", @\"This is a class deprecated since 25!\")]"), writer.ToString ());
			Assert.True (writer.ToString ().Contains ("[global::System.Runtime.Versioning.ObsoletedOSPlatform (\"android25.0\", @\"This is a field deprecated since 25!\")]"), writer.ToString ());
			Assert.True (writer.ToString ().Contains ("[global::System.Runtime.Versioning.ObsoletedOSPlatform (\"android25.0\", @\"This is a constructor deprecated since 25!\")]"), writer.ToString ());
			Assert.True (writer.ToString ().Contains ("[global::System.Runtime.Versioning.ObsoletedOSPlatform (\"android25.0\", @\"This is a method deprecated since 25!\")]"), writer.ToString ());
			Assert.True (writer.ToString ().Contains ("[global::System.Runtime.Versioning.ObsoletedOSPlatform (\"android25.0\", @\"This is a property getter deprecated since 25!\")]"), writer.ToString ());
			Assert.True (writer.ToString ().Contains ("[global::System.Runtime.Versioning.ObsoletedOSPlatform (\"android25.0\", @\"This is a property setter deprecated since 25!\")]"), writer.ToString ());
		}

		[Test]
		public void ObsoletedOSPlatformAttributeUnneededSupport ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='This is a class deprecated since 28!' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;' deprecated-since='28'>
			      <field deprecated='This is a field deprecated since 0!' final='true' name='ACCEPT_HANDOVER' jni-signature='Ljava/lang/String;' static='true' transient='false' type='java.lang.String' type-generic-aware='java.lang.String' value='&quot;android.permission.ACCEPT_HANDOVER&quot;' visibility='public' volatile='false' deprecated-since='0'></field>
			      <constructor deprecated='This is a constructor deprecated since empty string!' final='false' name='MyClass' jni-signature='()V' bridge='false' static='false' type='com.xamarin.android.MyClass' synthetic='false' visibility='public' deprecated-since=''></constructor>
			      <method abstract='true' deprecated='deprecated' final='false' name='countAffectedRows' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' deprecated-since='25'></method>
			      <method abstract='true' deprecated='This method has an invalid deprecated-since!' final='false' name='countAffectedRows2' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' deprecated-since='foo'></method>
			      <method abstract='false' deprecated='deprecated' final='false' name='getCount' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' deprecated-since='22'></method>
			      <method abstract='false' deprecated='deprecated' final='false' name='setCount' jni-signature='(I)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public' deprecated-since='22'>
					<parameter name='count' type='int' jni-type='I'></parameter>
				  </method>
			    </class>
			  </package>
			</api>";

			options.UseObsoletedOSPlatformAttributes = true;

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// These should use [Obsolete] because they have always been obsolete in all currently supported versions (21+)
			Assert.True (writer.ToString ().Contains ("[global::System.Obsolete (@\"This is a field deprecated since 0!\")]"), writer.ToString ());
			Assert.True (writer.ToString ().Contains ("[global::System.Obsolete (@\"This is a constructor deprecated since empty string!\")]"), writer.ToString ());

			// This should not have a message because the default "deprecated" message isn't useful
			Assert.True (writer.ToString ().Contains ("[global::System.Runtime.Versioning.ObsoletedOSPlatform (\"android25.0\")]"), writer.ToString ());
			Assert.True (writer.ToString ().Contains ("[global::System.Runtime.Versioning.ObsoletedOSPlatform (\"android22.0\")]"), writer.ToString ());

			// This should use [Obsolete] because the 'deprecated-since' attribute could not be parsed
			Assert.True (writer.ToString ().Contains ("[global::System.Obsolete (@\"This method has an invalid deprecated-since!\")]"), writer.ToString ());
		}

		[Test]
		public void ObsoleteGetterOnlyProperty ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='deprecated' final='false' name='getCount' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// This should use [Obsolete] on the entire property because the getter is obsolete and there is no setter
			Assert.True (StripRegisterAttributes (writer.ToString ()).NormalizeLineEndings ().Contains ("[global::System.Obsolete (@\"deprecated\")]public virtual unsafe int Count".NormalizeLineEndings ()), writer.ToString ());

			// Ensure we don't write getter attribute
			Assert.False (StripRegisterAttributes (writer.ToString ()).NormalizeLineEndings ().Contains ("[global::System.Obsolete(@\"deprecated\")]get"), writer.ToString ());
		}

		[Test]
		public void ObsoletePropertyGetter ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='deprecated' final='false' name='getCount' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			      <method abstract='false' deprecated='not deprecated' final='false' name='setCount' jni-signature='(I)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public'>
				<parameter name='count' type='int' jni-type='I'></parameter>
			      </method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// This should use [Obsolete] on just the property getter since the setter is not obsolete
			Assert.True (StripRegisterAttributes (writer.ToString ()).NormalizeLineEndings ().Contains ("[global::System.Obsolete(@\"deprecated\")]get"), writer.ToString ());
		}

		[Test]
		public void ObsoletePropertySetter ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='not deprecated' final='false' name='getCount' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			      <method abstract='false' deprecated='deprecated' final='false' name='setCount' jni-signature='(I)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public'>
				<parameter name='count' type='int' jni-type='I'></parameter>
			      </method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// This should use [Obsolete] on just the property setter since the getter is not obsolete
			Assert.True (StripRegisterAttributes (writer.ToString ()).NormalizeLineEndings ().Contains ("[global::System.Obsolete(@\"deprecated\")]set"), writer.ToString ());
		}

		[Test]
		public void ObsoleteBothPropertyMethods ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='getter_message' final='false' name='getCount' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			      <method abstract='false' deprecated='setter_message' final='false' name='setCount' jni-signature='(I)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public'>
				<parameter name='count' type='int' jni-type='I'></parameter>
			      </method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// This should use [Obsolete] on both property methods because the deprecation messages are different
			Assert.True (StripRegisterAttributes (writer.ToString ()).NormalizeLineEndings ().Contains ("[global::System.Obsolete(@\"getter_message\")]get"), writer.ToString ());
			Assert.True (StripRegisterAttributes (writer.ToString ()).NormalizeLineEndings ().Contains ("[global::System.Obsolete(@\"setter_message\")]set"), writer.ToString ());
		}

		[Test]
		public void ObsoleteEntireProperty ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='deprecated' final='false' name='getCount' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			      <method abstract='false' deprecated='deprecated' final='false' name='setCount' jni-signature='(I)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public'>
				<parameter name='count' type='int' jni-type='I'></parameter>
			      </method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// This should use [Obsolete] on the entire property because the getter and setter are both obsoleted with the same message
			Assert.True (StripRegisterAttributes (writer.ToString ()).NormalizeLineEndings ().Contains ("[global::System.Obsolete (@\"deprecated\")]public virtual unsafe int Count".NormalizeLineEndings ()), writer.ToString ());

			// Ensure we don't write getter/setter attributes
			Assert.False (StripRegisterAttributes (writer.ToString ()).NormalizeLineEndings ().Contains ("[global::System.Obsolete(@\"deprecated\")]get"), writer.ToString ());
			Assert.False (StripRegisterAttributes (writer.ToString ()).NormalizeLineEndings ().Contains ("[global::System.Obsolete(@\"deprecated\")]set"), writer.ToString ());
		}

		[Test]
		[NonParallelizable]     // We are setting a static property on Report
		public void WarnIfTypeNameMatchesNamespace ()
		{
			var @class = new TestClass ("Object", "java.myclass.MyClass");
			var sb = new StringBuilder ();

			var write_output = new Action<TraceLevel, string> ((t, s) => { sb.AppendLine (s); });
			Report.OutputDelegate = write_output;

			generator.Context.ContextTypes.Push (@class);
			generator.WriteType (@class, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Report.OutputDelegate = null;

			// Ensure the warning was raised
			Assert.True (sb.ToString ().Contains ("warning BG8403"));
		}

		[Test]
		[NonParallelizable]     // We are setting a static property on Report
		public void DontWarnIfNestedTypeNameMatchesNamespace ()
		{
			var @class = new TestClass ("Object", "java.myclass.MyParentClass");
			@class.NestedTypes.Add (new TestClass ("Object", "java.myclass.MyParentClass.MyClass"));
			var sb = new StringBuilder ();

			var write_output = new Action<TraceLevel, string> ((t, s) => { sb.AppendLine (s); });
			Report.OutputDelegate = write_output;

			generator.Context.ContextTypes.Push (@class);
			generator.WriteType (@class, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Report.OutputDelegate = null;

			// The warning should not be raised if the nested type matches enclosing namespace
			Assert.False (sb.ToString ().Contains ("warning BG8403"));
		}

		static string StripRegisterAttributes (string str)
		{
			// It is hard to test if the [Obsolete] is on the setter/etc due to the [Register], so remove all [Register]s
			// [global::System.Obsolete (@"setter_message")]
			// [Register ("setCount", "(I)V", "GetSetCount_IHandler")]
			// set {
			int index;

			while ((index = str.IndexOf ("[Register", StringComparison.Ordinal)) > -1)
				str = str.Substring (0, index) + str.Substring (str.IndexOf (']', index) + 1);

			return str;
		}
	}

	[TestFixture]
	class JavaInteropCodeGeneratorTests : AnyJavaInteropCodeGeneratorTests
	{
		protected override CodeGenerationTarget Target => CodeGenerationTarget.JavaInterop1;
		protected override string  CommonDirectoryOverride => "JavaInterop1";

		protected override CodeGenerationOptions CreateOptions ()
		{
			var options = new CodeGenerationOptions {
				CodeGenerationTarget            = Target,
				SupportDefaultInterfaceMethods	= true,
				SupportInterfaceConstants       = true,
				SupportNestedInterfaceTypes     = true,
				SupportNullableReferenceTypes   = true,
			};
			return options;
		}
	}

	[TestFixture]
	class XAJavaInteropCodeGeneratorTests : AnyJavaInteropCodeGeneratorTests
	{
		protected override CodeGenerationTarget Target => CodeGenerationTarget.XAJavaInterop1;

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

		[Test]
		public void SupportedOSPlatform ()
		{
			// We do not write [SupportedOSPlatform] for JavaInterop, only XAJavaInterop
			var klass = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);
			klass.ApiAvailableSince = 30;

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			StringAssert.Contains ("[global::System.Runtime.Versioning.SupportedOSPlatformAttribute (\"android30.0\")]", builder.ToString (), "Should contain SupportedOSPlatform!");
		}

		[Test]
		public void SupportedOSPlatformConstFields ()
		{
			// Ensure we write [SupportedOSPlatform] for const fields
			var klass = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar").SetConstant ("MY_VALUE");

			field.ApiAvailableSince = 30;

			klass.Fields.Add (field);

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			StringAssert.Contains ("[global::System.Runtime.Versioning.SupportedOSPlatformAttribute (\"android30.0\")]", builder.ToString (), "Should contain SupportedOSPlatform!");
		}
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

			AssertTargetedExpected (nameof (WriteClassConstructors), writer.ToString ());
		}

		[Test]
		public void WriteClassHandle ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");

			generator.WriteClassHandle (@class, string.Empty, false);

			AssertTargetedExpected (nameof (WriteClassHandle), writer.ToString ());
		}

		[Test]
		public void WriteClassInvoker ()
		{
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);

			generator.Context.ContextTypes.Push (@class);
			generator.WriteClassInvoker (@class, string.Empty);
			generator.Context.ContextTypes.Pop ();

			AssertTargetedExpected (nameof (WriteClassInvoker), writer.ToString ());
		}

		[Test]
		public void WriteClassInvokerHandle ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");

			generator.WriteClassInvokerHandle (@class, string.Empty, "Com.MyPackage.Foo");

			AssertTargetedExpected (nameof (WriteClassInvokerHandle), writer.ToString ());
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

			AssertTargetedExpected (nameof (WriteClassInvokerMembers), writer.ToString ());
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

			AssertTargetedExpected (nameof (WriteClassMethodInvokers), writer.ToString ());
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

			AssertTargetedExpected (nameof (WriteClassMethodInvokersWithSkips), writer.ToString ());
		}

		[Test]
		public void WriteClassMethods ()
		{
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);

			generator.Context.ContextTypes.Push (@class);
			generator.WriteClassMethods (@class, string.Empty);
			generator.Context.ContextTypes.Pop ();

			AssertTargetedExpected (nameof (WriteClassMethods), writer.ToString ());
		}

		[Test]
		public void WriteClassProperties ()
		{
			var @class = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);

			generator.Context.ContextTypes.Push (@class);
			generator.WriteImplementedProperties (@class.Properties, string.Empty, @class.IsFinal, @class);
			generator.Context.ContextTypes.Pop ();

			AssertTargetedExpected (nameof (WriteClassProperties), writer.ToString ());
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

			AssertTargetedExpected (nameof (WriteClassPropertyInvokers), writer.ToString ());
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

			AssertTargetedExpected (nameof (WriteClassPropertyInvokersWithSkips), writer.ToString ());
		}

		[Test]
		public void WriteCtor ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var ctor = new TestCtor (@class, "Object");

			generator.Context.ContextTypes.Push (@class);
			generator.WriteConstructor (ctor, string.Empty, true, @class);
			generator.Context.ContextTypes.Pop ();

			AssertTargetedExpected (nameof (WriteCtor), writer.ToString ());
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

			AssertTargetedExpected (nameof (WriteCtorDeprecated), writer.ToString ());
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

			AssertTargetedExpected (nameof (WriteCtorWithStringOverload), writer.ToString ());
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

			AssertTargetedExpected (nameof (WriteFieldConstant), writer.ToString ());
		}

		[Test]
		public void WriteFieldConstantWithIntValue ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("int", "bar").SetConstant ("1234");

			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteField (field, string.Empty, @class);

			AssertTargetedExpected (nameof (WriteFieldConstantWithIntValue), writer.ToString ());
		}

		[Test]
		public void WriteFieldConstantWithStringValue ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar").SetConstant ("\"hello\"");

			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteField (field, string.Empty, @class);

			AssertTargetedExpected (nameof (WriteFieldConstantWithStringValue), writer.ToString ());
		}

		[Test]
		public void WriteFieldGetBody ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar");

			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteFieldGetBody (field, string.Empty, @class);

			AssertTargetedExpected (nameof (WriteFieldGetBody), writer.ToString ());
		}

		[Test]
		public void WriteFieldIdField ()
		{
			var field = new TestField ("java.lang.String", "bar");

			generator.WriteFieldIdField (field, string.Empty);

			AssertTargetedExpected (nameof (WriteFieldIdField), writer.ToString ());
		}

		[Test]
		public void WriteFieldInt ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("int", "bar");

			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteField (field, string.Empty, @class);

			AssertTargetedExpected (nameof (WriteFieldInt), writer.ToString ());
		}

		[Test]
		public void WriteFieldSetBody ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar");

			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteFieldSetBody (field, string.Empty, @class);

			AssertTargetedExpected (nameof (WriteFieldSetBody), writer.ToString ());
		}

		[Test]
		public void WriteFieldString ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar");

			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");
			generator.WriteField (field, string.Empty, @class);

			AssertTargetedExpected (nameof (WriteFieldString), writer.ToString ());
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

			AssertTargetedExpected (nameof (WriteInterfaceInvoker), writer.ToString ());
		}

		[Test]
		public void WriteInterfaceListenerEvent ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceListenerEvent (iface, string.Empty, "MyName", "MyNameSpec", "MyMethodName", "MyFullDelegateName", true, "MyWrefSuffix", "Add", "Remove");
			generator.Context.ContextTypes.Pop ();

			AssertOriginalExpected (nameof (WriteInterfaceListenerEvent), writer.ToString ());
		}

		[Test]
		public void WriteInterfaceListenerEventWithHandlerArgument ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceListenerEvent (iface, string.Empty, "MyName", "MyNameSpec", "MyMethodName", "MyFullDelegateName", true, "MyWrefSuffix", "AddMyName", "RemoveMyName", true);
			generator.Context.ContextTypes.Pop ();

			AssertOriginalExpected (nameof (WriteInterfaceListenerEventWithHandlerArgument), writer.ToString ());
		}

		[Test]
		public void WriteInterfaceListenerProperty ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceListenerProperty (iface, string.Empty, "MyName", "MyNameSpec", "MyMethodName", "MyConnectorFmt", "MyFullDelegateName");
			generator.Context.ContextTypes.Pop ();

			AssertOriginalExpected (nameof (WriteInterfaceListenerProperty), writer.ToString ());
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

			AssertOriginalExpected (nameof (WriteInterfaceMethodInvokers), writer.ToString ());
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

			AssertOriginalExpected (nameof (WriteInterfaceMethodInvokersWithSkips), writer.ToString ());
		}

		[Test]
		public void WriteInterfaceMethods ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceMethods (iface, string.Empty);
			generator.Context.ContextTypes.Pop ();

			AssertOriginalExpected (nameof (WriteInterfaceMethods), writer.ToString ());
		}

		[Test]
		public void WriteInterfaceProperties ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceProperties (iface, string.Empty);
			generator.Context.ContextTypes.Pop ();

			AssertOriginalExpected (nameof (WriteInterfaceProperties), writer.ToString ());
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

			AssertOriginalExpected (nameof (WriteInterfacePropertyInvokers), writer.ToString ());
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

			AssertOriginalExpected (nameof (WriteInterfacePropertyInvokersWithSkips), writer.ToString ());
		}

		[Test]
		public void WriteMethodAbstractWithVoidReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetAbstract ();

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			AssertTargetedExpected (nameof (WriteMethodAbstractWithVoidReturn), writer.ToString ());
		}

		[Test]
		public void WriteMethodAsyncifiedWithIntReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar", @return: "int").SetAsyncify ();

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			AssertTargetedExpected (nameof (WriteMethodAsyncifiedWithIntReturn), writer.ToString ());
		}

		[Test]
		public void WriteMethodAsyncifiedWithVoidReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetAsyncify ();

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			AssertTargetedExpected (nameof (WriteMethodAsyncifiedWithVoidReturn), writer.ToString ());
		}

		[Test]
		public void WriteMethodBody ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar");

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethodBody (method, string.Empty, @class);

			AssertTargetedExpected (nameof (WriteMethodBody), writer.ToString ());
		}

		[Test]
		public void WriteMethodFinalWithVoidReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetFinal ();

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			AssertTargetedExpected (nameof (WriteMethodFinalWithVoidReturn), writer.ToString ());
		}

		[Test]
		public void WriteMethodIdField ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar");

			generator.WriteMethodIdField (method, string.Empty);

			AssertTargetedExpected (nameof (WriteMethodIdField), writer.ToString ());
		}

		[Test]
		public void WriteMethodProtected ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetVisibility ("protected");

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			AssertTargetedExpected (nameof (WriteMethodProtected), writer.ToString ());
		}

		[Test]
		public void WriteMethodStaticWithVoidReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetStatic ();

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			AssertTargetedExpected (nameof (WriteMethodStaticWithVoidReturn), writer.ToString ());
		}

		[Test]
		public void WriteMethodWithIntReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar", @return: "int");

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			AssertTargetedExpected (nameof (WriteMethodWithIntReturn), writer.ToString ());
		}

		[Test]
		public void WriteMethodWithStringReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar", @return: "java.lang.String");

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			AssertTargetedExpected (nameof (WriteMethodWithStringReturn), writer.ToString ());
		}

		[Test]
		public void WriteMethodWithVoidReturn ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar");

			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			generator.WriteMethod (method, string.Empty, @class, true);

			AssertTargetedExpected (nameof (WriteMethodWithVoidReturn), writer.ToString ());
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

			AssertTargetedExpected (nameof (WriteInterfaceDeclaration), writer.ToString ());
		}

		[Test]
		public void WriteProperty ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.WriteProperty (@class.Properties.First (), @class, string.Empty);

			AssertTargetedExpected (nameof (WriteProperty), writer.ToString ());
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

			AssertTargetedExpected (nameof (WriteClass), writer.ToString ());
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
		public void WriteObjectField ()
		{
			options.SymbolTable.AddType (new TestClass (null, "Java.Lang.Object"));
			var eClass = new TestClass ("Java.Lang.Object", "java.code.Example");
			options.SymbolTable.AddType (eClass);

			var @class = new TestClass ("Object", "java.code.MyClass");

			var field   = SupportTypeBuilder.CreateField ("field", options, "java.code.Example");

			@class.Fields.Add (field);

			generator.Context.ContextTypes.Push (@class);
			generator.WriteType (@class, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			AssertTargetedExpected (nameof (WriteObjectField), writer.ToString ());
		}

		[Test]
		public void WriteInterface ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);
			var gen_info = new GenerationInfo (null, null, null);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, gen_info);
			generator.Context.ContextTypes.Pop ();

			AssertTargetedExpected (nameof (WriteInterface), writer.ToString ());
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

			WriteActualContents (nameof (WriteInterfaceEventArgs), writer.ToString ());

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceEventArgs)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceEventArgsWithParamArray ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='true' deprecated='not deprecated' final='false' name='MyListener' static='true' visibility='public' jni-signature='Lcom/xamarin/android/MyListener;'>
			      <method abstract='true' deprecated='deprecated' final='false' name='onDoSomething' jni-signature='([Ljava/lang/Object;)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public'>
			        <parameter name='args' type='java.lang.Object...' jni-type='[Ljava/lang/Object;'></parameter>
			      </method>
			    </interface>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "IMyListener");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceEventArgs (iface as InterfaceGen, iface.Methods [0], string.Empty);
			generator.Context.ContextTypes.Pop ();

			WriteActualContents (nameof (WriteInterfaceEventArgsWithParamArray), writer.ToString ());

			Assert.AreEqual (GetExpected (nameof (WriteInterfaceEventArgsWithParamArray)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteInterfaceEventHandler ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceEventHandler (iface, string.Empty);
			generator.Context.ContextTypes.Pop ();

			AssertOriginalExpected (nameof (WriteInterfaceEventHandler), writer.ToString ());
		}

		[Test]
		public void WriteInterfaceEventHandlerImpl ()
		{
			var iface = SupportTypeBuilder.CreateInterface ("java.code.IMyInterface", options);

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceEventHandlerImpl (iface, string.Empty);
			generator.Context.ContextTypes.Pop ();

			AssertOriginalExpected (nameof (WriteInterfaceEventHandlerImpl), writer.ToString ());
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
			AssertOriginalExpected (nameof (WriteInterfaceEventHandlerImplContent), writer.ToString ());
		}

		[Test]
		public void WriteMethodWithCharSequenceArrays ()
		{
			var gens = ParseApiDefinition (@"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>

			  <package name='com.example' jni-name='com/example'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/example/MyClass;'>
			      <method abstract='false' deprecated='not deprecated' final='false' name='echo' jni-signature='([Ljava/lang/Charsequence;])[Ljava/lang/CharSequence;' bridge='false' native='false' return='java.lang.CharSequence[]' jni-return='[Ljava/lang/CharSequence;' static='false' synchronized='false' synthetic='false' visibility='public'>
			        <parameter name='messages' type='java.lang.CharSequence[]' jni-type='[Ljava/lang/CharSequence;'>
			        </parameter>
			      </method>
			    </class>
			  </package>
			</api>");

			var @class = gens.OfType<ClassGen> ().First (c => c.Name == "MyClass");

			generator.Context.ContextTypes.Push (@class);
			generator.WriteType (@class, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			AssertOriginalTargetExpected (nameof (WriteMethodWithCharSequenceArrays), writer.ToString ());
		}

		[Test]
		public void WriteParameterListCallArgs ()
		{
			var list = SupportTypeBuilder.CreateParameterList (options);

			generator.WriteParameterListCallArgs (list, string.Empty, false);

			AssertTargetedExpected (nameof (WriteParameterListCallArgs), writer.ToString ());
		}

		[Test]
		public void WriteParameterListCallArgsForInvoker ()
		{
			var list = SupportTypeBuilder.CreateParameterList (options);

			generator.WriteParameterListCallArgs (list, string.Empty, true);

			AssertTargetedExpected (nameof (WriteParameterListCallArgsForInvoker), writer.ToString ());
		}

		[Test]
		public void WritePropertyAbstractDeclaration ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.WritePropertyAbstractDeclaration (@class.Properties.First (), string.Empty, @class);

			AssertOriginalExpected (nameof (WritePropertyAbstractDeclaration), writer.ToString ());
		}

		[Test]
		public void WritePropertyCallbacks ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.WritePropertyCallbacks (@class.Properties.First (), string.Empty, @class);

			AssertOriginalExpected (nameof (WritePropertyCallbacks), writer.ToString ());
		}

		[Test]
		public void WritePropertyDeclaration ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.WritePropertyDeclaration (@class.Properties.First (), string.Empty, @class, "ObjectAdapter");

			AssertOriginalExpected (nameof (WritePropertyDeclaration), writer.ToString ());
		}

		[Test]
		public void WritePropertyStringVariant ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.WritePropertyStringVariant (@class.Properties.First (), string.Empty);

			AssertOriginalExpected (nameof (WritePropertyStringVariant), writer.ToString ());
		}

		[Test]
		public void WritePropertyInvoker ()
		{
			var @class = SupportTypeBuilder.CreateClassWithProperty ("java.lang.Object", "com.mypackage.foo", "MyProperty", "int", options);

			generator.Context.ContextTypes.Push (@class);
			generator.WritePropertyInvoker (@class.Properties.First (), string.Empty, @class);
			generator.Context.ContextTypes.Pop ();

			AssertOriginalExpected (nameof (WritePropertyInvoker), writer.ToString ());
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
		public void WriteBoundMethodAbstractDeclarationWithGenericReturn ()
		{
			// Fix a case where the ReturnType of a class method implementing a generic interface method
			// that has a generic parameter type return (like T) wasn't getting set, resulting in a NRE.
			var gens = ParseApiDefinition (@"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			    <interface abstract='false' deprecated='not deprecated' final='false' name='Comparable' static='false' visibility='public' jni-signature='Ljava/lang/Double;' />
			  </package>

			  <package name='com.example' jni-name='com/example'>
			    <interface abstract='true' deprecated='not deprecated' final='false' name='FlowIterator' static='false' visibility='public' jni-signature='Lcom/example/FlowIterator;'>
			      <typeParameters>
			        <typeParameter name='R' classBound='java.lang.Object' jni-classBound='Ljava/lang/Object;'></typeParameter>
			      </typeParameters>
			      <method abstract='true' deprecated='not deprecated' final='false' name='next' jni-signature='()Ljava/lang/Object;' bridge='false' native='false' return='R' jni-return='TR;' static='false' synchronized='false' synthetic='false' visibility='public' />
			    </interface>
			    <class abstract='true' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='FlowIterator.RangeIterator' static='true' visibility='public' jni-signature='Lcom/example/FlowIterator$RangeIterator;'>
			      <implements name='com.example.FlowIterator' name-generic-aware='com.example.FlowIterator&lt;T&gt;' jni-type='Lcom/example/FlowIterator&lt;TT;&gt;;'></implements>
			      <typeParameters>
			        <typeParameter name='T' interfaceBounds='java.lang.Comparable&lt;T&gt;' jni-interfaceBounds='Ljava/lang/Comparable&lt;TT;&gt;;'>
			          <genericConstraints>
			            <genericConstraint type='java.lang.Comparable&lt;T&gt;'></genericConstraint>
			          </genericConstraints>
			        </typeParameter>
			      </typeParameters>
			      <method abstract='false' deprecated='not deprecated' final='false' name='next' jni-signature='()Ljava/lang/Comparable;' bridge='false' native='false' return='T' jni-return='TT;' static='false' synchronized='false' synthetic='false' visibility='public' />
			    </class>
			  </package>
			</api>");

			var declIface = gens.OfType<InterfaceGen> ().Single (c => c.Name == "IFlowIterator");
			var declClass = options.SupportNestedInterfaceTypes
				? declIface.NestedTypes.OfType<ClassGen> ().Single (c => c.Name == "RangeIterator")
				: declIface.NestedTypes.OfType<ClassGen> ().Single (c => c.Name == "FlowIteratorRangeIterator");
			var method = declClass.Methods.Single ();

			var bmad = new BoundMethodAbstractDeclaration (declClass, method, options, null);
			var source_writer = new CodeWriter (writer);

			bmad.Write (source_writer);

			var expected = options.SupportNestedInterfaceTypes
				? @"Java.Lang.Object Com.Example.IFlowIterator.RangeIterator.Next ()
					{
					  throw new NotImplementedException ();
					}"
				: @"Java.Lang.Object Com.Example.FlowIteratorRangeIterator.Next ()
					{
					  throw new NotImplementedException ();
					}";

			// Ignore nullable operator so this test works on all generators  ;)
			Assert.AreEqual (expected.NormalizeLineEndings (), writer.ToString ().NormalizeLineEndings ().Replace ("?", ""));
		}
	}
}
