using MonoDroid.Generation;
using NUnit.Framework;
using System.IO;
using System.Text;

namespace generatortests
{
	[TestFixture]
	public class XamarinAndroidCodeGeneratorTests
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
				CodeGenerationTarget = Xamarin.Android.Binder.CodeGenerationTarget.XamarinAndroid,
			};
			generator = options.CodeGenerator;
		}

		[TearDown]
		public void TearDown ()
		{
			writer.Dispose ();
		}

		[Test]
		public void WriteClassHandle()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");

			generator.WriteClassHandle (@class, writer, string.Empty, options, false);

			Assert.AreEqual (@"	internal static IntPtr java_class_handle;
	internal static IntPtr class_ref {
		get {
			return JNIEnv.FindClass (""com/mypackage/foo"", ref java_class_handle);
		}
	}

", builder.ToString ());
		}

		[Test]
		public void WriteClassInvokerHandle ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");

			generator.WriteClassInvokerHandle (@class, writer, string.Empty, options, "Com.MyPackage.Foo");

			Assert.AreEqual (@"protected override global::System.Type ThresholdType {
	get { return typeof (Com.MyPackage.Foo); }
}

", builder.ToString ());
		}

		[Test]
		public void WriteFieldIdField ()
		{
			var field = new TestField ("java.lang.String", "bar");

			generator.WriteFieldIdField (field, writer, string.Empty, options);

			Assert.AreEqual (@"static IntPtr bar_jfieldId;
", builder.ToString ());
		}

		[Test]
		public void WriteFieldGetBody ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar");
			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList ()), "field.Validate failed!");
			generator.WriteFieldGetBody (field, writer, string.Empty, options, @class);

			Assert.AreEqual (@"if (bar_jfieldId == IntPtr.Zero)
	bar_jfieldId = JNIEnv.GetFieldID (class_ref, ""bar"", ""Ljava/lang/String;"");
IntPtr __ret = JNIEnv.GetObjectField (((global::Java.Lang.Object) this).Handle, bar_jfieldId);
return JNIEnv.GetString (__ret, JniHandleOwnership.TransferLocalRef);
", builder.ToString ());
		}

		[Test]
		public void WriteFieldSetBody ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var field = new TestField ("java.lang.String", "bar");
			Assert.IsTrue (field.Validate (options, new GenericParameterDefinitionList ()), "field.Validate failed!");
			generator.WriteFieldSetBody (field, writer, string.Empty, options, @class);

			Assert.AreEqual (@"if (bar_jfieldId == IntPtr.Zero)
	bar_jfieldId = JNIEnv.GetFieldID (class_ref, ""bar"", ""Ljava/lang/String;"");
IntPtr native_value = JNIEnv.NewString (value);
try {
	JNIEnv.SetField (((global::Java.Lang.Object) this).Handle, bar_jfieldId, native_value);
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

			Assert.AreEqual (@"static IntPtr bar_jfieldId;

// Metadata.xml XPath field reference: path=""/api/package[@name='com.mypackage']/class[@name='foo']/field[@name='bar']""
[Register (""bar"")]
public string bar {
	get {
		if (bar_jfieldId == IntPtr.Zero)
			bar_jfieldId = JNIEnv.GetFieldID (class_ref, ""bar"", ""Ljava/lang/String;"");
		IntPtr __ret = JNIEnv.GetObjectField (((global::Java.Lang.Object) this).Handle, bar_jfieldId);
		return JNIEnv.GetString (__ret, JniHandleOwnership.TransferLocalRef);
	}
	set {
		if (bar_jfieldId == IntPtr.Zero)
			bar_jfieldId = JNIEnv.GetFieldID (class_ref, ""bar"", ""Ljava/lang/String;"");
		IntPtr native_value = JNIEnv.NewString (value);
		try {
			JNIEnv.SetField (((global::Java.Lang.Object) this).Handle, bar_jfieldId, native_value);
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

			Assert.AreEqual (@"static IntPtr bar_jfieldId;

// Metadata.xml XPath field reference: path=""/api/package[@name='com.mypackage']/class[@name='foo']/field[@name='bar']""
[Register (""bar"")]
public int bar {
	get {
		if (bar_jfieldId == IntPtr.Zero)
			bar_jfieldId = JNIEnv.GetFieldID (class_ref, ""bar"", ""I"");
		return JNIEnv.GetIntField (((global::Java.Lang.Object) this).Handle, bar_jfieldId);
	}
	set {
		if (bar_jfieldId == IntPtr.Zero)
			bar_jfieldId = JNIEnv.GetFieldID (class_ref, ""bar"", ""I"");
		try {
			JNIEnv.SetField (((global::Java.Lang.Object) this).Handle, bar_jfieldId, value);
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

			Assert.AreEqual (@"static IntPtr bar_jfieldId;

// Metadata.xml XPath field reference: path=""/api/package[@name='com.mypackage']/class[@name='foo']/field[@name='bar']""
[Register (""bar"")]
public static string bar {
	get {
		if (bar_jfieldId == IntPtr.Zero)
			bar_jfieldId = JNIEnv.GetStaticFieldID (class_ref, ""bar"", ""Ljava/lang/String;"");
		IntPtr __ret = JNIEnv.GetStaticObjectField (class_ref, bar_jfieldId);
		return JNIEnv.GetString (__ret, JniHandleOwnership.TransferLocalRef);
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
