using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
		public void ManagedOverrideAbstractMethod_Override ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='true' deprecated='not deprecated' final='true' name='DoStuff' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' managedOverride='override'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var klass = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.True (writer.ToString ().Contains ("public override abstract int DoStuff ();"), $"was: `{writer}`");
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
		public void SkipInvokerMethodsMetadata ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='true' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyBaseClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyBaseClass;'>
			      <method abstract='true' deprecated='not deprecated' final='false' name='doStuff' jni-signature='()Lcom/xamarin/android/MyBaseClass;' bridge='false' native='false' return='com.xamarin.android.MyBaseClass' jni-return='Lcom/xamarin/android/MyBaseClass;' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			    </class>
			    <class abstract='true' deprecated='not deprecated' extends='com.xamarin.android.MyBaseClass' extends-generic-aware='com.xamarin.android.MyBaseClass' jni-extends='Lcom/xamarin/android/MyBaseClass;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;' skipInvokerMethods='com/xamarin/android/MyBaseClass.doStuff()Lcom/xamarin/android/MyBaseClass;'>
			      <method abstract='true' deprecated='not deprecated' final='false' name='doStuff' jni-signature='()Lcom/xamarin/android/MyClass;' bridge='false' native='false' return='com.xamarin.android.MyClass' jni-return='Lcom/xamarin/android/MyClass;' static='false' synchronized='false' synthetic='false' visibility='public' managedOverride='override' return-not-null='true'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var klass = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// `override abstract` causes both invoker methods to get generated. We use metadata
			// to suppress the base class's method to prevent a conflict.
			Assert.True (writer.ToString ().Contains ("public override abstract Com.Xamarin.Android.MyClass DoStuff ();"), $"was: `{writer}`");
			Assert.False (writer.ToString ().Contains ("public abstract Com.Xamarin.Android.MyBaseClass DoStuff ();"), $"was: `{writer}`");
		}

		[Test]
		public void SkipInterfaceInvokerMethodsMetadata ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='true' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyInterface' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyInterface;' skipInvokerMethods='com/xamarin/android/MyInterface.countAffectedRows()I'>
			      <method abstract='true' deprecated='not deprecated' final='false' name='countAffectedRows' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			    </interface>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "IMyInterface");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// Ensure the invoker for 'countAffectedRows' isn't generated
			Assert.False (writer.ToString ().Contains ("static Delegate cb_countAffectedRows;"), $"was: `{writer}`");
			Assert.False (writer.ToString ().Contains ("InvokeAbstractInt32Method"), $"was: `{writer}`");
		}

		[Test]
		public void SkipInterfaceMethodsMetadata ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='true' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyInterface' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyInterface;'>
			      <method abstract='true' deprecated='not deprecated' final='false' name='countAffectedRows' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			    </interface>
			    <class abstract='true' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyBaseClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyBaseClass;' skipInterfaceMethods='com/xamarin/android/MyInterface.countAffectedRows()I'>
			      <implements name='com.xamarin.android.MyInterface' name-generic-aware='com.xamarin.android.MyInterface' jni-type='Lcom/xamarin/android/MyInterface;'></implements>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var klass = gens.Single (g => g.Name == "MyBaseClass");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// The abstract method should not get generated because we are suppressing it with 'skipInterfaceMethods'
			Assert.False (writer.ToString ().Contains ("public abstract int CountAffectedRows ();"), $"was: `{writer}`");
		}

		[Test]
		public void CompatVirtualMethod_Class ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='true' deprecated='not deprecated' final='true' name='DoStuff' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' compatVirtualMethod='true'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var klass = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.True (writer.ToString ().NormalizeLineEndings ().Contains ("catch (Java.Lang.NoSuchMethodError) { throw new Java.Lang.AbstractMethodError (__id); }".NormalizeLineEndings ()), $"was: `{writer}`");
		}

		[Test]
		public void PeerConstructorPartialMethod_Class ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated'
			        extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;'
			        peerConstructorPartialMethod='_OnMyClassCreated'
			        jni-signature='Lcom/xamarin/android/MyClass;'
			        name='MyClass'
			        final='false' static='false' visibility='public'>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var klass = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			Assert.True (writer.ToString ().NormalizeLineEndings ().Contains ("partial void _OnMyClassCreated ();".NormalizeLineEndings ()), $"was: `{writer}`");
			Assert.True (writer.ToString ().NormalizeLineEndings ().Contains ("{ _OnMyClassCreated (); }".NormalizeLineEndings ()), $"was: `{writer}`");
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

			AssertTargetedExpected (nameof (WriteDuplicateInterfaceEventArgs), writer.ToString ());
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
		public void ObsoletedOSPlatformAttributeInterfaceInfrastructureSupport ()
		{
			var xml = @"<api>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <interface abstract='false' deprecated='This interface was deprecated in API-25' final='false' name='MyType' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyType;' deprecated-since='25' />
			  </package>
			</api>";

			options.UseObsoletedOSPlatformAttributes = true;

			var gens = ParseApiDefinition (xml);
			var iface = gens.OfType<InterfaceGen> ().Single (g => g.Name == "IMyType");

			generator.Context.ContextTypes.Push (iface);
			var invoker = new InterfaceInvokerClass (iface, options, generator.Context);
			var extensions = new InterfaceExtensionsClass (iface, iface.Name, options);
			generator.Context.ContextTypes.Pop ();

			// Ensure attribute was added to invoker class
			var invoker_attribute = invoker.Attributes.OfType<ObsoletedOSPlatformAttr> ().Single ();
			Assert.AreEqual ("This interface was deprecated in API-25", invoker_attribute.Message);
			Assert.AreEqual (25, invoker_attribute.Version);

			// Ensure attribute was added to extensions class
			var extensions_attribute = invoker.Attributes.OfType<ObsoletedOSPlatformAttr> ().Single ();
			Assert.AreEqual ("This interface was deprecated in API-25", extensions_attribute.Message);
			Assert.AreEqual (25, extensions_attribute.Version);
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
		public void RestrictToType ()
		{
			options.UseRestrictToAttributes = true;

			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;' annotated-visibility='LIBRARY_GROUP_PREFIX' />
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// This should use a special [Obsolete] describing the "internal" nature of this API
			Assert.True (writer.ToString ().NormalizeLineEndings ().Contains ("[global::System.Obsolete (\"While this type is 'public', Google considers it internal API and reserves the right to modify or delete it in the future. Use at your own risk.\", DiagnosticId = \"XAOBS001\")]".NormalizeLineEndings ()), writer.ToString ());
		}

		[Test]
		public void RestrictToField ()
		{
			options.UseRestrictToAttributes = true;

			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <field deprecated='not deprecated' name='ACCEPT_HANDOVER' jni-signature='Ljava/lang/String;' transient='false' type='java.lang.String' type-generic-aware='java.lang.String' visibility='public' volatile='false' annotated-visibility='LIBRARY_GROUP_PREFIX'></field>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// This should use a special [Obsolete] describing the "internal" nature of this API
			Assert.True (writer.ToString ().NormalizeLineEndings ().Contains ("[global::System.Obsolete (\"While this member is 'public', Google considers it internal API and reserves the right to modify or delete it in the future. Use at your own risk.\", DiagnosticId = \"XAOBS001\")]".NormalizeLineEndings ()), writer.ToString ());
		}

		[Test]
		public void RestrictToMethod ()
		{
			options.UseRestrictToAttributes = true;

			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' final='false' name='countAffectedRows' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' annotated-visibility='LIBRARY_GROUP_PREFIX'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// This should use a special [Obsolete] describing the "internal" nature of this API
			Assert.True (writer.ToString ().NormalizeLineEndings ().Contains ("[global::System.Obsolete (\"While this member is 'public', Google considers it internal API and reserves the right to modify or delete it in the future. Use at your own risk.\", DiagnosticId = \"XAOBS001\")]".NormalizeLineEndings ()), writer.ToString ());
		}

		[Test]
		public void RestrictToProperty ()
		{
			options.UseRestrictToAttributes = true;

			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='not deprecated' final='false' name='getCount' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' annotated-visibility='LIBRARY_GROUP_PREFIX'></method>
			      <method abstract='false' deprecated='not deprecated' final='false' name='setCount' jni-signature='(I)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public' annotated-visibility='LIBRARY_GROUP_PREFIX'>
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

			// This should use a special [Obsolete] describing the "internal" nature of this API
			Assert.True (writer.ToString ().NormalizeLineEndings ().Contains ("[global::System.Obsolete (\"While this member is 'public', Google considers it internal API and reserves the right to modify or delete it in the future. Use at your own risk.\", DiagnosticId = \"XAOBS001\")]".NormalizeLineEndings ()), writer.ToString ());
		}

		[Test]
		public void DoNotWriteObsoleteAndRestrictTo ()
		{
			options.UseRestrictToAttributes = true;

			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method deprecated='deprecated' abstract='false' final='false' name='countAffectedRows' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' annotated-visibility='LIBRARY_GROUP_PREFIX'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var iface = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (iface);
			generator.WriteType (iface, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			// This method is both @Deprecated and @RestrictTo. We cannot write 2 [Obsolete] attributes, so
			// only write the deprecated one.
			Assert.True (writer.ToString ().Replace (" (@\"deprecated\")", "").NormalizeLineEndings ().Contains ("[global::System.Obsolete]".NormalizeLineEndings ()), writer.ToString ());
			Assert.False (writer.ToString ().NormalizeLineEndings ().Contains ("[global::System.Obsolete (\"While this member is 'public', Google considers it internal API and reserves the right to modify or delete it in the future. Use at your own risk.\", DiagnosticId = \"XAOBS001\")]".NormalizeLineEndings ()), writer.ToString ());
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


		[Test]
		public void AvoidNREOnInvalidBaseMethod ()
		{
			// We copy methods from the package-private base class to the public class, however
			// the copied method is not valid because it doesn't understand the generic argument
			// type. The method is remove from the public class, but we need to ensure the
			// base class method still exists and IsValid.
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>

			  <package name='android.view' jni-name='android/view'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' final='false' name='View' static='false' visibility='public' jni-signature='Landroid/view/View;' />
			  </package>

			  <package name='com.google.android.material.behavior' jni-name='com/google/android/material/behavior'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='ViewOffsetBehavior' static='false' visibility='' jni-signature='Lcom/google/android/material/appbar/ViewOffsetBehavior;'>
			      <typeParameters>
				<typeParameter name='V' classBound='android.view.View' jni-classBound='Landroid/view/View;'>
				  <genericConstraints>
				    <genericConstraint type='android.view.View' />
				  </genericConstraints>
				</typeParameter>
			      </typeParameters>
			      <method abstract='false' deprecated='not deprecated' final='false' name='layoutChild' jni-signature='(Landroid/view/View;)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='protected'>
				<parameter name='child' type='V' jni-type='TV;' not-null='true' />
			      </method>
			    </class>
    
			    <class abstract='false' deprecated='not deprecated' extends='com.google.android.material.behavior.ViewOffsetBehavior' extends-generic-aware='com.google.android.material.behavior.ViewOffsetBehavior&lt;android.view.View&gt;' jni-extends='Lcom/google/android/material/appbar/ViewOffsetBehavior;' final='false' name='Behavior' static='false' visibility='public' jni-signature='Lcom/google/android/material/appbar/Behavior;'>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);

			var public_class = gens.Single (g => g.Name == "Behavior");
			var base_class = gens.Single (g => g.Name == "ViewOffsetBehavior");

			// Method got removed
			Assert.AreEqual (0, public_class.Methods.Count);

			// Method still exists and is valid
			Assert.AreEqual (1, base_class.Methods.Count);
			Assert.AreEqual (true, base_class.Methods [0].IsValid);
		}

		[Test]
		public void FixupDeprecatedBaseMethods ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;'>
			      <method abstract='false' deprecated='deprecated' final='false' name='DoStuff' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			    </class>
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='not deprecated' final='false' name='DoStuff' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public'></method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);

			// Override method should not be marked deprecated because it's: deprecated='not deprecated'
			Assert.IsNull (gens.Single (g => g.Name == "MyClass").Methods.Single (m => m.Name == "DoStuff").Deprecated);

			options.FixObsoleteOverrides = true;
			gens = ParseApiDefinition (xml);

			// Override method should be marked deprecated because base method is
			Assert.AreEqual ("deprecated", gens.Single (g => g.Name == "MyClass").Methods.Single (m => m.Name == "DoStuff").Deprecated);
		}

		[Test]
		public void FixupDeprecatedSinceBaseMethods ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;'>
			      <method abstract='false' deprecated='deprecated' final='false' name='DoStuff' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' deprecated-since='11'></method>
			    </class>
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract='false' deprecated='not deprecated' final='false' name='DoStuff' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' deprecated-since='21'></method>
			    </class>
			  </package>
			</api>";

			options.FixObsoleteOverrides = true;
			var gens = ParseApiDefinition (xml);

			// Override method should match base method's 'deprecated-since'
			Assert.AreEqual (11, gens.Single (g => g.Name == "MyClass").Methods.Single (m => m.Name == "DoStuff").DeprecatedSince);
		}

		protected static IEnumerable<string> GetLinesThatStartWith (string source, string value)
		{
			using (var reader = new StringReader (source)) {
				string line;
				while ((line = reader.ReadLine ()) != null) {
					if (line.TrimStart ().StartsWith (value, StringComparison.Ordinal))
						yield return line;
				}
			}
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

		[Test]
		public void StringPropertyOverride ([Values("true", "false")] string final)
		{
			var xml = @$"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object'
			      final='true' name='String' static='false' visibility='public'>
			    </class>
			  </package>
			  <package name='android.widget' jni-name='android/widget'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' final='false' name='TextView' static='false' visibility='public'>
			       <method abstract='false' deprecated='not deprecated' final='{final}' name='getText' bridge='false' native='false' return='java.lang.CharSequence' static='false' synchronized='false' synthetic='false' visibility='public'>
			       </method>
			       <method abstract='false' deprecated='not deprecated' final='{final}' name='setText' bridge='false' native='false' return='void' static='false' synchronized='false' synthetic='false' visibility='public'>
			         <parameter name='text' type='java.lang.CharSequence'>
			         </parameter>
			       </method>
			     </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var klass = gens.Single (g => g.Name == "TextView");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			if (final == "true") {
				Assert.True (writer.ToString ().Contains (
		@"set {
			const string __id = ""setText.(Ljava/lang/CharSequence;)V"";
			global::Java.Interop.JniObjectReference text = global::Java.Interop.JniEnvironment.Strings.NewString (value);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (text);
				_members.InstanceMethods.InvokeNonvirtualVoidMethod (__id, this, __args);
			} finally {
				global::Java.Interop.JniObjectReference.Dispose (ref text);
			}
		}
	}"), $"was: `{writer}`");
			} else {
				Assert.True (writer.ToString ().Contains (
		@"set {
			var jls = value == null ? null : new global::Java.Lang.String (value);
			TextFormatted = jls;
			if (jls != null) jls.Dispose ();
		}"), $"was: `{writer}`");
			}
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

		[Test]
		public void UnsupportedOSPlatform ()
		{
			var klass = SupportTypeBuilder.CreateClass ("java.code.MyClass", options);
			klass.ApiRemovedSince = 30;

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			StringAssert.Contains ("[global::System.Runtime.Versioning.UnsupportedOSPlatformAttribute (\"android30.0\")]", builder.ToString (), "Should contain UnsupportedOSPlatform!");
		}

		[Test]
		public void UnsupportedOSPlatformConstFields ()
		{
			var klass = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar").SetConstant ("MY_VALUE");

			field.ApiRemovedSince = 30;

			klass.Fields.Add (field);

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			StringAssert.Contains ("[global::System.Runtime.Versioning.UnsupportedOSPlatformAttribute (\"android30.0\")]", builder.ToString (), "Should contain UnsupportedOSPlatform!");
		}

		[Test]
		public void StringPropertyOverride ([Values ("true", "false")] string final)
		{
			var xml = @$"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object'
			      final='true' name='String' static='false' visibility='public'>
			    </class>
			  </package>
			  <package name='android.widget' jni-name='android/widget'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' final='false' name='TextView' static='false' visibility='public'>
			       <method abstract='false' deprecated='not deprecated' final='{final}' name='getText' bridge='false' native='false' return='java.lang.CharSequence' static='false' synchronized='false' synthetic='false' visibility='public'>
			       </method>
			       <method abstract='false' deprecated='not deprecated' final='{final}' name='setText' bridge='false' native='false' return='void' static='false' synchronized='false' synthetic='false' visibility='public'>
			         <parameter name='text' type='java.lang.CharSequence'>
			         </parameter>
			       </method>
			     </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var klass = gens.Single (g => g.Name == "TextView");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			if (final == "true") {
				Assert.True (writer.ToString ().Contains (
		@"set {
			const string __id = ""setText.(Ljava/lang/CharSequence;)V"";
			global::Java.Interop.JniObjectReference native_text = global::Java.Interop.JniEnvironment.Strings.NewString (value);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_text);
				_members.InstanceMethods.InvokeNonvirtualVoidMethod (__id, this, __args);
			} finally {
				global::Java.Interop.JniObjectReference.Dispose (ref native_text);
			}
		}"), $"was: `{writer}`");
			} else {
				Assert.True (writer.ToString ().Contains (
		@"set {
			var jls = value == null ? null : new global::Java.Lang.String (value);
			TextFormatted = jls;
			if (jls != null) jls.Dispose ();
		}"), $"was: `{writer}`");
			}
		}

		[Test]
		public void CallbackVariableNamesShouldntCollide ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='Object2' static='false' visibility='public' jni-signature='Ljava/lang/Object2;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='MyClass' static='false' visibility='public' jni-signature='Lcom/xamarin/android/MyClass;'>
			      <method abstract=""false"" deprecated=""not deprecated"" final=""false"" name=""setDetectorMode"" jni-signature=""(I)Ljava/lang/Object;"" bridge=""false"" native=""false"" return=""java.lang.Object"" jni-return=""Ljava/lang/Object;"" static=""false"" synchronized=""false"" synthetic=""false"" visibility=""public"" return-not-null=""true"">
			        <parameter name=""detectorMode"" type=""int"" jni-type=""I"" />
			      </method>
			      <method abstract=""false"" deprecated=""not deprecated"" final=""false"" name=""setDetectorMode"" jni-signature=""(I)Ljava/lang/Object2;"" bridge=""false"" native=""false"" return=""java.lang.Object2"" jni-return=""Ljava/lang/Object2;"" static=""false"" synchronized=""false"" synthetic=""false"" visibility=""public"" return-not-null=""true"" managedName=""SetDetectorMode2"">
			        <parameter name=""p0"" type=""int"" jni-type=""I"" />
			      </method>
			    </class>
			  </package>
			</api>";

			var gens = ParseApiDefinition (xml);
			var klass = gens.Single (g => g.Name == "MyClass");

			generator.Context.ContextTypes.Push (klass);
			generator.WriteType (klass, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();
			var a = writer.ToString ();
			var lines = GetLinesThatStartWith (writer.ToString (), "static Delegate cb_");

			// Ensure that 2 cb_ delegates got written, and they do not have the same name
			Assert.AreEqual (2, lines.Count ());
			Assert.AreNotEqual (lines.ElementAt (0), lines.ElementAt (1));
		}
	}

	abstract class CodeGeneratorTests : CodeGeneratorTestBase
	{
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
