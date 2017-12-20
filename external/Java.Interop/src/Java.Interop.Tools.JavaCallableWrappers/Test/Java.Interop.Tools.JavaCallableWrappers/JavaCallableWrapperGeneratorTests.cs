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
			var e   = Assert.Throws<XamarinAndroidException> (() => new JavaCallableWrapperGenerator (td, logger));
			Assert.AreEqual (4200, e.Code);
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

		static string Generate (Type type, string applicationJavaClass = null)
		{
			var td  = SupportDeclarations.GetTypeDefinition (type);
			var g   = new JavaCallableWrapperGenerator (td, null) {
				ApplicationJavaClass        = applicationJavaClass,
			};
			var o   = new StringWriter ();
			g.Generate ("__o");
			g.Generate (o);
			return o.ToString ();
		}


		[Test]
		public void GenerateIndirectApplication (
				[Values (null, "android.app.Application", "android.support.multidex.MultiDexApplication")] string applicationJavaClass
		)
		{
			var actual      = Generate (typeof (IndirectApplication), applicationJavaClass);
			var expected    = @"package md5f43cdfade412ae71b21bb70a5c2841ab;


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
			var expected = @"package md5f43cdfade412ae71b21bb70a5c2841ab;


public class ExportsMembers
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			""n_GetInstance:()Lmd5f43cdfade412ae71b21bb70a5c2841ab/ExportsMembers;:__export__\n"" +
			""n_GetValue:()Ljava/lang/String;:__export__\n"" +
			""n_methodNamesNotMangled:()V:__export__\n"" +
			""n_CompletelyDifferentName:(Ljava/lang/String;I)Ljava/lang/String;:__export__\n"" +
			""n_methodThatThrows:()V:__export__\n"" +
			""n_methodThatThrowsEmptyArray:()V:__export__\n"" +
			"""";
		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.ExportsMembers, Java.Interop.Tools.JavaCallableWrappers-Tests"", ExportsMembers.class, __md_methods);
	}


	public static md5f43cdfade412ae71b21bb70a5c2841ab.ExportsMembers STATIC_INSTANCE = GetInstance ();


	public java.lang.String VALUE = GetValue ();

	public static md5f43cdfade412ae71b21bb70a5c2841ab.ExportsMembers GetInstance ()
	{
		return n_GetInstance ();
	}

	private static native md5f43cdfade412ae71b21bb70a5c2841ab.ExportsMembers n_GetInstance ();

	public java.lang.String GetValue ()
	{
		return n_GetValue ();
	}

	private native java.lang.String n_GetValue ();


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
			var expected = @"package md5f43cdfade412ae71b21bb70a5c2841ab;


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
		if (getClass () == ExportsConstructors.class)
			mono.android.TypeManager.Activate (""Xamarin.Android.ToolsTests.ExportsConstructors, Java.Interop.Tools.JavaCallableWrappers-Tests"", """", this, new java.lang.Object[] {  });
	}


	public ExportsConstructors (int p0)
	{
		super (p0);
		if (getClass () == ExportsConstructors.class)
			mono.android.TypeManager.Activate (""Xamarin.Android.ToolsTests.ExportsConstructors, Java.Interop.Tools.JavaCallableWrappers-Tests"", """", this, new java.lang.Object[] { p0 });
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
		if (getClass () == ExportsThrowsConstructors.class)
			mono.android.TypeManager.Activate (""Xamarin.Android.ToolsTests.ExportsThrowsConstructors, Java.Interop.Tools.JavaCallableWrappers-Tests"", """", this, new java.lang.Object[] {  });
	}


	public ExportsThrowsConstructors (int p0) throws java.lang.Throwable
	{
		super (p0);
		if (getClass () == ExportsThrowsConstructors.class)
			mono.android.TypeManager.Activate (""Xamarin.Android.ToolsTests.ExportsThrowsConstructors, Java.Interop.Tools.JavaCallableWrappers-Tests"", """", this, new java.lang.Object[] { p0 });
	}


	public ExportsThrowsConstructors (java.lang.String p0)
	{
		super (p0);
		if (getClass () == ExportsThrowsConstructors.class)
			mono.android.TypeManager.Activate (""Xamarin.Android.ToolsTests.ExportsThrowsConstructors, Java.Interop.Tools.JavaCallableWrappers-Tests"", """", this, new java.lang.Object[] { p0 });
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
	}
}

