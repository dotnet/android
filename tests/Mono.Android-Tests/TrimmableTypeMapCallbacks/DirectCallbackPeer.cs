using System;
using System.Runtime.InteropServices;

using Android.Runtime;

using Java.Interop;

[assembly: JavaPeerCallbackFormat (JavaPeerCallbackFormatAttribute.UnmanagedCallersOnlyCallbacks)]

namespace TrimmableTypeMapCallbacks
{
	[Register ("net/dot/android/test/TrimmableDirectCallbackBase", DoNotGenerateAcw = true)]
	public class DirectCallbackBase : Java.Lang.Object
	{
		public static int LastIntValue;
		public static long LastLongValue;

		public DirectCallbackBase ()
		{
		}

		public DirectCallbackBase (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Register ("remove", "(I)V", "n_Remove")]
		public virtual void Remove (int value)
		{
		}

		[UnmanagedCallersOnly]
		static void n_Remove (IntPtr jnienv, IntPtr nativeThis, int value)
		{
			LastIntValue = value;
		}

		[Register ("remove", "(J)V", "n_Remove_1")]
		public virtual void Remove (long value)
		{
		}

		[UnmanagedCallersOnly]
		static void n_Remove_1 (IntPtr jnienv, IntPtr nativeThis, long value)
		{
			LastLongValue = value;
		}

		[Register ("qualified", "(I)I", "n_Qualified:TrimmableTypeMapCallbacks.QualifiedCallbackHost, TrimmableTypeMapCallbacks")]
		public virtual int Qualified (int value) => -1;
	}

	[Register ("net/dot/android/test/TrimmableDirectCallbackPeer")]
	public class DirectCallbackPeer : DirectCallbackBase
	{
		public DirectCallbackPeer ()
		{
		}

		public override void Remove (int value)
		{
		}

		public override void Remove (long value)
		{
		}

		public override int Qualified (int value) => -2;
	}

	public static class QualifiedCallbackHost
	{
		public static int Invocations;

		[UnmanagedCallersOnly]
		internal static int n_Qualified (IntPtr jnienv, IntPtr nativeThis, int value)
		{
			Invocations++;
			return value + 100;
		}
	}
}
