using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Java.Interop;

using NUnit.Framework;

namespace Java.Interop.PerformanceTests {

	[TestFixture]
	class JniMethodInvocationOverheadTiming : Java.InteropTests.JavaVMFixture {

		const string LibName = "NativeTiming";

		[DllImport (LibName)]
		static extern void foo_void_timing ();

		[DllImport (LibName)]
		static extern int foo_int_timing ();

		[DllImport (LibName)]
		static extern IntPtr foo_ptr_timing ();

		[DllImport (LibName)]
		static extern void foo_init (IntPtr env);

		[DllImport (LibName)]
		static extern void foo_get_native_jni_timings (IntPtr env, int count, IntPtr klass, IntPtr self, long[] jniTimes);

		struct FooMethods {
			public IntPtr instance_void;
			public IntPtr instance_int;
			public IntPtr instance_ptr;

			public IntPtr void_1_args;
			public IntPtr void_2_args;
			public IntPtr void_3_args;

			public IntPtr void_1_iargs;
			public IntPtr void_2_iargs;
			public IntPtr void_3_iargs;
		}

		[DllImport (LibName)]
		static extern void foo_get_methods (out FooMethods methods);


		delegate void   DV ();
		delegate int    DI ();
		delegate IntPtr DP ();

		delegate void DV1A (IntPtr obj1);
		delegate void DV2A (IntPtr obj1, IntPtr obj2);
		delegate void DV3A (IntPtr obj1, IntPtr obj2, IntPtr obj3);

		delegate void DV1AI (int i1);
		delegate void DV2AI (int i1, int i2);
		delegate void DV3AI (int i1, int i2, int i3);

		[Test]
		public unsafe void MethodInvocationTiming ()
		{
			FooMethods pinvoke_methods;
			foo_get_methods (out pinvoke_methods);

			var p_instance_void = (DV) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.instance_void, typeof (DV));
			var p_instance_int  = (DI) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.instance_int, typeof (DI));
			var p_instance_ptr  = (DP) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.instance_ptr, typeof (DP));

			var p_void_1a   = (DV1A) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.void_1_args, typeof (DV1A));
			var p_void_2a   = (DV2A) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.void_2_args, typeof (DV2A));
			var p_void_3a   = (DV3A) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.void_3_args, typeof (DV3A));

			var p_void_1ai  = (DV1AI) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.void_1_iargs, typeof (DV1AI));
			var p_void_2ai  = (DV2AI) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.void_2_iargs, typeof (DV2AI));
			var p_void_3ai  = (DV3AI) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.void_3_iargs, typeof (DV3AI));

			var Object_class    = new JniType ("java/lang/Object");
			var Object_init     = Object_class.GetConstructor ("()V");

			var transfer    = JniObjectReferenceOptions.CopyAndDispose;

			var jobj1 = CreateJavaObject (Object_class.NewObject (Object_init, null), transfer);
			var jobj2 = CreateJavaObject (Object_class.NewObject (Object_init, null), transfer);
			var jobj3 = CreateJavaObject (Object_class.NewObject (Object_init, null), transfer);

			var obj1 = new SomeClass ();
			var obj2 = new SomeClass ();
			var obj3 = new SomeClass ();

			var j = new JavaTiming ();
			var m = new ManagedTiming ();
			var comparisons = new[]{
				new {
					Name    = "static void",
					Jni     = A (() => JavaTiming.StaticVoidMethod ()),
					Managed = A (() => ManagedTiming.StaticVoidMethod ()),
					Pinvoke = A (() => foo_void_timing ()),
				},
				new {
					Name    = "static int",
					Jni     = A (() => JavaTiming.StaticIntMethod ()),
					Managed = A (() => ManagedTiming.StaticIntMethod ()),
					Pinvoke = A (() => foo_int_timing ()),
				},
				new {
					Name    = "static object",
					Jni     = A (() => JavaTiming.StaticObjectMethod ()),
					Managed = A (() => ManagedTiming.StaticObjectMethod ()),
					Pinvoke = A (() => foo_ptr_timing ()),
				},
				new {
					Name    = "virtual void",
					Jni     = A (() => j.VirtualVoidMethod ()),
					Managed = A (() => m.VirtualVoidMethod ()),
					Pinvoke = A (() => p_instance_void ()),
				},
				new {
					Name    = "virtual int",
					Jni     = A (() => j.VirtualIntMethod ()),
					Managed = A (() => m.VirtualIntMethod ()),
					Pinvoke = A (() => p_instance_int ()),
				},
				new {
					Name    = "virtual object",
					Jni     = A (() => j.VirtualObjectMethod ()),
					Managed = A (() => m.VirtualObjectMethod ()),
					Pinvoke = A (() => p_instance_ptr ()),
				},
				new {
					Name    = "final void",
					Jni     = A (() => j.FinalVoidMethod ()),
					Managed = A (() => m.FinalVoidMethod ()),
					Pinvoke = A (null),
				},
				new {
					Name    = "final int",
					Jni     = A (() => j.FinalIntMethod ()),
					Managed = A (() => m.FinalIntMethod ()),
					Pinvoke = A (null),
				},
				new {
					Name    = "final object",
					Jni     = A (() => j.FinalObjectMethod ()),
					Managed = A (() => m.FinalObjectMethod ()),
					Pinvoke = A (null),
				},
				new {
					Name    = "static void o1",
					Jni     = A (() => JavaTiming.StaticVoidMethod1Args (jobj1)),
					Managed = A (() => ManagedTiming.StaticVoidMethod1Args (obj1)),
					Pinvoke = A (() => {
						// We include timing of the GCHandle manipulation since
						// a JNI invocation has to do similar work, and pinning
						// is usually always needed for P/Invokes.
						GCHandle h1 = GCHandle.Alloc (obj1, GCHandleType.Pinned);
						IntPtr addr1 = h1.AddrOfPinnedObject ();

						p_void_1a (addr1);

						h1.Free ();
					}),
				},
				new {
					Name    = "static void o2",
					Jni     = A (() => JavaTiming.StaticVoidMethod2Args (jobj1, jobj2)),
					Managed = A (() => ManagedTiming.StaticVoidMethod2Args (obj1, obj2)),
					Pinvoke = A (() => {
						GCHandle h1 = GCHandle.Alloc (obj1, GCHandleType.Pinned),
						h2 = GCHandle.Alloc (obj2, GCHandleType.Pinned);
						IntPtr addr1 = h1.AddrOfPinnedObject (),
						addr2 = h2.AddrOfPinnedObject ();

						p_void_2a (addr1, addr2);

						h1.Free ();
						h2.Free ();
					}),
				},
				new {
					Name    = "static void o3",
					Jni     = A (() => JavaTiming.StaticVoidMethod3Args (jobj1, jobj2, jobj3)),
					Managed = A (() => ManagedTiming.StaticVoidMethod3Args (obj1, obj2, obj3)),
					Pinvoke = A (() => {
						GCHandle h1 = GCHandle.Alloc (obj1, GCHandleType.Pinned),
						h2 = GCHandle.Alloc (obj2, GCHandleType.Pinned),
						h3 = GCHandle.Alloc (obj3, GCHandleType.Pinned);
						IntPtr addr1 = h1.AddrOfPinnedObject (),
						addr2 = h2.AddrOfPinnedObject (),
						addr3 = h3.AddrOfPinnedObject ();

						p_void_3a (addr1, addr2, addr3);

						h1.Free ();
						h2.Free ();
						h3.Free ();
					}),
				},
				new {
					Name    = "static void i1",
					Jni     = A (() => JavaTiming.StaticVoidMethod1IArgs (42)),
					Managed = A (() => ManagedTiming.StaticVoidMethod1IArgs (42)),
					Pinvoke = A (() => p_void_1ai (42)),
				},
				new {
					Name    = "static void i2",
					Jni     = A (() => JavaTiming.StaticVoidMethod2IArgs (42, 42)),
					Managed = A (() => ManagedTiming.StaticVoidMethod2IArgs (42, 42)),
					Pinvoke = A (() => p_void_2ai (42, 42)),
				},
				new {
					Name    = "static void i3",
					Jni     = A (() => JavaTiming.StaticVoidMethod3IArgs (42, 42, 42)),
					Managed = A (() => ManagedTiming.StaticVoidMethod3IArgs (42, 42, 42)),
					Pinvoke = A (() => p_void_3ai (42, 42, 42)),
				},
			};

#if __ANDROID__
			const int count = 1000;
#else   // __ANDROID__
			const int count = 100000;
#endif  // __ANDROID__

			var total   = Stopwatch.StartNew ();

			foo_init (JniEnvironment.EnvironmentPointer);

			var jniTimes = new long [comparisons.Length];
			foo_get_native_jni_timings (JniEnvironment.EnvironmentPointer, count, JavaTiming.TypeRef.PeerReference.Handle, j.PeerReference.Handle, jniTimes);

			int jniTimeIndex = 0;
			foreach (var c in comparisons) {
				var jw = System.Diagnostics.Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i)
					c.Jni ();
				jw.Stop ();

				var mw = System.Diagnostics.Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i)
					c.Managed ();
				mw.Stop ();

				System.Diagnostics.Stopwatch pw = null;
				if (c.Pinvoke != null) {
					pw = System.Diagnostics.Stopwatch.StartNew ();
					for (int i = 0; i < count; ++i)
						c.Pinvoke ();
					pw.Stop ();
				}

				string message = string.Format ("Method Invoke: {0}: JNI is {1}x managed",
					c.Name, System.Math.Round (jw.Elapsed.TotalMilliseconds / mw.Elapsed.TotalMilliseconds));
				Console.WriteLine (message);

				var ct = TimeSpan.FromMilliseconds (jniTimes [jniTimeIndex++]);
				Console.WriteLine ("\t  C/JNI: {0} ms               | average: {1} ms",
						FormatFraction (ct.TotalMilliseconds, 12, 5),
						FormatFraction (ct.TotalMilliseconds / count, 12, 5));
				Console.WriteLine ("\t    JNI: {0} ms; {1,3}x C/JNI   | average: {2} ms",
						FormatFraction (jw.Elapsed.TotalMilliseconds, 12, 5),
						ToString (jw.Elapsed, ct),
						FormatFraction (jw.Elapsed.TotalMilliseconds / count, 12, 5));
				Console.WriteLine ("\tManaged: {0} ms               | average: {1} ms",
						FormatFraction (mw.Elapsed.TotalMilliseconds, 12, 5),
						FormatFraction (mw.Elapsed.TotalMilliseconds / count, 12, 5));
				if (pw != null)
					Console.WriteLine ("\tPinvoke: {0} ms; {1,3}x managed | average: {2} ms",
							FormatFraction (pw.Elapsed.TotalMilliseconds, 12, 5),
							ToString (pw.Elapsed, mw.Elapsed),
							FormatFraction (pw.Elapsed.TotalMilliseconds / count, 12, 5));
			}

			total.Stop ();
			Console.WriteLine ("## {0} Timing: {1}", nameof (MethodInvocationTiming), total.Elapsed);
		}

		static JavaObject CreateJavaObject (JniObjectReference value, JniObjectReferenceOptions transfer)
		{
			return new JavaObject (ref value, transfer);
		}

		static Action A (Action a)
		{
			return a;
		}

		static string FormatFraction (double value, int width, int fractionWidth)
		{
			var v = value.ToString ("0.0" + new string ('#', fractionWidth - 1));
			var i = v.IndexOf (NumberFormatInfo.CurrentInfo.NumberDecimalSeparator);
			var p = new string (' ', width - fractionWidth - i - 1);
			return p + v + new string (' ', width - p.Length - v.Length);
		}

		static string ToString (TimeSpan numerator, TimeSpan denominator)
		{
			if (System.Math.Abs (denominator.TotalMilliseconds) > double.Epsilon)
				return System.Math.Round (numerator.TotalMilliseconds / denominator.TotalMilliseconds).ToString ();
			return " ∞ ";
		}
	}


	[TestFixture]
	class JniMethodLookupOverheadTiming : Java.InteropTests.JavaVMFixture {

		[Test]
		public void MethodLookupTiming ()
		{
#if __ANDROID__
			const int count = 100;
#else   // __ANDROID__
			const int count = 100;
#endif  // __ANDROID__

			var total   = Stopwatch.StartNew ();

			using (var o = new JavaTiming ()) {
				var tt = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					var s = o.Timing_ToString_Traditional ();
					JniObjectReference.Dispose (ref s);
				}
				tt.Stop ();

				var ta = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					var s = o.Timing_ToString_NoCache ();
					JniObjectReference.Dispose (ref s);
				}
				ta.Stop ();

				var td = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					var s = o.Timing_ToString_DictWithLock ();;
					JniObjectReference.Dispose (ref s);
				}
				td.Stop ();

				var tc = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					var s = o.Timing_ToString_DictWithNoLock ();
					JniObjectReference.Dispose (ref s);
				}
				tc.Stop ();

				var tp = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					var s = o.Timing_ToString_JniPeerMembers ();
					JniObjectReference.Dispose (ref s);
				}
				tp.Stop ();


				var vtt = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.VirtualIntMethod1Args (i);
				}
				vtt.Stop ();

				var vti = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.Timing_VirtualIntMethod_Marshal1Args (i);
				}
				vti.Stop ();


				Console.WriteLine ("Method Lookup + Invoke Timing:");
				Console.WriteLine ("\t   Traditional: {0}", tt.Elapsed);
				Console.WriteLine ("\t    No caching: {0}", ta.Elapsed);
				Console.WriteLine ("\t  Dict w/ lock: {0}", td.Elapsed);
				Console.WriteLine ("\tConcurrentDict: {0}", tc.Elapsed);
				Console.WriteLine ("\tJniPeerMembers: {0}", tp.Elapsed);
				Console.WriteLine ();
				Console.WriteLine ("\t      (I)I virtual+traditional: {0}", vtt.Elapsed);
				Console.WriteLine ("\t   (I)I virtual+JniPeerMembers: {0}", vti.Elapsed);
			}
			using (var o = new DerivedJavaTiming ()) {
				var ntt = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.VirtualIntMethod1Args (i);
				}
				ntt.Stop ();

				var nti = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.Timing_VirtualIntMethod_Marshal1Args (i);
				}
				nti.Stop ();
				Console.WriteLine ("\t   (I)I nonvirtual+traditional: {0}", ntt.Elapsed);
				Console.WriteLine ("\t(I)I nonvirtual+JniPeerMembers: {0}", nti.Elapsed);
			}

			total.Stop ();
			Console.WriteLine ("## {0} Timing: {1}", nameof (MethodLookupTiming), total.Elapsed);
		}
	}

	[TestFixture]
	class JavaArrayTiming : Java.InteropTests.JavaVMFixture {

		[Test]
		public void IndexOfTiming ()
		{
#if __ANDROID__
			const int C = 100;
#else   // __ANDROID__
			const int C = 1000;
#endif  // __ANDROID__

			var total   = Stopwatch.StartNew ();

			using (var array = new JavaInt32Array (Enumerable.Range (0, 10000))) {
				var io = Stopwatch.StartNew ();
				for (int c = 0; c < C; ++c)
					array.IndexOf (10000);
				io.Stop ();
				var _io = Stopwatch.StartNew ();
				for (int c = 0; c < C; ++c)
					_IndexOf (array, 10000);
				_io.Stop ();
				Console.WriteLine ("JavaArray<T>.IndexOf Timing:");
				Console.WriteLine ("\t   JavaArray<T>.IndexOf: {0}", io.Elapsed);
				Console.WriteLine ("\tJavaInt32Array._IndexOf: {0}", _io.Elapsed);
			}

			total.Stop ();
			Console.WriteLine ("## {0} Timing: {1}", nameof (IndexOfTiming), total.Elapsed);
		}

		static unsafe int _IndexOf (JavaInt32Array array, int item)
		{
			using (var e = array.GetElements ()) {
				int len = array.Length;
				for (int i = 0; i < len; ++i)
					if (e [i] == item)
						return i;
			}
			return -1;
		}

		[Test]
		public void ObjectArrayEnumerationTiming ()
		{
			const int C = 100;

			var total   = Stopwatch.StartNew ();

			JniMethodInfo Class_getMethods;
			using (var t = new JniType ("java/lang/Class")) {
				Class_getMethods        = t.GetInstanceMethod ("getMethods", "()[Ljava/lang/reflect/Method;");
			}

			JniMethodInfo Method_getName;
			JniMethodInfo Method_getParameterTypes;
			JniMethodInfo Method_getReturnType;
			using (var t = new JniType ("java/lang/reflect/Method")) {
				Method_getName              = t.GetInstanceMethod ("getName", "()Ljava/lang/String;");
				Method_getParameterTypes    = t.GetInstanceMethod ("getParameterTypes", "()[Ljava/lang/Class;");
				Method_getReturnType        = t.GetInstanceMethod ("getReturnType", "()Ljava/lang/Class;");
			}
			Console.WriteLine ("# {0}: Method Lookups Timing: {1}", nameof (ObjectArrayEnumerationTiming), total.Elapsed);

			var methodHandles   = new List<JavaObject> ();

			using (var Arrays_class = new JniType ("java/util/Arrays")) {
				var lrefMethods = JniEnvironment.InstanceMethods.CallObjectMethod (Arrays_class.PeerReference, Class_getMethods);
				Console.WriteLine ("# {0}: java.util.Arrays.class.getMethods() Timing: {1}", nameof (ObjectArrayEnumerationTiming), total.Elapsed);

				var methodsTiming   = Stopwatch.StartNew ();
				using (var methods  = new JavaObjectArray<JavaObject> (ref lrefMethods, JniObjectReferenceOptions.Copy)) {
					foreach (var method in methods) {
						methodHandles.Add (method);
					}
				}
				methodsTiming.Stop ();
				Console.WriteLine ("# methodHandles(JavaObjectArray<JavaObject>) creation timing: {0} Count={1}", methodsTiming.Elapsed, methodHandles.Count);

				methodsTiming       = Stopwatch.StartNew ();
				var methodHandlesGO = new List<JavaObject> ();
				var vm              = JniEnvironment.Runtime;
				int len             = JniEnvironment.Arrays.GetArrayLength (lrefMethods);
				for (int i = 0; i < len; ++i) {
					var v = JniEnvironment.Arrays.GetObjectArrayElement (lrefMethods, i);
					methodHandlesGO.Add (vm.ValueManager.GetValue<JavaObject> (ref v, JniObjectReferenceOptions.CopyAndDoNotRegister));
					JniObjectReference.Dispose (ref v);
				}
				methodsTiming.Stop ();
				Console.WriteLine ("# methodHandles(JavaVM.GetObject) creation timing: {0} Count={1}", methodsTiming.Elapsed, methodHandles.Count);

				foreach (var h in methodHandlesGO)
					h.DisposeUnlessReferenced ();

				methodsTiming       = Stopwatch.StartNew ();
				var methodHandlesAr = new List<JavaObject> ();
				len                 = JniEnvironment.Arrays.GetArrayLength (lrefMethods);
				for (int i = 0; i < len; ++i) {
					var v = JniEnvironment.Arrays.GetObjectArrayElement (lrefMethods, i);
					methodHandlesAr.Add (new JavaObject (ref v, JniObjectReferenceOptions.CopyAndDoNotRegister));
					JniObjectReference.Dispose (ref v);
				}
				methodsTiming.Stop ();
				Console.WriteLine ("# methodHandles(JavaObject[]) creation timing: {0} Count={1}", methodsTiming.Elapsed, methodHandles.Count);

				foreach (var h in methodHandlesAr)
					h.Dispose ();


				methodsTiming       = Stopwatch.StartNew ();
				var methodHandlesGR = new List<JniObjectReference> ();
				len                 = JniEnvironment.Arrays.GetArrayLength (lrefMethods);
				for (int i = 0; i < len; ++i) {
					var v = JniEnvironment.Arrays.GetObjectArrayElement (lrefMethods, i);
					methodHandlesGR.Add (v.NewGlobalRef ());
					JniObjectReference.Dispose (ref v);
				}
				methodsTiming.Stop ();
				Console.WriteLine ("# methodHandles(JniGlobalReference) creation timing: {0} Count={1}", methodsTiming.Elapsed, methodHandles.Count);

				for (int i = 0; i < methodHandlesGR.Count; ++i) {
					var h = methodHandlesGR [i];
					JniObjectReference.Dispose (ref h);
					methodHandlesGR [i] = h;
				}

				JniObjectReference.Dispose (ref lrefMethods);
			}

			// HACK HACK HACK
			// This is to workaround an error wherein constructing `pt` (below)
			// throws an exception because `h` is NULL, when it really can't be.
			// I believe that this is due to the finalizer, which likewise makes
			// NO SENSE AT ALL, since `p` should be keeping the handle valid!
			// GC.Collect ();
			// GC.WaitForPendingFinalizers ();

			foreach (var method in methodHandles) {
				var lookupTiming    = Stopwatch.StartNew ();
				var n_name          = JniEnvironment.InstanceMethods.CallObjectMethod (method.PeerReference, Method_getName);
				var name            = JniEnvironment.Strings.ToString (ref n_name, JniObjectReferenceOptions.CopyAndDispose);
				var n_rt            = JniEnvironment.InstanceMethods.CallObjectMethod (method.PeerReference, Method_getReturnType);
				using (var rt       = new JniType (ref n_rt, JniObjectReferenceOptions.CopyAndDispose)) {
				}
				var parameterTiming = Stopwatch.StartNew ();
				var enumTime        = new TimeSpan ();
				var lrefPs          = JniEnvironment.InstanceMethods.CallObjectMethod (method.PeerReference, Method_getParameterTypes);
				int len = JniEnvironment.Arrays.GetArrayLength (lrefPs);
				var enumSw          = Stopwatch.StartNew ();
				for (int i = 0; i < len; ++i) {
					var p = JniEnvironment.Arrays.GetObjectArrayElement (lrefPs, i);
					using (var pt = new JniType (ref p, JniObjectReferenceOptions.Copy)) {
					}
					JniObjectReference.Dispose (ref p);
				}
				JniObjectReference.Dispose (ref lrefPs);
				enumSw.Stop ();
				enumTime    = enumSw.Elapsed;
				parameterTiming.Stop ();

				Console.WriteLine ("## method '{0}' timing: Total={1}; Parameters={2} Parameters.Dispose={3}",
						name,
						lookupTiming.Elapsed,
						enumTime,
						parameterTiming.Elapsed);
			}

			var mhDisposeTiming = Stopwatch.StartNew ();
			foreach (var method in methodHandles)
				method.Dispose ();
			mhDisposeTiming.Stop ();
			Console.WriteLine ("# methodHandles -> Dispose() Timing: {0}", mhDisposeTiming.Elapsed);

			total.Stop ();
			Console.WriteLine ("## {0} Timing: {1}", nameof (ObjectArrayEnumerationTiming), total.Elapsed);
		}
	}

	[TestFixture]
	class MiscRuntimeTiming : Java.InteropTests.JavaVMFixture {

		[Test]
		public void DelegateVsVirtualMethodInvocationTiming ()
		{
			const int C = 1000;

			var total   = Stopwatch.StartNew ();

			var d = GetDelegateTimingInfo ();
			var dt = Stopwatch.StartNew ();
			for (int i = 0; i < C; ++i) {
				if (d.GetValue != null)
					d.GetValue ();
			}
			dt.Stop ();

			var m = GetVirtualMethodTimingInfo ();
			var ct = Stopwatch.StartNew ();
			for (int i = 0; i < C; ++i) {
				if (m.CanGetValue)
					m.GetValue ();
			}
			ct.Stop ();

			Console.WriteLine ("Delegate vs. Method Invocation Timing:");
			Console.WriteLine ("\t      Delegate Timing: {0}", dt.Elapsed);
			Console.WriteLine ("\tVirtual Method Timing: {0}", ct.Elapsed);

			total.Stop ();
			Console.WriteLine ("## {0} Timing: {1}", nameof (DelegateVsVirtualMethodInvocationTiming), total.Elapsed);
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static DelegateInvocationTiming GetDelegateTimingInfo ()
		{
			var d = new DelegateInvocationTiming ();
			d.GetValue = () => null;
			return d;
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static VirtualMethodInvocationTiming GetVirtualMethodTimingInfo ()
		{
			return new VirtualMethodInvocationImpl ();
		}

		[Test]
		public unsafe void ObjectCreationTiming ()
		{
			const int C = 100;

			var total       = Stopwatch.StartNew ();

			Stopwatch   allocTime, newObjectTime, newTime, getObjectTime;

			using (var Object_class = new JniType ("java/lang/Object")) {
				var Object_init = Object_class.GetConstructor ("()V");
				allocTime   = Stopwatch.StartNew ();
				for (int i = 0; i < C; ++i) {
					var h = Object_class.AllocObject ();
					JniObjectReference.Dispose (ref h);
				}
				allocTime.Stop ();

				newObjectTime   = Stopwatch.StartNew ();
				for (int i = 0; i < C; ++i) {
					var h = Object_class.NewObject (Object_init, null);
					JniObjectReference.Dispose (ref h);
				}
				newObjectTime.Stop ();

				newTime     = Stopwatch.StartNew ();
				var olist   = new List<JavaObject> (C);
				for (int i = 0; i < C; ++i) {
					olist.Add (new JavaObject ());
				}
				newTime.Stop ();
				foreach (var o in olist)
					o.Dispose ();

				var strings = new JavaObjectArray<string> (100);
				for (int i = 0; i < 100; ++i) {
					strings [i] = i.ToString ();
				}

				using (strings) {
					var vm          = JniEnvironment.Runtime;
					var rlist       = new List<JavaObject> (C);
					getObjectTime   = Stopwatch.StartNew ();
					for (int i  = 0; i < C; ++i) {
						var h   = JniEnvironment.Arrays.GetObjectArrayElement (strings.PeerReference, i);
						var o   = vm.ValueManager.GetValue<JavaObject> (ref h, JniObjectReferenceOptions.CopyAndDispose);
						rlist.Add (o);
					}
					getObjectTime.Stop ();
					foreach (var o in rlist)
						o.DisposeUnlessReferenced ();
				}
			}

			total.Stop ();
			Console.WriteLine ("## {0} Timing: Total={1} AllocObject={2} NewObject={3} `new JavaObject()`={4} JavaVM.GetObject()={5}",
					nameof (ObjectCreationTiming), total.Elapsed, allocTime.Elapsed, newObjectTime.Elapsed, newTime.Elapsed, getObjectTime.Elapsed);
		}
	}

	[TestFixture]
	class GenericMarshalOverheadTiming : Java.InteropTests.JavaVMFixture {

		[Test]
		public void GenericMarshalingOverhead_Int32 ()
		{
			const int C = 10000;

			var total   = Stopwatch.StartNew ();

			using (var t = new JavaTiming ()) {
				var n = Stopwatch.StartNew ();
				for (int i = 0; i < C; ++i) {
					t.VirtualIntMethod1Args (i);
				}
				n.Stop ();

				var m = Stopwatch.StartNew ();
				for (int i = 0; i < C; ++i) {
					t.Timing_VirtualIntMethod_Marshal1Args (i);
				}
				m.Stop ();

				var g = Stopwatch.StartNew ();
				for (int i = 0; i < C; ++i) {
					t.Timing_VirtualIntMethod_GenericMarshal1Args (i);
				}
				g.Stop ();

				Console.WriteLine ("Generic Marshaling Overhead: (I)I");
				Console.WriteLine ("\t Native Marshaling: {0}", n.Elapsed);
				Console.WriteLine ("\tPartial Marshaling: {0}", m.Elapsed);
				Console.WriteLine ("\tGeneric Marshaling: {0}", g.Elapsed);
			}

			total.Stop ();
			Console.WriteLine ("## {0} Timing: {1}", nameof (GenericMarshalingOverhead_Int32), total.Elapsed);
		}

		[Test]
		public void GenericMarshalingOverhead_Int32ArrayArrayArray ()
		{
#if __ANDROID__
			const int C = 100;
#else   // __ANDROID__
			const int C = 100;
#endif  // __ANDROID__

			var total   = Stopwatch.StartNew ();

			var value = new int[][][] {
				new int[][] {
					new int[]{111, 112, 113},
					new int[]{121, 122, 123},
				},
				new int[][] {
					new int[]{211, 212, 213},
					new int[]{221, 222, 223},
				},
			};

			using (var t = new JavaTiming ()) {
				var n = Stopwatch.StartNew ();
				for (int i = 0; i < C; ++i) {
					t.VirtualIntMethod1Args (value);
				}
				n.Stop ();

				var m = Stopwatch.StartNew ();
				for (int i = 0; i < C; ++i) {
					t.Timing_VirtualIntMethod_Marshal1Args (value);
				}
				m.Stop ();

				var g = Stopwatch.StartNew ();
				for (int i = 0; i < C; ++i) {
					t.Timing_VirtualIntMethod_GenericMarshal1Args (value);
				}
				g.Stop ();

				Console.WriteLine ("Generic Marshaling Overhead: ([[[I)I");
				Console.WriteLine ("\t Native Marshaling: {0}", n.Elapsed);
				Console.WriteLine ("\tPartial Marshaling: {0}", m.Elapsed);
				Console.WriteLine ("\tGeneric Marshaling: {0}", g.Elapsed);
			}

			total.Stop ();
			Console.WriteLine ("## {0} Timing: {1}", nameof (GenericMarshalingOverhead_Int32ArrayArrayArray), total.Elapsed);
		}
	}

	class ManagedTiming {

		[MethodImpl (MethodImplOptions.NoInlining)]
		public static void StaticVoidMethod ()
		{
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		public static int StaticIntMethod ()
		{
			return 0;
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		public static Object StaticObjectMethod ()
		{
			return null;
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		public virtual void VirtualVoidMethod ()
		{
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		public virtual int VirtualIntMethod ()
		{
			return 0;
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		public virtual object VirtualObjectMethod ()
		{
			return null;
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		public void FinalVoidMethod ()
		{
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		public int FinalIntMethod ()
		{
			return 0;
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		public Object FinalObjectMethod ()
		{
			return null;
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		public static void StaticVoidMethod1Args (object obj1)
		{
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		public static void StaticVoidMethod2Args (object obj1, object obj2)
		{
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		public static void StaticVoidMethod3Args (object obj1, object obj2, object obj3)
		{
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		public static void StaticVoidMethod1IArgs(int i1)
		{
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		public static void StaticVoidMethod2IArgs(int i1, int i2)
		{
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		public static void StaticVoidMethod3IArgs(int i1, int i2, int i3)
		{
		}
	}

	[StructLayout (LayoutKind.Sequential)]
	class SomeClass {
	}

	struct DelegateInvocationTiming {
		public Func<object> GetValue;
	}

	class VirtualMethodInvocationTiming {
		public virtual bool CanGetValue {
			get { return false; }
		}

		public virtual object GetValue ()
		{
			throw new NotImplementedException ();
		}
	}

	class VirtualMethodInvocationImpl : VirtualMethodInvocationTiming {
		public override bool CanGetValue {
			get {
				return true;
			}
		}

		public override object GetValue ()
		{
			return null;
		}
	}
}

