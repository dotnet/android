using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using Android.App;
using Android.Graphics;
using Android.Runtime;
using Android.Content;

using Java.Interop;

using NUnit.Framework;
using Android.OS;

namespace Java.InteropTests
{
	[TestFixture]
	public class JnienvTest
	{
		[Test]
		public void TestMyPaintColor ()
		{
			using (var p = new MyPaint ()) {
				var g = JNIEnv.GetMethodID(p.Class.Handle, "getColor", "()I");
				int c = JNIEnv.CallIntMethod(p.Handle, g);
				Assert.AreEqual (0x11223344, c);
				var s = JNIEnv.GetMethodID(p.Class.Handle, "setColor", "(I)V");
				JNIEnv.CallVoidMethod (p.Handle, s, new JValue (0x22331144));
				Assert.AreEqual (0x22331144, p.SetColor.ToArgb ());
			}
		}

		[DllImport ("reuse-threads")]
		static extern int rt_register_type_on_new_thread (string java_type_namem, IntPtr class_loader);

		delegate void CB (IntPtr jnienv, IntPtr java_instance);

		[DllImport ("reuse-threads")]
		static extern int rt_invoke_callback_on_new_thread (CB cb);

		[Test]
		public void RegisterTypeOnNewNativeThread ()
		{
			Java.Lang.JavaSystem.LoadLibrary ("reuse-threads");
			int ret = rt_register_type_on_new_thread ("from.NewThreadOne", Application.Context.ClassLoader.Handle);
			Assert.AreEqual (0, ret, $"Java type registration on a new thread failed with code {ret}");
		}

		[Test]
		public void RegisterTypeOnNewJavaThread ()
		{
			var thread = new MyRegistrationThread ();
			thread.Start ();
			thread.Join (5000);
			Assert.AreNotEqual (null, thread.Instance, "Failed to register instance of a class on new thread");
		}

		[Test]
		public void ThreadReuse ()
		{
			Java.Lang.JavaSystem.LoadLibrary ("reuse-threads");
			CB cb = (env, instance) => {
				Console.WriteLine ("CrossThreadObjectInteractions: JNIEnv.Handle={0} env={1}, instance={2}",
						JNIEnv.Handle.ToString ("x"), env.ToString ("x"), instance.ToString ("x"));
				if (env != JNIEnv.Handle)
					Console.WriteLine ("GOOD: they should differ (on the second call)....");
				if (instance == IntPtr.Zero)
					return;
				using (var o = Java.Lang.Object.GetObject<Java.Lang.Object>(env, instance, JniHandleOwnership.DoNotTransfer)) {
					Console.WriteLine ("CrossThreadObjectInteractions: o.Handle={0}", o.Handle.ToString ("x"));
				}
			};
			rt_invoke_callback_on_new_thread (cb);
			GC.KeepAlive (cb);
		}

		[Test]
		public void DeleteLrefOnWrongThread ()
		{
			Console.WriteLine ("Delete JNI local refs on wrong thread...");
			IntPtr lref = IntPtr.Zero;
			var t = new Thread (() => {
					lref = JNIEnv.NewArray(new[]{1,2,3});
			});
			Console.WriteLine ("Do we die?");
			JNIEnv.DeleteLocalRef (lref);
			Console.WriteLine ("still alive!");
		}

		static  readonly  bool  HaveJavaInterop   = AppDomain.CurrentDomain.GetAssemblies ().Any (a => a.FullName.StartsWith ("Java.Interop,"));

		[Test]
		public void InvokingNullInstanceDoesNotCrashDalvik ()
		{
			using (var o = new Java.Lang.Object (IntPtr.Zero, JniHandleOwnership.TransferLocalRef)) {
				Assert.AreEqual (IntPtr.Zero, o.Handle);
				if (HaveJavaInterop) {
					Assert.Throws<ObjectDisposedException>(() => o.ToString ());
				} else {
					Assert.Throws<ArgumentException>(() => o.ToString ());
				}
			}
		}

		[Test]
		public void NewOpenGenericTypeThrows ()
		{
			try {
				var lrefInstance = JNIEnv.StartCreateInstance (typeof (GenericHolder<>), "()V");
				JNIEnv.FinishCreateInstance (lrefInstance, "()V");
				Assert.Fail ("SHOULD NOT BE REACHED: creation of open generic types is not supported");
			} catch (NotSupportedException) {
			}
		}

		[Test]
		public void NewClosedGenericTypeWorks ()
		{
			using (var holder = new GenericHolder<int>()) {
			}
		}

		[Test]
		public void NewObjectArrayWithNullArray ()
		{
			Assert.AreEqual (IntPtr.Zero, JNIEnv.NewObjectArray<Java.Lang.Object> (null), "#1");
		}

		[Test]
		public void NewObjectArrayWithObjectArray ()
		{
			var array = JNIEnv.NewObjectArray<Java.Lang.String> (new Java.Lang.String [0]);
			Assert.AreNotEqual (IntPtr.Zero, array, "#1");
			Assert.AreEqual (0, JNIEnv.GetArrayLength (array), "#2");
			Assert.AreEqual ("[Ljava/lang/String;", JNIEnv.GetClassNameFromInstance (array), "#3");
			JNIEnv.DeleteLocalRef (array);

			array = JNIEnv.NewObjectArray<Java.Lang.String> (new Java.Lang.String [1] { new Java.Lang.String ("str")});
			Assert.AreNotEqual (IntPtr.Zero, array, "#4");
			Assert.AreEqual (1, JNIEnv.GetArrayLength (array), "#5");
			Assert.AreEqual ("[Ljava/lang/String;", JNIEnv.GetClassNameFromInstance (array), "#6");
			JNIEnv.DeleteLocalRef (array);
		}

		[Test]
		public void NewObjectArrayWithNullElement ()
		{
			var array = JNIEnv.NewObjectArray<Java.Lang.String> (new Java.Lang.String [1]);
			Assert.AreNotEqual (IntPtr.Zero, array, "#2");
			Assert.AreEqual (1, JNIEnv.GetArrayLength (array), "#3");
			Assert.AreEqual ("[Ljava/lang/String;", JNIEnv.GetClassNameFromInstance (array), "#4");
			JNIEnv.DeleteLocalRef (array);
		}

		[Test]
		public void NewObjectArrayWithIntArray ()
		{
			var array = JNIEnv.NewObjectArray<int> (new int [1]);
			Assert.AreNotEqual (IntPtr.Zero, array, "#1");
			Assert.AreEqual (1, JNIEnv.GetArrayLength (array), "#2");
			Assert.AreEqual ("[Ljava/lang/Integer;", JNIEnv.GetClassNameFromInstance (array), "#3");
			JNIEnv.DeleteLocalRef (array);
		}

		[Test]
		public void NewObjectArrayWithIntArrayAndEmptyArray ()
		{
			//empty array gives the right type
			var array = JNIEnv.NewObjectArray<int> (new int [0]);
			Assert.AreNotEqual (IntPtr.Zero, array, "#1");
			Assert.AreEqual (0, JNIEnv.GetArrayLength (array), "#2");
			Assert.AreEqual ("[Ljava/lang/Integer;", JNIEnv.GetClassNameFromInstance (array), "#3");
			JNIEnv.DeleteLocalRef (array);
		}

		[Test]
		public void NewObjectArrayWithNonJavaType ()
		{
			//empty array gives the right type
			var array = JNIEnv.NewObjectArray<Type> (new Type [1] { typeof (Type) });
			Assert.AreNotEqual (IntPtr.Zero, array, "#1");
			Assert.AreEqual ("[Ljava/lang/Object;", JNIEnv.GetClassNameFromInstance (array), "#2");
			JNIEnv.DeleteLocalRef (array);
		}

		[Test]
		public void NewObjectArrayWithNonJavaTypeAndEmptyArray ()
		{
			//empty array gives the right type
			var array = JNIEnv.NewObjectArray<Type> (new Type [0]);
			Assert.AreNotEqual (IntPtr.Zero, array, "#1");
			Assert.AreEqual ("[Ljava/lang/Object;", JNIEnv.GetClassNameFromInstance (array), "#2");
			JNIEnv.DeleteLocalRef (array);
		}

		[Test]
		[Ignore ("This crashes on the emulator")]
		public void NewObjectArrayWithBadValues ()
		{
			try {
				JNIEnv.NewObjectArray (-1, JNIEnv.FindClass (typeof (Java.Lang.Object)));
				Assert.Fail ("Must throw");
			} catch (Java.Lang.OutOfMemoryError e) {
				//XXX shouldn't this exception be an ArgumentException?
			}

			try {
				JNIEnv.NewObjectArray (1, IntPtr.Zero);
				Assert.Fail ("Must throw");
			} catch (Java.Lang.NullPointerException e) {
				//XXX shouldn't this exception be an ArgumentException?
			}
		}

		[Test]
		public void NewObjectArray_UsesOnlyTypeParameter ()
		{
			using (var s = new Java.Lang.String ("foo"))
			using (var i = new Java.Lang.Integer (42)) {
				var array = JNIEnv.NewObjectArray<Java.Lang.Object> (s, i);
				Assert.AreNotEqual (IntPtr.Zero, array, "#1");
				Assert.AreEqual ("[Ljava/lang/Object;", JNIEnv.GetClassNameFromInstance (array), "#2");
				Assert.AreEqual (2, JNIEnv.GetArrayLength (array));
				JNIEnv.DeleteLocalRef (array);
			}
		}

		[Test]
		public void SetField_PermitNullValues ()
		{
			using (var resource = new Intent.ShortcutIconResource ()) {
				var f = JNIEnv.GetFieldID (resource.Class.Handle, "packageName", "Ljava/lang/String;");
				Console.WriteLine ("# f=0x{0}", f.ToString ("x"));
				resource.PackageName = null;
				Assert.AreEqual (null, resource.PackageName);
			}
		}

		[Test]
		public void CreateTypeWithExportedMethods ()
		{
			using (var e = new ContainsExportedMethods ()) {
				e.Exported ();
				Assert.AreEqual (1, e.Count);
				IntPtr m = JNIEnv.GetMethodID (e.Class.Handle, "Exported", "()V");
				JNIEnv.CallVoidMethod (e.Handle, m);
				Assert.AreEqual (2, e.Count);
			}
		}

		[Test]
		public void ActivatedDirectObjectSubclassesShouldBeRegistered ()
		{
			if (Build.VERSION.SdkInt <= BuildVersionCodes.GingerbreadMr1)
				Assert.Ignore ("Skipping test due to Bug #34141");

			using (var ContainsExportedMethods_class  = Java.Lang.Class.FromType (typeof (ContainsExportedMethods))) {
				var ContainsExportedMethods_init = JNIEnv.GetMethodID (ContainsExportedMethods_class.Handle, "<init>", "()V");

				var o = JNIEnv.StartCreateInstance (ContainsExportedMethods_class.Handle, ContainsExportedMethods_init);
				JNIEnv.FinishCreateInstance (o, ContainsExportedMethods_class.Handle, ContainsExportedMethods_init);

				/*
				 * Before the fix to to Bxc#32311, this will trigger an ART abort.
				 *
				 * StartCreateInstance()+FinishCreateInstance() will cause a ContainsExportedMethods instance
				 * to be created, but it (1) isn't "registered" (meaning Java.Lang.Object.PeekObject(IntPtr)
				 * will return null) and (2) doesn't even contain a JNI Global Reference, but instead a
				 * JNI *Local* Ref!
				 *
				 * This causes ART to abort when attempting to use an invalid JNI local reference from the finalizer.
				 */
				GC.Collect ();
				GC.WaitForPendingFinalizers ();

				var v = Java.Lang.Object.GetObject<ContainsExportedMethods>(o, JniHandleOwnership.TransferLocalRef);
				Assert.IsNotNull (v);
				Assert.IsTrue (v.Constructed);
				v.Dispose ();
			}
		}

		[Test]
		public void ActivatedDirectThrowableSubclassesShouldBeRegistered ()
		{
			if (Build.VERSION.SdkInt <= BuildVersionCodes.GingerbreadMr1)
				Assert.Ignore ("Skipping test due to Bug #34141");
			
			Console.Error.WriteLine ($"# jonp: BEGIN ActivatedDirectThrowableSubclassesShouldBeRegistered!!!");

			using (var ThrowableActivatedFromJava_class  = Java.Lang.Class.FromType (typeof (ThrowableActivatedFromJava))) {
				var ThrowableActivatedFromJava_init = JNIEnv.GetMethodID (ThrowableActivatedFromJava_class.Handle, "<init>", "()V");

				var o = JNIEnv.StartCreateInstance (ThrowableActivatedFromJava_class.Handle, ThrowableActivatedFromJava_init);
				JNIEnv.FinishCreateInstance (o, ThrowableActivatedFromJava_class.Handle, ThrowableActivatedFromJava_init);

				GC.Collect ();
				GC.WaitForPendingFinalizers ();

				var v = Java.Lang.Object.GetObject<ThrowableActivatedFromJava>(o, JniHandleOwnership.TransferLocalRef);
				Assert.IsNotNull (v);
				Assert.IsTrue (v.Constructed);
				v.Dispose ();
			}
			Console.Error.WriteLine ($"# jonp:   END ActivatedDirectThrowableSubclassesShouldBeRegistered!!!");
		}

		[Test]
		public void ConversionsAndThreadsAndInstanceMappingsOhMy ()
		{
			IntPtr lrefJliArray = JNIEnv.NewObjectArray<int> (new[]{1});
			IntPtr grefJliArray = JNIEnv.NewGlobalRef (lrefJliArray);
			JNIEnv.DeleteLocalRef (lrefJliArray);

			Java.Lang.Object[] jarray = (Java.Lang.Object[])
				JNIEnv.GetArray (grefJliArray, JniHandleOwnership.DoNotTransfer, typeof(Java.Lang.Object));

			Exception ignore_t1 = null;
			Exception ignore_t2 = null;

			var t1 = new Thread (() => {
				int[] output_array1 = new int[1];
				for (int i = 0; i < 2000; ++i) {
					Console.WriteLine ("# t1 iter: {0}", i);
					try {
						JNIEnv.CopyObjectArray (grefJliArray, output_array1);
					} catch (Exception e) {
						ignore_t1 = e;
						break;
					}
				}
			});
			var t2 = new Thread (() => {
				for (int i = 0; i < 2000; ++i) {
					Console.WriteLine ("# t2 iter: {0}", i);
					try {
						JNIEnv.GetArray<int>(jarray);
					} catch (Exception e) {
						ignore_t2 = e;
						break;
					}
				}
			});

			t1.Start ();
			t2.Start ();
			t1.Join ();
			t2.Join ();

			for (int i = 0; i < jarray.Length; ++i) {
				jarray [i].Dispose ();
				jarray [i]  = null;
			}

			JNIEnv.DeleteGlobalRef (grefJliArray);

			Assert.IsNull (ignore_t1, string.Format ("No exception should be thrown [t1]! Got: {0}", ignore_t1));
			Assert.IsNull (ignore_t2, string.Format ("No exception should be thrown [t2]! Got: {0}", ignore_t2));
		}

		[Test]
		public void MoarThreadingTests ()
		{
			IntPtr lrefJliArray = JNIEnv.NewObjectArray<int> (new[]{1});
			IntPtr grefJliArray = JNIEnv.NewGlobalRef (lrefJliArray);
			JNIEnv.DeleteLocalRef (lrefJliArray);

			Exception ignore_t1 = null;
			Exception ignore_t2 = null;

			var t1 = new Thread (() => {
				int[] output_array1 = new int[1];
				for (int i = 0; i < 2000; ++i) {
					Console.WriteLine ("# t1 iter: {0}", i);
					try {
						JNIEnv.CopyObjectArray (grefJliArray, output_array1);
					} catch (Exception e) {
						ignore_t1 = e;
						break;
					}
				}
			});
			var t2 = new Thread (() => {
				for (int i = 0; i < 2000; ++i) {
					Console.WriteLine ("# t2 iter: {0}", i);
					try {
						JNIEnv.GetObjectArray (grefJliArray, new[]{typeof (int)});
					} catch (Exception e) {
						ignore_t2 = e;
						break;
					}
				}
			});

			t1.Start ();
			t2.Start ();
			t1.Join ();
			t2.Join ();

			JNIEnv.DeleteGlobalRef (grefJliArray);

			Assert.IsNull (ignore_t1, string.Format ("No exception should be thrown [t1]! Got: {0}", ignore_t1));
			Assert.IsNull (ignore_t2, string.Format ("No exception should be thrown [t2]! Got: {0}", ignore_t2));
		}

		[Test]
		public void JavaToManagedTypeMapping ()
		{
			Type m = Java.Interop.TypeManager.GetJavaToManagedType ("android/content/res/Resources");
			Assert.AreNotEqual (null, m);
			m = Java.Interop.TypeManager.GetJavaToManagedType ("this/type/does/not/exist");
			Assert.AreEqual (null, m);
		}

		[Test]
		public void ManagedToJavaTypeMapping ()
		{
			Type type = typeof(Activity);
			string m = JNIEnv.TypemapManagedToJava (type);
			Assert.AreNotEqual (null, m, "`Activity` subclasses Java.Lang.Object, it should be in the typemap!");

			type = typeof (JnienvTest);
			m = JNIEnv.TypemapManagedToJava (type);
			Assert.AreEqual (null, m, "`JnienvTest` does *not* subclass Java.Lang.Object, it should *not* be in the typemap!");
		}

		[Test]
		public void DoNotLeakWeakReferences ()
		{
			GC.Collect ();
			GC.WaitForPendingFinalizers ();

			var surfaced    = Runtime.GetSurfacedObjects ();
			int startCount  = surfaced.Count;

			Assert.IsTrue (surfaced.All (s => s.Target != null), "#1");

			WeakReference r = null;
			var t = new Thread (() => {
				var c = new MyCb ();
				Assert.AreEqual (startCount + 1, Runtime.GetSurfacedObjects ().Count, "#2");
				r = new WeakReference (c);
			});
			t.Start ();
			t.Join ();

			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			GC.Collect ();
			GC.WaitForPendingFinalizers ();

			surfaced  = Runtime.GetSurfacedObjects ();
			Assert.AreEqual (startCount, surfaced.Count, "#3");
			Assert.IsTrue (surfaced.All (s => s.Target != null), "#4");
		}
	}

	[Register ("from/NewThreadOne")]
	class RegisterMeOnNewThreadOne : Java.Lang.Object
	{}

	[Register ("from/NewThreadTwo")]
	class RegisterMeOnNewThreadTwo : Java.Lang.Object
	{}

	class MyRegistrationThread : Java.Lang.Thread
	{
		public RegisterMeOnNewThreadTwo Instance { get; private set; }

		public override void Run ()
		{
			Instance = new RegisterMeOnNewThreadTwo ();
		}
	}

	class MyCb : Java.Lang.Object, Java.Lang.IRunnable {
		public void Run ()
		{
			Console.WriteLine ("MyCb.Run! JNIEnv.Handle={0}", JNIEnv.Handle.ToString ("x"));
		}
	}

	class ContainsExportedMethods : Java.Lang.Object {

		public bool Constructed;

		public int Count;

		public ContainsExportedMethods ()
		{
			Console.WriteLine ("# ContainsExportedMethods: constructed! Handle=0x{0}", Handle.ToString ("x"));
			Constructed = true;
		}

		[Export]
		public void Exported ()
		{
			Count++;
		}
	}

	class ThrowableActivatedFromJava : Java.Lang.Throwable {

		public  bool  Constructed;

		public ThrowableActivatedFromJava ()
		{
			Constructed = true;
		}
	}

	class GenericHolder<T> : Java.Lang.Object {

		public T Value {get; set;}

	}

	#region BXC_374
	class MyPaint : Paint {

		public Color SetColor;

		public override Color Color {
			get {
				Console.WriteLine ("get_Color");
				return new Color (a:0x11, r:0x22, g:0x33, b:0x44);
			}
			set {
				Console.WriteLine ("set_Color({0})", value.ToArgb ());
				SetColor = value;
				base.Color = value;
			}
		}
	}
	#endregion
}
