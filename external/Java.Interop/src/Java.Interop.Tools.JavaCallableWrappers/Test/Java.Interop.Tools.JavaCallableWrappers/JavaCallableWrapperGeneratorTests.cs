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
		public void GenerateInParallel ()
		{
			var assemblyDef = AssemblyDefinition.ReadAssembly (typeof (DefaultName).Assembly.Location);
			var types       = new []{
				typeof (AbstractClassInvoker),
				typeof (AbstractClass),
				typeof (ActivityName),
				typeof (ApplicationName),
				typeof (DefaultName),
				typeof (DefaultName.A),
				typeof (DefaultName.A.B),
				typeof (DefaultName.C.D),
				// Skip because this will produce nested types
				// typeof (ExampleOuterClass),
				// typeof (ExampleOuterClass.ExampleInnerClass),
				typeof (InstrumentationName),
				// Skip because this will produce nested types
				// typeof (NonStaticOuterClass),
				// typeof (NonStaticOuterClass.NonStaticInnerClass),
				typeof (ProviderName),
				typeof (ReceiverName),
				typeof (RegisterName),
				typeof (RegisterName.DefaultNestedName),
				typeof (RegisterName.OverrideNestedName),
				typeof (ServiceName),
			};
			var typeDefs    = types.Select (t => SupportDeclarations.GetTypeDefinition (t, assemblyDef))
				.ToList ();

			var tasks       = typeDefs.Select (type => Task.Run (() => {
					var g = new JavaCallableWrapperGenerator (type, log: Console.WriteLine);
					var o = new StringWriter ();
					g.Generate (o);
					var r = new StringReader (o.ToString ());
					var l = r.ReadLine ();
					if (!l.StartsWith ("package ", StringComparison.Ordinal))
						throw new InvalidOperationException ($"Invalid JCW for {type.FullName}!");
					var p = l.Substring ("package ".Length);
					p = p.Substring (0, p.Length - 1);
					l = r.ReadLine ();
					if (l.Length != 0)
						throw new InvalidOperationException ($"Invalid JCW for {type.FullName}! (Missing newline)");
					l = r.ReadLine ();
					if (l.Length != 0)
						throw new InvalidOperationException ($"Invalid JCW for {type.FullName}! (Missing 2nd newline)");
					l = r.ReadLine ();
					string c = null;
					if (l.StartsWith ("public class ", StringComparison.Ordinal))
						c = l.Substring ("public class ".Length);
					else if (l.StartsWith ("public abstract class ", StringComparison.Ordinal))
						c = l.Substring ("public abstract class ".Length);
					else
						throw new InvalidOperationException ($"Invalid JCW for {type.FullName}! (Missing class)");
					return p + "/" + c;
			})).ToArray ();
			Task.WaitAll (tasks);
			for (int i = 0; i < types.Length; ++i) {
				Assert.AreEqual (JniType.ToJniName (typeDefs [i]),  tasks [i].Result);
				Assert.AreEqual (JniType.ToJniName (types [i]),     tasks [i].Result);
			}
		}

		[Test]
		public void GenerateApplication ()
		{
			var actual      = Generate (typeof (ApplicationName));
			var expected    = @"package application;


public class Name
	extends android.app.Application
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"""";
	}

	public Name ()
	{
		mono.MonoPackageManager.setContext (this);
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

		static string Generate (Type type)
		{
			var td  = SupportDeclarations.GetTypeDefinition (type);
			var g   = new JavaCallableWrapperGenerator (td, null);
			var o   = new StringWriter ();
			g.Generate ("__o");
			g.Generate (o);
			return o.ToString ();
		}


		[Test]
		public void GenerateIndirectApplication ()
		{
			var actual      = Generate (typeof (IndirectApplication));
			var expected    = @"package md5fef72cac46d04ae5bdc90af5bb6221ad;


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
			var expected = @"package md5fef72cac46d04ae5bdc90af5bb6221ad;


public class ExportsMembers
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			""n_GetInstance:()Lmd5fef72cac46d04ae5bdc90af5bb6221ad/ExportsMembers;:__export__\n"" +
			""n_GetValue:()Ljava/lang/String;:__export__\n"" +
			""n_methodNamesNotMangled:()V:__export__\n"" +
			""n_CompletelyDifferentName:(Ljava/lang/String;I)Ljava/lang/String;:__export__\n"" +
			"""";
		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.ExportsMembers, Java.Interop.Tools.JavaCallableWrappers-Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"", ExportsMembers.class, __md_methods);
	}


	public static md5fef72cac46d04ae5bdc90af5bb6221ad.ExportsMembers STATIC_INSTANCE = GetInstance ();


	public java.lang.String VALUE = GetValue ();

	public static md5fef72cac46d04ae5bdc90af5bb6221ad.ExportsMembers GetInstance ()
	{
		return n_GetInstance ();
	}

	private static native md5fef72cac46d04ae5bdc90af5bb6221ad.ExportsMembers n_GetInstance ();

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
			var expected = @"package md5fef72cac46d04ae5bdc90af5bb6221ad;


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
		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.ExampleOuterClass, Java.Interop.Tools.JavaCallableWrappers-Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"", ExampleOuterClass.class, __md_methods);
		__md_1_methods = 
			"""";
		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.ExampleOuterClass+ExampleInnerClass, Java.Interop.Tools.JavaCallableWrappers-Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"", ExampleOuterClass_ExampleInnerClass.class, __md_1_methods);
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
		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.RegisterName+DefaultNestedName, Java.Interop.Tools.JavaCallableWrappers-Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"", Name_DefaultNestedName.class, __md_methods);
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
		mono.android.Runtime.register (""Xamarin.Android.ToolsTests.RegisterName+OverrideNestedName, Java.Interop.Tools.JavaCallableWrappers-Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"", Name$Override.class, __md_methods);
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

