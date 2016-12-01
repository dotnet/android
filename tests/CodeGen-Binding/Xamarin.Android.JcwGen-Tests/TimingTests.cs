using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using NUnit.Framework;

using Android.Runtime;

namespace Xamarin.Android.JcwGenTests {

	[TestFixture]
	public class TimingTests {

		[DllImport ("timing")]
		static extern void foo_void_timing ();

		[DllImport ("timing")]
		static extern int foo_int_timing ();

		[DllImport ("timing")]
		static extern IntPtr foo_ptr_timing ();

		[DllImport ("timing")]
		static extern void foo_init (IntPtr env);

		[DllImport ("timing")]
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

		[DllImport ("timing")]
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

			DV p_instance_void  = (DV) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.instance_void, typeof (DV));
			DI p_instance_int   = (DI) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.instance_int, typeof (DI));
			DP p_instance_ptr   = (DP) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.instance_ptr, typeof (DP));

			DV1A p_void_1a = (DV1A) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.void_1_args, typeof (DV1A));
			DV2A p_void_2a = (DV2A) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.void_2_args, typeof (DV2A));
			DV3A p_void_3a = (DV3A) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.void_3_args, typeof (DV3A));

			DV1AI p_void_1ai = (DV1AI) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.void_1_iargs, typeof (DV1AI));
			DV2AI p_void_2ai = (DV2AI) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.void_2_iargs, typeof (DV2AI));
			DV3AI p_void_3ai = (DV3AI) Marshal.GetDelegateForFunctionPointer (pinvoke_methods.void_3_iargs, typeof (DV3AI));

			IntPtr obj_class = JNIEnv.FindClass ("java/lang/Object");
			IntPtr obj_init = JNIEnv.GetMethodID (obj_class, "<init>", "()V");

			var ownership = JniHandleOwnership.DoNotTransfer;

			Java.Lang.Object jobj1 = new Java.Lang.Object (JNIEnv.NewObject (obj_class, obj_init), ownership),
					 jobj2 = new Java.Lang.Object (JNIEnv.NewObject (obj_class, obj_init), ownership),
					 jobj3 = new Java.Lang.Object (JNIEnv.NewObject (obj_class, obj_init), ownership);

                        SomeClass obj1 = new SomeClass (),
				  obj2 = new SomeClass (),
				  obj3 = new SomeClass ();

			var j = new Com.Xamarin.Android.Timing ();
			var m = new ManagedTiming ();
			var comparisons = new[]{
				new {
					Name    = "static void",
					Jni     = A (() => Com.Xamarin.Android.Timing.StaticVoidMethod ()),
					Managed = A (() => ManagedTiming.StaticVoidMethod ()),
					Pinvoke = A (() => foo_void_timing ()),
				},
				new {
					Name    = "static int",
					Jni     = A (() => Com.Xamarin.Android.Timing.StaticIntMethod ()),
					Managed = A (() => ManagedTiming.StaticIntMethod ()),
					Pinvoke = A (() => foo_int_timing ()),
				},
				new {
					Name    = "static object",
					Jni     = A (() => Com.Xamarin.Android.Timing.StaticObjectMethod ()),
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
					Jni     = A (() => Com.Xamarin.Android.Timing.StaticVoidMethod1Args (jobj1)),
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
					Jni     = A (() => Com.Xamarin.Android.Timing.StaticVoidMethod2Args (jobj1, jobj2)),
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
					Jni     = A (() => Com.Xamarin.Android.Timing.StaticVoidMethod3Args (jobj1, jobj2, jobj3)),
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
					Jni     = A (() => Com.Xamarin.Android.Timing.StaticVoidMethod1IArgs (42)),
					Managed = A (() => ManagedTiming.StaticVoidMethod1IArgs (42)),
					Pinvoke = A (() => p_void_1ai (42)),
				},
				new {
					Name    = "static void i2",
					Jni     = A (() => Com.Xamarin.Android.Timing.StaticVoidMethod2IArgs (42, 42)),
					Managed = A (() => ManagedTiming.StaticVoidMethod2IArgs (42, 42)),
					Pinvoke = A (() => p_void_2ai (42, 42)),
				},
				new {
					Name    = "static void i3",
					Jni     = A (() => Com.Xamarin.Android.Timing.StaticVoidMethod3IArgs (42, 42, 42)),
					Managed = A (() => ManagedTiming.StaticVoidMethod3IArgs (42, 42, 42)),
					Pinvoke = A (() => p_void_3ai (42, 42, 42)),
				},
			};

			const int count = 100000;

			foo_init (JNIEnv.Handle);

			var jniTimes = new long [comparisons.Length];
			foo_get_native_jni_timings (JNIEnv.Handle, count, j.Class.Handle, j.Handle, jniTimes);

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
						FormatFraction (ct.TotalMilliseconds, 10, 5),
						FormatFraction (ct.TotalMilliseconds / count, 12, 5));
				Console.WriteLine ("\t    JNI: {0} ms; {1,3}x C/JNI   | average: {2} ms",
						FormatFraction (jw.Elapsed.TotalMilliseconds, 10, 5),
						ToString (jw.Elapsed, ct),
						FormatFraction (jw.Elapsed.TotalMilliseconds / count, 12, 5));
				Console.WriteLine ("\tManaged: {0} ms               | average: {1} ms",
						FormatFraction (mw.Elapsed.TotalMilliseconds, 10, 5),
						FormatFraction (mw.Elapsed.TotalMilliseconds / count, 12, 5));
				if (pw != null)
					Console.WriteLine ("\tPinvoke: {0} ms; {1,3}x managed | average: {2} ms",
							FormatFraction (pw.Elapsed.TotalMilliseconds, 10, 5),
							ToString (pw.Elapsed, mw.Elapsed),
							FormatFraction (pw.Elapsed.TotalMilliseconds / count, 12, 5));
			}
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

	[TestFixture]
	class JniMethodLookupOverheadTiming {

		[Test]
		public void MethodLookupTiming ()
		{
			const int count = 100000;

			var total   = Stopwatch.StartNew ();

			using (var o = new Com.Xamarin.Android.Timing ()) {
				var tt = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.VirtualVoidMethod_Timing_Traditional ();
				}
				tt.Stop ();

				var tx = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.VirtualVoidMethod_Timing_TraditionalWithCaching ();
				}
				tx.Stop ();

				var ta = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.VirtualVoidMethod_Timing_NoCache ();
				}
				ta.Stop ();

				var td = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.VirtualVoidMethod_Timing_DictWithLock ();;
				}
				td.Stop ();

				var tc = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.VirtualVoidMethod_Timing_ConcurrentDict ();
				}
				tc.Stop ();

				var tp = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.VirtualVoidMethod_Timing_JniPeerMembers ();
				}
				tp.Stop ();


				var vtt = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.VirtualVoidMethod_Timing_Traditional ();
				}
				vtt.Stop ();

				var vtx = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.VirtualVoidMethod_Timing_TraditionalWithCaching ();
				}
				vtx.Stop ();

				var vti = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.VirtualVoidMethod_Timing_JniPeerMembers ();
				}
				vti.Stop ();


				Console.WriteLine ("Method Lookup + Invoke Timing:");
				Console.WriteLine ("\t   Traditional: {0}", tt.Elapsed);
				Console.WriteLine ("\t Traditional-N: {0}", tx.Elapsed);
				Console.WriteLine ("\t    No caching: {0}", ta.Elapsed);
				Console.WriteLine ("\t  Dict w/ lock: {0}", td.Elapsed);
				Console.WriteLine ("\tConcurrentDict: {0}", tc.Elapsed);
				Console.WriteLine ("\tJniPeerMembers: {0}", tp.Elapsed);
				Console.WriteLine ();
				Console.WriteLine ("\t       ()V virtual+traditional: {0}", vtt.Elapsed);
				Console.WriteLine ("\t   ()V virtual+traditional+nvc: {0}", vtx.Elapsed);
				Console.WriteLine ("\t    ()V virtual+JniPeerMembers: {0}", vti.Elapsed);
			}
			using (var o = new DerivedTiming ()) {
				var ntt = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.VirtualVoidMethod_Timing_Traditional ();
				}
				ntt.Stop ();

				var ntx = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.VirtualVoidMethod_Timing_TraditionalWithCaching ();
				}
				ntx.Stop ();

				var nti = Stopwatch.StartNew ();
				for (int i = 0; i < count; ++i) {
					o.VirtualVoidMethod_Timing_JniPeerMembers ();
				}
				nti.Stop ();
				Console.WriteLine ("\t    ()V nonvirtual+traditional: {0}", ntt.Elapsed);
				Console.WriteLine ("\t()V nonvirtual+traditional+nvc: {0}", ntx.Elapsed);
				Console.WriteLine ("\t ()V nonvirtual+JniPeerMembers: {0}", nti.Elapsed);
			}

			total.Stop ();
			Console.WriteLine ("## {0} Timing: {1}", "MethodLookupTiming", total.Elapsed);
		}
	}

	class DerivedTiming : Com.Xamarin.Android.Timing {
	}
}

