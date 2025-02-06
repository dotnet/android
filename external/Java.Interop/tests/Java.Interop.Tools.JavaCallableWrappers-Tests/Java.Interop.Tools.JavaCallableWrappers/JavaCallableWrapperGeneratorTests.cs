using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Mono.Cecil;

using NUnit.Framework;

using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.JavaCallableWrappersTests;
using Java.Interop.Tools.TypeNameMappings;

using Xamarin.Android.ToolsTests;
using Java.Interop.Tools.JavaCallableWrappers.Adapters;
using Java.Interop.Tools.Cecil;

namespace Java.Interop.Tools.JavaCallableWrappersTests
{
	[TestFixture]
	public class JavaCallableWrapperGeneratorTests
	{
		[Test]
		public void ConstructorExceptions ()
		{
			Action<string, object []> logger = (f, o) => { };

			// structs aren't supported
			var td  = SupportDeclarations.GetTypeDefinition (typeof (int));
			var e   = Assert.Throws<XamarinAndroidException> (() => CecilImporter.CreateType (td, new TypeDefinitionCache ()));
			Assert.AreEqual (4200, e.Code);
		}

		[Test]
		public void KotlinInvalidImplRegisterName ()
		{
			Action<string, object []> logger = (f, o) => { };

			// Contains invalid [Register] name of "foo-impl"
			var td = SupportDeclarations.GetTypeDefinition (typeof (KotlinInvalidImplRegisterName));
			var e = Assert.Throws<XamarinAndroidException> (() => CecilImporter.CreateType (td, new TypeDefinitionCache ()));
			Assert.AreEqual (4217, e.Code);
		}

		[Test]
		public void KotlinInvalidHashRegisterName ()
		{
			Action<string, object []> logger = (f, o) => { };

			// Contains invalid [Register] name of "foo-f8k2a13"
			var td = SupportDeclarations.GetTypeDefinition (typeof (KotlinInvalidHashRegisterName));
			var e = Assert.Throws<XamarinAndroidException> (() => CecilImporter.CreateType (td, new TypeDefinitionCache ()));
			Assert.AreEqual (4217, e.Code);
		}

		[Test]
		public void GenerateApplication (
				[Values (null, "android.app.Application", "android.support.multidex.MultiDexApplication")] string applicationJavaClass
		)
		{
			var actual      = Generate (typeof (ApplicationName), applicationJavaClass);
			var expected    = $@"package application;


public class Name
	extends {applicationJavaClass ?? "android.app.Application"}
	implements
		mono.android.IGCUserPeer
{{
/** @hide */
	public static final String __md_methods;
	static {{
		__md_methods = 
			"""";
	}}

	public Name ()
	{{
		mono.MonoPackageManager.setContext (this);
	}}

	public void onCreate ()
	{{
		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.ApplicationName, Java.Interop.Tools.JavaCallableWrappers-Tests"", Name.class, __md_methods);
		super.onCreate ();
	}}

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}}

	public void monodroidClearReferences ()
	{{
		if (refList != null)
			refList.clear ();
	}}
}}
";
			Assert.AreEqual (expected, actual);
		}

		[Test]
		public void GenerateTypeMentioningNestedInvoker ()
		{
			var actual      = Generate (typeof (ApplicationName.ActivityLifecycleCallbacks));
			var expected    = """
package application;


public class Name_ActivityLifecycleCallbacks
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		java.lang.Object
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onActivityCreated:(Landroid/app/Activity;Landroid/os/Bundle;)V:GetOnActivityCreated_Landroid_app_Activity_Landroid_os_Bundle_Handler:Android.App.Application+IActivityLifecycleCallbacksInvoker, Mono.Android\n" +
			"";
		mono.android.Runtime.register ("Xamarin.Android.ToolsTests.ApplicationName+ActivityLifecycleCallbacks, Java.Interop.Tools.JavaCallableWrappers-Tests", Name_ActivityLifecycleCallbacks.class, __md_methods);
	}

	public void onActivityCreated (android.app.Activity p0, android.os.Bundle p1)
	{
		n_onActivityCreated (p0, p1);
	}

	private native void n_onActivityCreated (android.app.Activity p0, android.os.Bundle p1);

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}

""";
			Assert.AreEqual (expected, actual);
		}

		static string Generate (Type type, string applicationJavaClass = null, string monoRuntimeInit = null, JavaPeerStyle style = JavaPeerStyle.XAJavaInterop1)
		{
			var reader_options = new CallableWrapperReaderOptions {
				DefaultApplicationJavaClass = applicationJavaClass,
				DefaultGenerateOnCreateOverrides = true,
				DefaultMonoRuntimeInitialization = monoRuntimeInit,
			};

			var td  = SupportDeclarations.GetTypeDefinition (type);
			var g = CecilImporter.CreateType (td, new TypeDefinitionCache (), reader_options);

			var o   = new StringWriter ();
			var dir = Path.GetDirectoryName (typeof (JavaCallableWrapperGeneratorTests).Assembly.Location);
			var options = new CallableWrapperWriterOptions {
				CodeGenerationTarget        = style,
			};

			g.Generate (Path.Combine (dir, "__o"), options);
			g.Generate (o, options);

			return o.ToString ();
		}


		[Test]
		public void GenerateIndirectApplication (
				[Values (null, "android.app.Application", "android.support.multidex.MultiDexApplication")] string applicationJavaClass
		)
		{
			var actual      = Generate (typeof (IndirectApplication), applicationJavaClass);
			var expected    = @"package crc64197ae30a36756915;


public class IndirectApplication
	extends application.Name
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			""n_onCreate:()V:Application.OnCreate\n"" +
			"""";
	}

	public IndirectApplication ()
	{
		mono.MonoPackageManager.setContext (this);
	}

	public void onCreate ()
	{
		n_onCreate ();
	}

	private native void n_onCreate ();

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
";
			Assert.AreEqual (expected, actual);
		}

		[Test]
		public void GenerateExportedMembers ()
		{
			var actual = Generate (typeof (ExportsMembers));
			var expected = @"package crc64197ae30a36756915;


public class ExportsMembers
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			""n_GetInstance:()Lcrc64197ae30a36756915/ExportsMembers;:__export__\n"" +
			""n_GetValue:()Ljava/lang/String;:__export__\n"" +
			""n_staticMethodNotMangled:()V:__export__\n"" +
			""n_methodNamesNotMangled:()V:__export__\n"" +
			""n_CompletelyDifferentName:(Ljava/lang/String;I)Ljava/lang/String;:__export__\n"" +
			""n_methodThatThrows:()V:__export__\n"" +
			""n_methodThatThrowsEmptyArray:()V:__export__\n"" +
			"""";
		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.ExportsMembers, Java.Interop.Tools.JavaCallableWrappers-Tests"", ExportsMembers.class, __md_methods);
	}

	public static crc64197ae30a36756915.ExportsMembers STATIC_INSTANCE = GetInstance ();

	public java.lang.String VALUE = GetValue ();

	public static crc64197ae30a36756915.ExportsMembers GetInstance ()
	{
		return n_GetInstance ();
	}

	private static native crc64197ae30a36756915.ExportsMembers n_GetInstance ();

	public java.lang.String GetValue ()
	{
		return n_GetValue ();
	}

	private native java.lang.String n_GetValue ();

	public static void staticMethodNotMangled ()
	{
		n_staticMethodNotMangled ();
	}

	private static native void n_staticMethodNotMangled ();

	public void methodNamesNotMangled ()
	{
		n_methodNamesNotMangled ();
	}

	private native void n_methodNamesNotMangled ();

	public java.lang.String attributeOverridesNames (java.lang.String p0, int p1)
	{
		return n_CompletelyDifferentName (p0, p1);
	}

	private native java.lang.String n_CompletelyDifferentName (java.lang.String p0, int p1);

	public void methodThatThrows () throws java.lang.Throwable
	{
		n_methodThatThrows ();
	}

	private native void n_methodThatThrows ();

	public void methodThatThrowsEmptyArray ()
	{
		n_methodThatThrowsEmptyArray ();
	}

	private native void n_methodThatThrowsEmptyArray ();

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
";
			Assert.AreEqual (expected, actual);
		}

		[Test]
		public void GenerateInnerClass ()
		{
			var actual = Generate (typeof (ExampleOuterClass));
			var expected = @"package crc64197ae30a36756915;


public class ExampleOuterClass
	extends register.NonStaticOuterClass
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static final String __md_1_methods;
	static {
		__md_methods = 
			"""";
		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.ExampleOuterClass, Java.Interop.Tools.JavaCallableWrappers-Tests"", ExampleOuterClass.class, __md_methods);
		__md_1_methods = 
			"""";
		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.ExampleOuterClass+ExampleInnerClass, Java.Interop.Tools.JavaCallableWrappers-Tests"", ExampleOuterClass_ExampleInnerClass.class, __md_1_methods);
	}

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}

public class ExampleOuterClass_ExampleInnerClass
	extends register.NonStaticOuterClass.NonStaticInnerClass
	implements
		mono.android.IGCUserPeer
{

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
}
";
			Assert.AreEqual (expected, actual);
		}

		[Test]
		public void GenerateNestedClass_DefaultName ()
		{
			var actual = Generate (typeof (RegisterName.DefaultNestedName));
			var expected = @"package register;


public class Name_DefaultNestedName
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"""";
		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.RegisterName+DefaultNestedName, Java.Interop.Tools.JavaCallableWrappers-Tests"", Name_DefaultNestedName.class, __md_methods);
	}

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
";
			Assert.AreEqual (expected, actual);
		}

		[Test]
		public void GenerateNestedClass_OverrideName ()
		{
			var actual = Generate (typeof (RegisterName.OverrideNestedName));
			var expected = @"package register;


public class Name$Override
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"""";
		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.RegisterName+OverrideNestedName, Java.Interop.Tools.JavaCallableWrappers-Tests"", Name$Override.class, __md_methods);
	}

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
";
			Assert.AreEqual (expected, actual);
		}

		[Test]
		public void GenerateConstructors ()
		{
			var actual = Generate (typeof (ExportsConstructors));
			var expected = @"package register;


public class ExportsConstructors
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"""";
		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.ExportsConstructors, Java.Interop.Tools.JavaCallableWrappers-Tests"", ExportsConstructors.class, __md_methods);
	}

	public ExportsConstructors ()
	{
		super ();
		if (getClass () == ExportsConstructors.class) {
			mono.android.TypeManager.Activate (""Xamarin.Android.ToolsTests.ExportsConstructors, Java.Interop.Tools.JavaCallableWrappers-Tests"", """", this, new java.lang.Object[] {  });
		}
	}

	public ExportsConstructors (int p0)
	{
		super (p0);
		if (getClass () == ExportsConstructors.class) {
			mono.android.TypeManager.Activate (""Xamarin.Android.ToolsTests.ExportsConstructors, Java.Interop.Tools.JavaCallableWrappers-Tests"", ""System.Int32, System.Private.CoreLib"", this, new java.lang.Object[] { p0 });
		}
	}

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
";
			Assert.AreEqual (expected, actual);
		}

		[Test]
		public void GenerateConstructors_WithThrows ()
		{
			var actual = Generate (typeof (ExportsThrowsConstructors));
			var expected = @"package register;


public class ExportsThrowsConstructors
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"""";
		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.ExportsThrowsConstructors, Java.Interop.Tools.JavaCallableWrappers-Tests"", ExportsThrowsConstructors.class, __md_methods);
	}

	public ExportsThrowsConstructors () throws java.lang.Throwable
	{
		super ();
		if (getClass () == ExportsThrowsConstructors.class) {
			mono.android.TypeManager.Activate (""Xamarin.Android.ToolsTests.ExportsThrowsConstructors, Java.Interop.Tools.JavaCallableWrappers-Tests"", """", this, new java.lang.Object[] {  });
		}
	}

	public ExportsThrowsConstructors (int p0) throws java.lang.Throwable
	{
		super (p0);
		if (getClass () == ExportsThrowsConstructors.class) {
			mono.android.TypeManager.Activate (""Xamarin.Android.ToolsTests.ExportsThrowsConstructors, Java.Interop.Tools.JavaCallableWrappers-Tests"", ""System.Int32, System.Private.CoreLib"", this, new java.lang.Object[] { p0 });
		}
	}

	public ExportsThrowsConstructors (java.lang.String p0)
	{
		super (p0);
		if (getClass () == ExportsThrowsConstructors.class) {
			mono.android.TypeManager.Activate (""Xamarin.Android.ToolsTests.ExportsThrowsConstructors, Java.Interop.Tools.JavaCallableWrappers-Tests"", ""System.String, System.Private.CoreLib"", this, new java.lang.Object[] { p0 });
		}
	}

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
";
			Assert.AreEqual (expected, actual);
		}

		[Test]
		public void GenerateActivity ()
		{
			var actual = Generate (typeof (ExampleActivity));
			var expected = @"package my;


public class ExampleActivity
	extends android.app.Activity
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"""";
		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.ExampleActivity, Java.Interop.Tools.JavaCallableWrappers-Tests"", ExampleActivity.class, __md_methods);
	}

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
";
			Assert.AreEqual (expected, actual);
		}

		[Test]
		public void GenerateInstrumentation ()
		{
			var init = "mono.MonoPackageManager.LoadApplication (context, context.getApplicationInfo (), new String[]{context.getApplicationInfo ().sourceDir});";
			var actual = Generate (typeof (ExampleInstrumentation), monoRuntimeInit: init);
			var expected = $@"package my;


public class ExampleInstrumentation
	extends android.app.Instrumentation
	implements
		mono.android.IGCUserPeer
{{
/** @hide */
	public static final String __md_methods;
	static {{
		__md_methods = 
			"""";
	}}

	public void onCreate (android.os.Bundle arguments)
	{{
		android.content.Context context = getContext ();

{init}

		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.ExampleInstrumentation, Java.Interop.Tools.JavaCallableWrappers-Tests"", ExampleInstrumentation.class, __md_methods);
		super.onCreate (arguments);
	}}

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}}

	public void monodroidClearReferences ()
	{{
		if (refList != null)
			refList.clear ();
	}}
}}
";
			Assert.AreEqual (expected, actual);
		}

		[Test]
		public void GenerateJavaInteropExample ()
		{
			var actual = Generate (typeof (JavaInteropExample), style: JavaPeerStyle.JavaInterop1);
			var expected = @"package register;


public class JavaInteropExample
	extends java.lang.Object
	implements
		net.dot.jni.GCUserPeerable
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			""n_Example:()V:__export__\n"" +
			"""";
		net.dot.jni.ManagedPeer.registerNativeMembers (JavaInteropExample.class, __md_methods);
	}

	public JavaInteropExample (int p0, int p1)
	{
		super ();
		if (getClass () == JavaInteropExample.class) {
			net.dot.jni.ManagedPeer.construct (this, ""(II)V"", new java.lang.Object[] { p0, p1 });
		}
	}

	public void example ()
	{
		n_Example ();
	}

	private native void n_Example ();

	private java.util.ArrayList refList;
	public void jiAddManagedReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void jiClearManagedReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
";
			Assert.AreEqual (expected, actual);
		}
	}
}

