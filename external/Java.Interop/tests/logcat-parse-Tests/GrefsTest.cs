using System;
using System.IO;
using System.Linq;
using System.Reflection;

using Xamarin.Android.Tools.LogcatParse;

using NUnit.Framework;

namespace Xamarin.Android.Tools.LogcatParse.Tests {

	[TestFixture]
	public class GrefsTest {

		[Test]
		public void Instances_GrefToWgrefToCollected ()
		{
			using (var source = GetResourceStream ("logcat-gwd.txt")) {
				var info = Grefs.Parse (source, options: GrefParseOptions.ThrowOnCountMismatch);
				Assert.AreEqual (1, info.AllocatedPeers.Count);
				Assert.AreEqual (0, info.AlivePeers.Count ());
				Assert.AreEqual (0, info.GrefCount);
				Assert.AreEqual (0, info.WeakGrefCount);

				var peer = info.AllocatedPeers.First ();
				Assert.IsTrue (peer.Collected);
				Assert.AreEqual ("android/widget/ProgressBar", peer.JniType);
				Assert.AreEqual ("Android.Widget.ProgressBar", peer.McwType);
				Assert.AreEqual ("0x41f008f8", peer.KeyHandle);
				Assert.AreEqual (0, peer.Handles.Count);
				Assert.AreEqual (3, peer.RemovedHandles.Count);
				Assert.IsTrue (peer.RemovedHandles [0] == "0x41f008f8/L");
				Assert.IsTrue (peer.RemovedHandles [1] == "0x1d20046a/G");
				Assert.IsTrue (peer.RemovedHandles [2] == "0x1d200003/W");

				Assert.AreEqual (
						"   at Android.Runtime.JNIEnv.NewGlobalRef(IntPtr jobject)\n" +
						"   at Java.Lang.Object.RegisterInstance(IJavaObject instance, IntPtr value, JniHandleOwnership transfer)\n" +
						"   at Java.Lang.Object.SetHandle(IntPtr value, JniHandleOwnership transfer)\n" +
						"   at Android.Widget.ProgressBar..ctor(Android.Content.Context context, IAttributeSet attrs, Int32 defStyle)\n" +
						"   at Android.Support.V4.App.Fragment.n_OnCreateView_Landroid_view_LayoutInflater_Landroid_view_ViewGroup_Landroid_os_Bundle_(IntPtr jnienv, IntPtr native__this, ",
						peer.GetStackTraceForHandle ("0x1d20046a"));
				Assert.AreEqual (
						"take_weak_global_ref_jni",
						peer.GetStackTraceForHandle ("0x1d200003"));
			}
		}

		[Test]
		public void Instances_GrefToDisposed ()
		{
			using (var source = GetResourceStream ("logcat-disposed.txt")) {
				var info = Grefs.Parse (source, options: GrefParseOptions.ThrowOnCountMismatch);
				Assert.AreEqual (1, info.AllocatedPeers.Count);
				Assert.AreEqual (0, info.AlivePeers.Count ());
				Assert.AreEqual (0, info.GrefCount);
				Assert.AreEqual (0, info.WeakGrefCount);

				var peer = info.AllocatedPeers.First ();
				Assert.IsTrue (peer.Collected);
				Assert.IsTrue (peer.Disposed);
				Assert.AreEqual ("java/lang/String", peer.JniType);
				Assert.AreEqual ("Java.Lang.String", peer.McwType);
				Assert.AreEqual ("0x41e29778", peer.KeyHandle);
				Assert.AreEqual (0, peer.Handles.Count);
				Assert.AreEqual (2, peer.RemovedHandles.Count);
				Assert.IsTrue (peer.RemovedHandles [0] == "0x41e29778/L");
				Assert.IsTrue (peer.RemovedHandles [1] == "0x1d200282/G");
				Assert.AreEqual (
						"   at Android.Runtime.JNIEnv.NewGlobalRef(IntPtr jobject)\n" +
						"   at Java.Lang.Object.RegisterInstance(IJavaObject instance, IntPtr value, JniHandleOwnership transfer)\n" +
						"   at Java.Lang.Object.SetHandle(IntPtr value, JniHandleOwnership transfer)\n" +
						"   at Java.Lang.String..ctor(System.String string)\n" +
						"   at Android.App.ProgressDialog.Show(Android.Content.Context context, System.String title, System.String message, Boolean indeterminate, Boolean cancelable)\n" +
						"   at Java.Lang.Thread+RunnableImplementor.Run()\n" +
						"   at Java.Lang.IRunnableInvoker.n_Run(IntPtr jnienv, IntPtr native__this)\n" +
						"   at System.Object.015d773b-4c56-4e22-ab21-e971886ef628(IntPtr , IntPtr )\n" +
						"   at System.Object.wrapper_native_0x408f48fd(IntPtr , IntPtr , IntPtr , Android.Runtime.JValue[] )\n" +
						"   at Android.Runtime.JNIEnv.CallVoidMethod(IntPtr jobject, IntPtr jmethod, Android.Runtime.JValue[] parms)\n" +
						"   at Android.App",
						peer.GetStackTraceForHandle ("0x1d200282"));
			}
		}

		[Test]
		public void Instances_Resurrection ()
		{
			using (var source = GetResourceStream ("logcat-resurrection.txt")) {
				var info = Grefs.Parse (source, options: GrefParseOptions.ThrowOnCountMismatch);
				Assert.AreEqual (1, info.AllocatedPeers.Count);
				Assert.AreEqual (1, info.AlivePeers.Count ());
				Assert.AreEqual (1, info.GrefCount);
				Assert.AreEqual (0, info.WeakGrefCount);

				var peer = info.AllocatedPeers.First ();
				Assert.IsFalse (peer.Collected);
				Assert.IsFalse (peer.Disposed);
				Assert.AreEqual ("android/widget/LinearLayout", peer.JniType);
				Assert.AreEqual ("Android.Widget.LinearLayout", peer.McwType);
				Assert.AreEqual ("0x41ff8758", peer.KeyHandle);
				Assert.AreEqual (1, peer.Handles.Count);
				Assert.IsTrue (peer.Handles [0] == "0x1d300eea");
				Assert.AreEqual (3, peer.RemovedHandles.Count);
				Assert.IsTrue (peer.RemovedHandles [0] == "0x41ff8758/L");
				Assert.IsTrue (peer.RemovedHandles [1] == "0x1d200f1e/G");
				Assert.IsTrue (peer.RemovedHandles [2] == "0x1d9000cb/W");
				Assert.AreEqual (
						"   at Android.Runtime.JNIEnv.NewGlobalRef(IntPtr jobject)\n" +
						"   at Java.Lang.Object.RegisterInstance(IJavaObject instance, IntPtr value, JniHandleOwnership transfer)\n" +
						"   at Java.Lang.Object.SetHandle(IntPtr value, JniHandleOwnership transfer)\n" +
						"   at Android.Widget.LinearLayout..ctor(Android.Content.Context context)\n" +
						"   at Android.Support.V4.App.Fragment.n_OnCreateView_Landroid_view_LayoutInflater_Landroid_view_ViewGroup_Landroid_os_Bundle_(IntPtr jnienv, IntPtr native__this, IntPtr native",
						peer.GetStackTraceForHandle ("0x1d200f1e"));
				Assert.AreEqual (
						"take_weak_global_ref_jni",
						peer.GetStackTraceForHandle ("0x1d9000cb"));
				Assert.AreEqual (
						"take_global_ref_jni",
						peer.GetStackTraceForHandle ("0x1d300eea"));
			}
		}

		void Instances_CreateAndDestroy (string resource)
		{
			using (var source = GetResourceStream (resource)) {
				var info = Grefs.Parse (source, options: GrefParseOptions.ThrowOnCountMismatch);
				Assert.AreEqual (1, info.AllocatedPeers.Count);
				Assert.AreEqual (0, info.AlivePeers.Count ());
				Assert.AreEqual (0, info.GrefCount);
				Assert.AreEqual (0, info.WeakGrefCount);

				var peer = info.AllocatedPeers.First ();
				Assert.IsTrue (peer.Collected);
				Assert.IsFalse (peer.Disposed);
				Assert.AreEqual ("Java.Lang.Thread+RunnableImplementor.class",      peer.JniType);
				Assert.AreEqual ("typeof(Java.Lang.Thread+RunnableImplementor)",    peer.McwType);
				Assert.AreEqual (0, peer.Handles.Count);
				Assert.AreEqual (2, peer.RemovedHandles.Count);
				Assert.IsTrue (peer.RemovedHandles [0] == "0x41e29370/L");
				Assert.IsTrue (peer.RemovedHandles [1] == "0x1d200276/G");
				Assert.AreEqual (
					"   at Android.Runtime.JNIEnv.NewGlobalRef(IntPtr jobject)\n" +
					"   at Android.Runtime.JNIEnv.FindClass(System.String classname)\n" +
					"   at Android.Runtime.JNIEnv.CreateInstance(System.String jniClassName, System.String signature, Android.Runtime.JValue[] constructorParameters)\n" +
					"   at Java.Lang.Thread+RunnableImplementor..ctor(System.Action handler, Boolean removable)\n" +
					"   at Java.Lang.Thread+RunnableImplementor..ctor(System.Action handler)\n" +
					"   at Android.App.Activity.RunOnUiThread(System.Action action)\n" +
					"   at Android.App.Activity.n_OnCreate_Landroid_os_Bundle_(IntPtr jnienv, IntPtr native__this, IntPtr native_savedInstanceState)\n" +
					"   at System.Object.71bca0f9-d145-4b62-90c1-4e9ad52c866a(IntPtr , IntPtr , IntPtr ",
					peer.GetStackTraceForHandle ("0x1d200276"));
			}
		}

		[Test]
		public void Instances_CreateAndDestroy ()
		{
			Instances_CreateAndDestroy ("logcat-ag-rg.txt");
		}

		[Test]
		public void Instances_CreateAndDestroy_Stdout ()
		{
			Instances_CreateAndDestroy ("stdout-ag-rg.txt");
		}

		[Test]
		public void Instances_CreateAndDestroy_Timestamp ()
		{
			Instances_CreateAndDestroy ("timestamp-ag-rg.txt");
		}

		[Test]
		public void Instances_CreateAndDestroy_Stdio ()
		{
			Instances_CreateAndDestroy ("stdio-ag-rg.txt");
		}

		[Test]
		public void Instances_Alias ()
		{
			using (var source = GetResourceStream ("logcat-alias.txt")) {
				var info = Grefs.Parse (source, options: GrefParseOptions.ThrowOnCountMismatch);
				Assert.AreEqual (1, info.AllocatedPeers.Count);
				Assert.AreEqual (1, info.AlivePeers.Count ());
				Assert.AreEqual (2, info.GrefCount);
				Assert.AreEqual (0, info.WeakGrefCount);

				var peer = info.AllocatedPeers.First ();
				Assert.IsFalse (peer.Collected);
				Assert.IsFalse (peer.Disposed);
				Assert.AreEqual ("android/widget/NewButton", peer.JniType);
				Assert.AreEqual ("Android.Widget.NewButton", peer.McwType);
				Assert.AreEqual (2, peer.Handles.Count);
				Assert.IsTrue (peer.Handles.Contains ("0x100456"));
				Assert.IsTrue (peer.Handles.Contains ("0x100472"));
				Assert.AreEqual (1, peer.RemovedHandles.Count);
				Assert.IsTrue (peer.RemovedHandles [0] == "0xbecdf114/L");
				Assert.AreEqual (
					"   at Android.Runtime.JNIEnv.NewGlobalRef(IntPtr jobject)\n" +
					"   at Java.Lang.Object.RegisterInstance(IJavaObject instance, IntPtr value, JniHandleOwnership transfer, IntPtr ByRef handle)\n" +
					"   at Java.Lang.Object.SetHandle(IntPtr value, JniHandleOwnership transfer)\n" +
					"   at Java.Lang.Object..ctor(IntPtr handle, JniHandleOwnership transfer)\n" +
					"   at Android.Views.View..ctor(IntPtr javaReference, JniHandleOwnership transfer)\n" +
					"   at Android.Widget.TextView..ctor(IntPtr javaReference, JniHandleOwnership transfer)\n" +
					"   at Android.Widget.Button..ctor(IntPtr javaReference, JniHandleOwnership transfer)\n" +
					"   at Android.Widget.NewButton..ctor(IntPtr jr, JniHandleOwnership tr)\n" +
					"   at System.Reflection.MonoCMethod.InternalInvoke(System.Reflection.MonoCMethod , System.Object , System.Object[] , System.Exception ByRef )\n" +
					"   at System.Reflection.MonoCMethod.InternalInvoke(System.Object obj, System.Object[] parameters)\n" +
					"   at System.Reflection.MonoCMethod.DoInvoke(Syste",
					peer.GetStackTraceForHandle ("0x100456"));
			}
		}

		[Test]
		public void GetClassRef ()
		{
			using (var source = GetResourceStream ("logcat-get_class_ref.txt")) {
				var info = Grefs.Parse (source, options: GrefParseOptions.ThrowOnCountMismatch);
				Assert.AreEqual (1, info.AllocatedPeers.Count);
				Assert.AreEqual (1, info.AlivePeers.Count ());
				Assert.AreEqual (1, info.GrefCount);
				Assert.AreEqual (0, info.WeakGrefCount);

				var peer = info.AllocatedPeers.First ();
				Assert.IsFalse (peer.Collected);
				Assert.IsFalse (peer.Disposed);
				Assert.AreEqual ("Android.Widget.Button.class",     peer.JniType);
				Assert.AreEqual ("typeof(Android.Widget.Button)",   peer.McwType);
				Assert.AreEqual (1, peer.Handles.Count);
				Assert.IsTrue (peer.Handles [0] == "0x10046a");
				Assert.AreEqual (1, peer.RemovedHandles.Count);
				Assert.IsTrue (peer.RemovedHandles [0] == "0x7830001d/L");

				Assert.AreEqual (
					"   at Android.Runtime.JNIEnv.NewGlobalRef(IntPtr jobject)\n" +
					"   at Android.Runtime.JNIEnv.FindClass(System.String classname)\n" +
					"   at Android.Runtime.JNIEnv.FindClass(System.String className, IntPtr ByRef cachedJniClassHandle)\n" +
					"   at Android.Widget.Button.get_class_ref()\n" +
					"   at Android.Widget.Button.get_ThresholdClass()\n" +
					"   at Android.Views.View.SetLayerType(LayerType layerType, Android.Graphics.Paint paint)",
					peer.GetStackTraceForHandle ("0x10046a"));
			}
		}

		[Test]
		public void InvokerJavaClassRef ()
		{
			using (var source = GetResourceStream ("logcat-Invoker-java_class_ref.txt")) {
				var info = Grefs.Parse (source, options: GrefParseOptions.ThrowOnCountMismatch);
				Assert.AreEqual (1, info.AllocatedPeers.Count);
				Assert.AreEqual (1, info.AlivePeers.Count ());
				Assert.AreEqual (1, info.GrefCount);
				Assert.AreEqual (0, info.WeakGrefCount);

				var peer = info.AllocatedPeers.First ();
				Assert.IsFalse (peer.Collected);
				Assert.IsFalse (peer.Disposed);
				Assert.AreEqual ("Android.Views.View+IOnClickListenerInvoker.class",    peer.JniType);
				Assert.AreEqual ("typeof(Android.Views.View+IOnClickListenerInvoker)",  peer.McwType);
				Assert.AreEqual (1, peer.Handles.Count);
				Assert.IsTrue (peer.Handles [0] == "0x100476");
				Assert.AreEqual (1, peer.RemovedHandles.Count);
				Assert.IsTrue (peer.RemovedHandles [0] == "0x78b0001d/L");


				Assert.AreEqual (
					"   at Android.Runtime.JNIEnv.NewGlobalRef(IntPtr jobject)\n" +
					"   at Android.Runtime.JNIEnv.FindClass(System.String classname)\n" +
					"   at Android.Views.View+IOnClickListenerInvoker..cctor()\n" +
					"   at Android.Runtime.JNIEnv.RegisterJniNatives(IntPtr typeName_ptr, Int32 typeName_len, IntPtr jniClass, IntPtr methods_ptr, Int32 methods_len)\n" +
					"   at System.Object.wrapper_native_0xb4dd21a1(IntPtr , IntPtr )\n" +
					"   at Android.Runtime.JNIEnv.AllocObject(IntPtr jclass)\n" +
					"   at Android.Runtime.JNIEnv.AllocObject(System.String jniClassName)\n" +
					"   at Android.Runtime.JNIEnv.StartCreateInstance(System.String jniClassName, System.String jniCtorSignature, Android.Runtime.JValue[] constructorParameters)\n" +
					"   at Android.Views.View+IOnClickListenerImplementor..ctor()\n" +
					"   at Android.Views.View.__CreateIOnClickListenerImplementor()\n" +
					"   at Java.Interop.EventHelper.AddEventHandler(System.WeakReference ByRef implementor, System.Func`1 creator, System.Action`1 setListener, System.Action`1 add)\n" +
					"   at Android.Views.View.add_Click(System.EventHandler value)\n" +
					"   at System.Object.1beb6483-abe9-4bc4-b75a-e21a3e26c5a1(IntPtr , IntPtr , IntPtr )",
					peer.GetStackTraceForHandle ("0x100476"));
			}
		}

		[Test]
		public void JavaListClassRef ()
		{
			using (var source = GetResourceStream ("stdio-JavaList.txt")) {
				var info = Grefs.Parse (source, options: GrefParseOptions.ThrowOnCountMismatch);
				Assert.AreEqual (1, info.AllocatedPeers.Count);
				Assert.AreEqual (1, info.AlivePeers.Count ());
				Assert.AreEqual (1, info.GrefCount);
				Assert.AreEqual (0, info.WeakGrefCount);

				var peer = info.AllocatedPeers.First ();
				Assert.IsFalse (peer.Collected);
				Assert.IsFalse (peer.Disposed);
				Assert.IsFalse (peer.Finalized);
				Assert.AreEqual ("Android.Runtime.JavaList.class",      peer.JniType);
				Assert.AreEqual ("typeof(Android.Runtime.JavaList)",    peer.McwType);
				Assert.IsTrue (peer.Handles.Contains ("0x19004aa"));
				Assert.AreEqual (1, peer.RemovedHandles.Count);
				Assert.IsTrue (peer.RemovedHandles [0] == "0x4a00009/L");


				Assert.AreEqual (
					"   at Android.Runtime.JNIEnv.NewGlobalRef(IntPtr jobject)\n" +
					"   at Android.Runtime.JNIEnv.FindClass(System.String classname)\n" +
					"   at Android.Runtime.JavaList..cctor()\n" +
					"   at Java.Interop.JavaConvert.FromJniHandle(IntPtr handle, JniHandleOwnership transfer, Boolean ByRef set)\n" +
					"   at Java.Interop.JavaConvert.FromJniHandle(IntPtr handle, JniHandleOwnership transfer)\n" +
					"   at Android.Runtime.JavaDictionary`2[[System.String, mscorlib, Version=2.0.5.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e],[System.Object, mscorlib, Version=2.0.5.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]].Get(System.String key)\n" +
					"   at Android.Runtime.JavaDictionary`2[[System.String, mscorlib, Version=2.0.5.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e],[System.Object, mscorlib, Version=2.0.5.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]].TryGetValue(System.String key, System.Object ByRef value)\n" +
					"   at Xamarin.Android.RuntimeTests.JavaConvertTest.Conversions()\n" +
					"   at System.Reflection.MonoMethod.InternalInvoke(System.Reflection.MonoMethod , System.Object , System.Object[] , System.Exception ByRef )\n" +
					"   at System.Reflection.MonoMethod.Invoke(System.Object obj, BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture)\n" +
					"   at System.Reflection.MethodBase.Invoke(System.Object obj, System.Object[] parameters)\n" +
					"   at NUnit.Framework.Internal.Reflect.InvokeMethod(System.Reflection.MethodInfo method, System.Object fixture, System.Object[] args)\n" +
					"   at NUnit.Framework.Internal.Commands.TestMethodCommand.RunNonAsyncTestMethod(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at NUnit.Framework.Internal.Commands.TestMethodCommand.RunTestMethod(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at NUnit.Framework.Internal.Commands.TestMethodCommand.Execute(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at NUnit.Framework.Internal.Commands.SetUpTearDownCommand.Execute(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at NUnit.Framework.Internal.WorkItems.SimpleWorkItem.PerformWork()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.RunTest()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.Execute(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at NUnit.Framework.Internal.WorkItems.CompositeWorkItem.RunChildren()\n" +
					"   at NUnit.Framework.Internal.WorkItems.CompositeWorkItem.PerformWork()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.RunTest()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.Execute(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at NUnit.Framework.Internal.WorkItems.CompositeWorkItem.RunChildren()\n" +
					"   at NUnit.Framework.Internal.WorkItems.CompositeWorkItem.PerformWork()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.RunTest()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.Execute(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at NUnit.Framework.Internal.WorkItems.CompositeWorkItem.RunChildren()\n" +
					"   at NUnit.Framework.Internal.WorkItems.CompositeWorkItem.PerformWork()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.RunTest()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.Execute(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at Xamarin.Android.NUnitLite.AndroidRunner.Run(NUnit.Framework.Internal.Test test)\n" +
					"   at Xamarin.Android.NUnitLite.AndroidRunner.Run(NUnit.Framework.Internal.Test test, Android.Content.Context context)\n" +
					"   at Xamarin.Android.NUnitLite.TestSuiteInstrumentation.OnStart()\n" +
					"   at Android.App.Instrumentation.n_OnStart(IntPtr jnienv, IntPtr native__this)\n" +
					"   at System.Object.b7ee1212-364f-405b-bebd-08a60608685f(IntPtr , IntPtr )",
					peer.GetStackTraceForHandle ("0x19004aa"));
			}
		}

		[Test]
		public void TrackThreadInformation ()
		{
			using (var source = GetResourceStream ("stdio-Finalized-threads.txt")) {
				var info = Grefs.Parse (source, options: GrefParseOptions.ThrowOnCountMismatch);
				Assert.AreEqual (1, info.AllocatedPeers.Count);
				Assert.AreEqual (0, info.AlivePeers.Count ());
				Assert.AreEqual (0, info.GrefCount);
				Assert.AreEqual (0, info.WeakGrefCount);

				var peer = info.AllocatedPeers.First ();
				Assert.IsTrue (peer.Collected);
				Assert.IsFalse (peer.Disposed);
				Assert.IsFalse (peer.Finalized);
				Assert.AreEqual ("java/lang/Boolean",   peer.JniType);
				Assert.AreEqual ("Java.Lang.Boolean",   peer.McwType);

				Assert.AreEqual ("'(null)'(3)",         peer.CreatedOnThread);
				Assert.AreEqual ("'finalizer'(20660)",  peer.DestroyedOnThread);

				Assert.AreEqual ("0x39bcfcb7",          peer.KeyHandle);
				Assert.AreEqual (0, peer.Handles.Count);
				Assert.AreEqual (3, peer.RemovedHandles.Count);
				Assert.IsTrue (peer.RemovedHandles [0] == "0x9500001/L");
				Assert.IsTrue (peer.RemovedHandles [1] == "0x100492/G");
				Assert.IsTrue (peer.RemovedHandles [2] == "0x700003/W");

				Assert.AreEqual (
					"   at Android.Runtime.JNIEnv.NewGlobalRef(IntPtr jobject)\n" +
					"   at Java.Lang.Object.RegisterInstance(IJavaObject instance, IntPtr value, JniHandleOwnership transfer, IntPtr ByRef handle)\n" +
					"   at Java.Lang.Object.SetHandle(IntPtr value, JniHandleOwnership transfer)\n" +
					"   at Java.Lang.Boolean..ctor(Boolean value)\n" +
					"   at Xamarin.Android.RuntimeTests.JavaConvertTest.Conversions()\n" +
					"   at System.Reflection.MonoMethod.InternalInvoke(System.Reflection.MonoMethod , System.Object , System.Object[] , System.Exception ByRef )\n" +
					"   at System.Reflection.MonoMethod.Invoke(System.Object obj, BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture)\n" +
					"   at System.Reflection.MethodBase.Invoke(System.Object obj, System.Object[] parameters)\n" +
					"   at NUnit.Framework.Internal.Reflect.InvokeMethod(System.Reflection.MethodInfo method, System.Object fixture, System.Object[] args)\n" +
					"   at NUnit.Framework.Internal.Commands.TestMethodCommand.RunNonAsyncTestMethod(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at NUnit.Framework.Internal.Commands.TestMethodCommand.RunTestMethod(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at NUnit.Framework.Internal.Commands.TestMethodCommand.Execute(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at NUnit.Framework.Internal.Commands.SetUpTearDownCommand.Execute(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at NUnit.Framework.Internal.WorkItems.SimpleWorkItem.PerformWork()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.RunTest()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.Execute(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at NUnit.Framework.Internal.WorkItems.CompositeWorkItem.RunChildren()\n" +
					"   at NUnit.Framework.Internal.WorkItems.CompositeWorkItem.PerformWork()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.RunTest()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.Execute(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at NUnit.Framework.Internal.WorkItems.CompositeWorkItem.RunChildren()\n" +
					"   at NUnit.Framework.Internal.WorkItems.CompositeWorkItem.PerformWork()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.RunTest()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.Execute(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at NUnit.Framework.Internal.WorkItems.CompositeWorkItem.RunChildren()\n" +
					"   at NUnit.Framework.Internal.WorkItems.CompositeWorkItem.PerformWork()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.RunTest()\n" +
					"   at NUnit.Framework.Internal.WorkItems.WorkItem.Execute(NUnit.Framework.Internal.TestExecutionContext context)\n" +
					"   at Xamarin.Android.NUnitLite.AndroidRunner.Run(NUnit.Framework.Internal.Test test)\n" +
					"   at Xamarin.Android.NUnitLite.AndroidRunner.Run(NUnit.Framework.Internal.Test test, Android.Content.Context context)\n" +
					"   at Xamarin.Android.NUnitLite.TestSuiteInstrumentation.OnStart()\n" +
					"   at Android.App.Instrumentation.n_OnStart(IntPtr jnienv, IntPtr native__this)\n" +
					"   at System.Object.b7ee1212-364f-405b-bebd-08a60608685f(IntPtr , IntPtr )",
					peer.GetStackTraceForHandle ("0x100492/G"));
			}
		}

		[Test]
		public void RepeatedThreadHandles ()
		{
			using (var source = GetResourceStream ("stdio-repeated-handles.txt")) {
				var info = Grefs.Parse (source, options: GrefParseOptions.ThrowOnCountMismatch);
				Assert.AreEqual (2, info.AllocatedPeers.Count);
				Assert.AreEqual (1, info.AlivePeers.Count ());
				Assert.AreEqual (1, info.GrefCount);
				Assert.AreEqual (0, info.WeakGrefCount);

				var peer = info.AllocatedPeers [0];
				Assert.IsTrue (peer.Collected);
				Assert.IsTrue (peer.Disposed);
				Assert.IsFalse (peer.Finalized);
				Assert.AreEqual ("java/lang/String", peer.JniType);
				Assert.AreEqual ("Java.Lang.String", peer.McwType);

				Assert.AreEqual ("'(null)'(3)", peer.CreatedOnThread);
				Assert.AreEqual ("'(null)'(3)", peer.DestroyedOnThread);

				Assert.AreEqual ("0x41e29778", peer.KeyHandle);
				Assert.AreEqual (0, peer.Handles.Count);
				Assert.AreEqual (2, peer.RemovedHandles.Count);
				Assert.IsTrue (peer.RemovedHandles [0] == "0x4a00009/L");
				Assert.IsTrue (peer.RemovedHandles [1] == "0x19004aa/G");

				Assert.AreEqual (
					"  at Doesn't Matter",
					peer.GetStackTraceForHandle ("0x19004aa/G"));

				peer = info.AllocatedPeers [1];
				Assert.IsFalse (peer.Collected);
				Assert.IsFalse (peer.Disposed);
				Assert.IsFalse (peer.Finalized);
				Assert.AreEqual ("java/lang/String", peer.JniType);
				Assert.AreEqual ("Java.Lang.String", peer.McwType);

				Assert.AreEqual ("'(null)'(3)", peer.CreatedOnThread);
				Assert.AreEqual (null,          peer.DestroyedOnThread);

				Assert.AreEqual ("0x41e29778", peer.KeyHandle);
				Assert.AreEqual (1, peer.Handles.Count);
				Assert.IsTrue (peer.Handles [0] == "0x19004aa/G");
				Assert.AreEqual (1, peer.RemovedHandles.Count);
				Assert.IsTrue (peer.RemovedHandles [0] == "0x4a00009/L");

				Assert.AreEqual (
					"  at Doesn't Matter",
					peer.GetStackTraceForHandle ("0x19004aa/G"));
			}
		}

		StreamReader GetResourceStream (string resource)
		{
			// Look for resources that end with our name, this allows us to
			// avoid the LogicalName stuff
			var assembly = Assembly.GetExecutingAssembly ();
			var name = assembly.GetManifestResourceNames ().FirstOrDefault (n => n.EndsWith ("." + resource, StringComparison.OrdinalIgnoreCase)) ?? resource;

			return new StreamReader (assembly.GetManifestResourceStream (name));
		}
	}
}

