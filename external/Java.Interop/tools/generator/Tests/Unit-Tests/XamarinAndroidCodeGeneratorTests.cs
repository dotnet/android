using MonoDroid.Generation;
using NUnit.Framework;
using Xamarin.Android.Binder;

namespace generatortests
{
	[TestFixture]
	class XamarinAndroidCodeGeneratorTests : CodeGeneratorTests
	{
		protected override CodeGenerationTarget Target => CodeGenerationTarget.XamarinAndroid;

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

		[Test]
		public void WriteMethodIdField ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar");
			generator.WriteMethodIdField (method, writer, string.Empty, options);

			Assert.AreEqual (@"static IntPtr id_bar;
", builder.ToString ());
		}

		[Test]
		public void WriteMethodBody ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar");
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList ()), "method.Validate failed!");
			generator.WriteMethodBody (method, writer, string.Empty, options);

			Assert.AreEqual (@"if (id_bar == IntPtr.Zero)
	id_bar = JNIEnv.GetMethodID (class_ref, ""bar"", ""()V"");
try {

	if (((object) this).GetType () == ThresholdType)
		JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_bar);
	else
		JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, ""bar"", ""()V""));
} finally {
}
", builder.ToString ());
		}

		[Test]
		public void WriteVoidMethod ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar");
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList ()), "method.Validate failed!");
			generator.WriteMethod (method, writer, string.Empty, options, @class, true);

			Assert.AreEqual (@"static Delegate cb_bar;
#pragma warning disable 0169
static Delegate GetbarHandler ()
{
	if (cb_bar == null)
		cb_bar = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr>) n_bar);
	return cb_bar;
}

static void n_bar (IntPtr jnienv, IntPtr native__this)
{
	com.mypackage.foo __this = global::Java.Lang.Object.GetObject<com.mypackage.foo> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
	__this.bar ();
}
#pragma warning restore 0169

static IntPtr id_bar;
// Metadata.xml XPath method reference: path=""/api/package[@name='com.mypackage']/class[@name='foo']/method[@name='bar' and count(parameter)=0]""
[Register (""bar"", ""()V"", ""GetbarHandler"")]
public virtual unsafe void bar ()
{
	if (id_bar == IntPtr.Zero)
		id_bar = JNIEnv.GetMethodID (class_ref, ""bar"", ""()V"");
	try {

		if (((object) this).GetType () == ThresholdType)
			JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_bar);
		else
			JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, ""bar"", ""()V""));
	} finally {
	}
}

", builder.ToString ());
		}

		[Test]
		public void WriteIntMethod ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar", @return: "int");
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList ()), "method.Validate failed!");
			generator.WriteMethod (method, writer, string.Empty, options, @class, true);

			Assert.AreEqual (@"static Delegate cb_bar;
#pragma warning disable 0169
static Delegate GetbarHandler ()
{
	if (cb_bar == null)
		cb_bar = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, int>) n_bar);
	return cb_bar;
}

static int n_bar (IntPtr jnienv, IntPtr native__this)
{
	com.mypackage.foo __this = global::Java.Lang.Object.GetObject<com.mypackage.foo> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
	return __this.bar ();
}
#pragma warning restore 0169

static IntPtr id_bar;
// Metadata.xml XPath method reference: path=""/api/package[@name='com.mypackage']/class[@name='foo']/method[@name='bar' and count(parameter)=0]""
[Register (""bar"", ""()I"", ""GetbarHandler"")]
public virtual unsafe int bar ()
{
	if (id_bar == IntPtr.Zero)
		id_bar = JNIEnv.GetMethodID (class_ref, ""bar"", ""()I"");
	try {

		if (((object) this).GetType () == ThresholdType)
			return JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_bar);
		else
			return JNIEnv.CallNonvirtualIntMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, ""bar"", ""()I""));
	} finally {
	}
}

", builder.ToString ());
		}

		[Test]
		public void WriteStringMethod ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar", @return: "java.lang.String");
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList ()), "method.Validate failed!");
			generator.WriteMethod (method, writer, string.Empty, options, @class, true);

			Assert.AreEqual (@"static Delegate cb_bar;
#pragma warning disable 0169
static Delegate GetbarHandler ()
{
	if (cb_bar == null)
		cb_bar = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr>) n_bar);
	return cb_bar;
}

static IntPtr n_bar (IntPtr jnienv, IntPtr native__this)
{
	com.mypackage.foo __this = global::Java.Lang.Object.GetObject<com.mypackage.foo> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
	return JNIEnv.NewString (__this.bar ());
}
#pragma warning restore 0169

static IntPtr id_bar;
// Metadata.xml XPath method reference: path=""/api/package[@name='com.mypackage']/class[@name='foo']/method[@name='bar' and count(parameter)=0]""
[Register (""bar"", ""()Ljava/lang/String;"", ""GetbarHandler"")]
public virtual unsafe string bar ()
{
	if (id_bar == IntPtr.Zero)
		id_bar = JNIEnv.GetMethodID (class_ref, ""bar"", ""()Ljava/lang/String;"");
	try {

		if (((object) this).GetType () == ThresholdType)
			return JNIEnv.GetString (JNIEnv.CallObjectMethod (((global::Java.Lang.Object) this).Handle, id_bar), JniHandleOwnership.TransferLocalRef);
		else
			return JNIEnv.GetString (JNIEnv.CallNonvirtualObjectMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, ""bar"", ""()Ljava/lang/String;"")), JniHandleOwnership.TransferLocalRef);
	} finally {
	}
}

", builder.ToString ());
		}

		[Test]
		public void WriteFinalVoidMethod ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetFinal ();
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList ()), "method.Validate failed!");
			generator.WriteMethod (method, writer, string.Empty, options, @class, true);

			Assert.AreEqual (@"static IntPtr id_bar;
// Metadata.xml XPath method reference: path=""/api/package[@name='com.mypackage']/class[@name='foo']/method[@name='bar' and count(parameter)=0]""
[Register (""bar"", ""()V"", """")]
public unsafe void bar ()
{
	if (id_bar == IntPtr.Zero)
		id_bar = JNIEnv.GetMethodID (class_ref, ""bar"", ""()V"");
	try {
		JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_bar);
	} finally {
	}
}

", builder.ToString ());
		}

		[Test]
		public void WriteAbstractVoidMethod ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetAbstract ();
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList ()), "method.Validate failed!");
			generator.WriteMethod (method, writer, string.Empty, options, @class, true);

			Assert.AreEqual (@"static Delegate cb_bar;
#pragma warning disable 0169
static Delegate GetbarHandler ()
{
	if (cb_bar == null)
		cb_bar = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr>) n_bar);
	return cb_bar;
}

static void n_bar (IntPtr jnienv, IntPtr native__this)
{
	com.mypackage.foo __this = global::Java.Lang.Object.GetObject<com.mypackage.foo> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
	__this.bar ();
}
#pragma warning restore 0169

static IntPtr id_bar;
// Metadata.xml XPath method reference: path=""/api/package[@name='com.mypackage']/class[@name='foo']/method[@name='bar' and count(parameter)=0]""
[Register (""bar"", ""()V"", ""GetbarHandler"")]
public virtual unsafe void bar ()
{
	if (id_bar == IntPtr.Zero)
		id_bar = JNIEnv.GetMethodID (class_ref, ""bar"", ""()V"");
	try {
		JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_bar);
	} finally {
	}
}

", builder.ToString ());
		}

		[Test]
		public void WriteStaticVoidMethod ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetStatic ();
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList ()), "method.Validate failed!");
			generator.WriteMethod (method, writer, string.Empty, options, @class, true);

			Assert.AreEqual (@"static IntPtr id_bar;
// Metadata.xml XPath method reference: path=""/api/package[@name='com.mypackage']/class[@name='foo']/method[@name='bar' and count(parameter)=0]""
[Register (""bar"", ""()V"", """")]
public static unsafe void bar ()
{
	if (id_bar == IntPtr.Zero)
		id_bar = JNIEnv.GetStaticMethodID (class_ref, ""bar"", ""()V"");
	try {
		JNIEnv.CallStaticVoidMethod  (class_ref, id_bar);
	} finally {
	}
}

", builder.ToString ());
		}

		[Test]
		public void WriteAsyncifiedVoidMethod ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetAsyncify ();
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList ()), "method.Validate failed!");
			generator.WriteMethod (method, writer, string.Empty, options, @class, true);

			StringAssert.Contains (@"public global::System.Threading.Tasks.Task barAsync ()
{
	return global::System.Threading.Tasks.Task.Run (() => bar ());
}", builder.ToString ());
		}

		[Test]
		public void WriteAsyncifiedIntMethod ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar", @return: "int").SetAsyncify ();
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList ()), "method.Validate failed!");
			generator.WriteMethod (method, writer, string.Empty, options, @class, true);

			StringAssert.Contains (@"public global::System.Threading.Tasks.Task<int> barAsync ()
{
	return global::System.Threading.Tasks.Task.Run (() => bar ());
}", builder.ToString ());
		}

		[Test]
		public void WriteProtectedMethod ()
		{
			var @class = new TestClass ("java.lang.Object", "com.mypackage.foo");
			var method = new TestMethod (@class, "bar").SetVisibility ("protected");
			Assert.IsTrue (method.Validate (options, new GenericParameterDefinitionList ()), "method.Validate failed!");
			generator.WriteMethod (method, writer, string.Empty, options, @class, true);

			StringAssert.Contains (@"protected virtual unsafe void bar ()", builder.ToString ());
		}
	}
}
