using MonoDroid.Generation;
using NUnit.Framework;
using System.IO;
using System.Text;

namespace generatortests
{
	[TestFixture]
	class JavaInteropCodeGeneratorTests
	{
		CodeGenerator generator;
		StringBuilder builder;
		StringWriter writer;
		CodeGenerationOptions options;

		[SetUp]
		public void SetUp ()
		{
			builder = new StringBuilder ();
			writer = new StringWriter (builder);
			options = new CodeGenerationOptions {
				CodeGenerationTarget = Xamarin.Android.Binder.CodeGenerationTarget.JavaInterop1,
			};
			generator = options.CodeGenerator;
		}

		[TearDown]
		public void TearDown ()
		{
			writer.Dispose ();
		}

		[Test]
		public void WriteClassHandle ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");

			generator.WriteClassHandle (@class, writer, string.Empty, options, false);

			Assert.AreEqual (@"	internal            static  readonly    JniPeerMembers  _members    = new JniPeerMembers (""com/mypackage/foo"", typeof (foo));
	internal static IntPtr class_ref {
		get {
			return _members.JniPeerType.PeerReference.Handle;
		}
	}

", builder.ToString ());
		}

		[Test]
		public void WriteClassInvokerHandle ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");

			generator.WriteClassInvokerHandle (@class, writer, string.Empty, options, "Com.MyPackage.Foo");

			Assert.AreEqual (@"internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers (""com/mypackage/foo"", typeof (Com.MyPackage.Foo));

public override global::Java.Interop.JniPeerMembers JniPeerMembers {
	get { return _members; }
}

protected override global::System.Type ThresholdType {
	get { return _members.ManagedPeerType; }
}

", builder.ToString ());
		}

		[Test]
		public void WriteFieldIdField ()
		{
			var field = new TestField ("java.lang.String", "bar");

			generator.WriteFieldIdField (field, writer, string.Empty, options);

			//NOTE: not needed for JavaInteropCodeGenerator
			Assert.AreEqual ("", builder.ToString ());
		}

		[Test]
		public void WriteFieldGetBody ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar");
			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList ()), "field.Validate failed!");
			generator.WriteFieldGetBody (field, writer, string.Empty, options, @class);

			Assert.AreEqual (@"const string __id = ""bar.Ljava/lang/String;"";

var __v = _members.InstanceFields.GetObjectValue (__id, this);
return JNIEnv.GetString (__v.Handle, JniHandleOwnership.TransferLocalRef);
", builder.ToString ());
		}

		[Test]
		public void WriteFieldSetBody ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar");
			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList ()), "field.Validate failed!");
			generator.WriteFieldSetBody (field, writer, string.Empty, options, @class);

			Assert.AreEqual (@"const string __id = ""bar.Ljava/lang/String;"";

IntPtr native_value = JNIEnv.NewString (value);
try {
	_members.InstanceFields.SetValue (__id, this, new JniObjectReference (native_value));
} finally {
	JNIEnv.DeleteLocalRef (native_value);
}
", builder.ToString ());
		}

		[Test]
		public void WriteStringField ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar");
			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList ()), "field.Validate failed!");
			generator.WriteField (field, writer, string.Empty, options, @class);

			Assert.AreEqual (@"
// Metadata.xml XPath field reference: path=""/api/package[@name='com.mypackage']/class[@name='foo']/field[@name='bar']""
[Register (""bar"")]
public string bar {
	get {
		const string __id = ""bar.Ljava/lang/String;"";

		var __v = _members.InstanceFields.GetObjectValue (__id, this);
		return JNIEnv.GetString (__v.Handle, JniHandleOwnership.TransferLocalRef);
	}
	set {
		const string __id = ""bar.Ljava/lang/String;"";

		IntPtr native_value = JNIEnv.NewString (value);
		try {
			_members.InstanceFields.SetValue (__id, this, new JniObjectReference (native_value));
		} finally {
			JNIEnv.DeleteLocalRef (native_value);
		}
	}
}
", builder.ToString ());
		}

		[Test]
		public void WriteIntField ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("int", "bar");
			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList ()), "field.Validate failed!");
			generator.WriteField (field, writer, string.Empty, options, @class);

			Assert.AreEqual (@"
// Metadata.xml XPath field reference: path=""/api/package[@name='com.mypackage']/class[@name='foo']/field[@name='bar']""
[Register (""bar"")]
public int bar {
	get {
		const string __id = ""bar.I"";

		var __v = _members.InstanceFields.GetInt32Value (__id, this);
		return __v;
	}
	set {
		const string __id = ""bar.I"";

		try {
			_members.InstanceFields.SetValue (__id, this, value);
		} finally {
		}
	}
}
", builder.ToString ());
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
		public void WriteConstantField ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar").SetConstant ();
			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList ()), "field.Validate failed!");
			generator.WriteField (field, writer, string.Empty, options, @class);

			Assert.AreEqual (@"
// Metadata.xml XPath field reference: path=""/api/package[@name='com.mypackage']/class[@name='foo']/field[@name='bar']""
[Register (""bar"")]
public static string bar {
	get {
		const string __id = ""bar.Ljava/lang/String;"";

		var __v = _members.StaticFields.GetObjectValue (__id);
		return JNIEnv.GetString (__v.Handle, JniHandleOwnership.TransferLocalRef);
	}
}
", builder.ToString ());
		}

		[Test]
		public void WriteConstantFieldWithStringValue ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar").SetConstant ("\"hello\"");
			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList ()), "field.Validate failed!");
			generator.WriteField (field, writer, string.Empty, options, @class);

			Assert.AreEqual (@"// Metadata.xml XPath field reference: path=""/api/package[@name='com.mypackage']/class[@name='foo']/field[@name='bar']""
[Register (""bar"")]
public const string bar = (string) ""hello"";
", builder.ToString ());
		}

		[Test]
		public void WriteConstantFieldWithIntValue ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("int", "bar").SetConstant ("1234");
			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList ()), "field.Validate failed!");
			generator.WriteField (field, writer, string.Empty, options, @class);

			Assert.AreEqual (@"// Metadata.xml XPath field reference: path=""/api/package[@name='com.mypackage']/class[@name='foo']/field[@name='bar']""
[Register (""bar"")]
public const int bar = (int) 1234;
", builder.ToString ());
		}
	}
}
