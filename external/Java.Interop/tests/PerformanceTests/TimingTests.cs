using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Java.Interop;

using NUnit.Framework;

namespace Java.Interop.PerformanceTests {

	[TestFixture]
	class TimingTests : Java.InteropTests.JavaVMFixture {

		const string LibName = "NativeTiming";

		[DllImport (LibName)]
		static extern void foo_void_timing ();

		[DllImport (LibName)]
		static extern int foo_int_timing ();

		[DllImport (LibName)]
		static extern IntPtr foo_ptr_timing ();

		[DllImport (LibName)]
		static extern void foo_init (JniEnvironmentSafeHandle env);

		[DllImport (LibName)]
		static extern void foo_get_native_jni_timings (JniEnvironmentSafeHandle env, int count, JniReferenceSafeHandle klass, JniReferenceSafeHandle self, long[] jniTimes);

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
		public void MethodTiming ()
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

			var transfer    = JniHandleOwnership.Transfer;

			var jobj1 = new JavaObject (Object_class.NewObject (Object_init), transfer);
			var jobj2 = new JavaObject (Object_class.NewObject (Object_init), transfer);
			var jobj3 = new JavaObject (Object_class.NewObject (Object_init), transfer);

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
			const int count = 100;
#else   // __ANDROID__
			const int count = 100000;
#endif  // __ANDROID__

			foo_init (JniEnvironment.Current.SafeHandle);

			var jniTimes = new long [comparisons.Length];
			foo_get_native_jni_timings (JniEnvironment.Current.SafeHandle, count, JavaTiming.TypeRef.SafeHandle, j.SafeHandle, jniTimes);

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
				Console.WriteLine ("\t  C/JNI: {0}", ct);
				Console.WriteLine ("\t    JNI: {0}; {1}x C/JNI", jw.Elapsed,
					System.Math.Round (jw.Elapsed.TotalMilliseconds / ct.TotalMilliseconds));
				Console.WriteLine ("\tManaged: {0}", mw.Elapsed);
				if (pw != null)
					Console.WriteLine ("\tPinvoke: {0}; {1}x managed", pw.Elapsed,
						System.Math.Round (pw.Elapsed.TotalMilliseconds / mw.Elapsed.TotalMilliseconds));
			}
		}

		static Action A (Action a)
		{
			return a;
		}

		[Test]
		public void MethodLookupTiming ()
		{
#if __ANDROID__
			const int count = 100;
#else   // __ANDROID__
			const int count = 1000;
#endif  // __ANDROID__
			using (var o = new JavaTiming ()) {
				var tt = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i)
					o.Timing_ToString_Traditional ().Dispose ();
				tt.Stop ();

				var ta = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i)
					o.Timing_ToString_NoCache ().Dispose ();
				ta.Stop ();

				var td = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i)
					o.Timing_ToString_DictWithLock ().Dispose ();
				td.Stop ();

				var tc = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i)
					o.Timing_ToString_DictWithNoLock ().Dispose ();
				tc.Stop ();

				Console.WriteLine ("Method Lookup + Invoke Timing:");
				Console.WriteLine ("\t   Traditional: {0}", tt.Elapsed);
				Console.WriteLine ("\t    No caching: {0}", ta.Elapsed);
				Console.WriteLine ("\t  Dict w/ lock: {0}", td.Elapsed);
				Console.WriteLine ("\tConcurrentDict: {0}", tc.Elapsed);
			}
		}
		[Test]
		public void IndexOfTiming ()
		{
#if __ANDROID__
			const int C = 100;
#else   // __ANDROID__
			const int C = 1000;
#endif  // __ANDROID__
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
		}

		static unsafe int _IndexOf (JavaInt32Array array, int item)
		{
			using (var e = array.GetElements ()) {
				int len = array.Length;
				for (int i = 0; i < len; ++i)
					if (e.Elements [i] == item)
						return i;
			}
			return -1;
		}

		[Test]
		public void DelegateVsVirtualMethodInvocationTiming ()
		{
			const int C = 100000;

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
		public void GenericMarshalingOverhead_Int32 ()
		{
			const int C = 10000;

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
		}

		[Test]
		public void GenericMarshalingOverhead_Int32ArrayArrayArray ()
		{
#if __ANDROID__
			const int C = 100;
#else   // __ANDROID__
			const int C = 1000;
#endif  // __ANDROID__

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

