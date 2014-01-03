using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public class JniInstanceMethodID : SafeHandle
	{
		JniInstanceMethodID ()
			: base (IntPtr.Zero, ownsHandle:false)
		{
		}

		protected override bool ReleaseHandle ()
		{
			return true;
		}

		public override bool IsInvalid {
			get {
				return handle == IntPtr.Zero;
			}
		}

		public int InvokeIntMethod (JniReferenceSafeHandle instance, params JValue[] @params)
		{
			return JniMembers.CallIntMethod (instance, this, @params);
		}

		public JniLocalReference InvokeObjectMethod (JniReferenceSafeHandle instance, params JValue[] @params)
		{
			return JniMembers.CallObjectMethod (instance, this, @params);
		}

		public void InvokeVoidMethod (JniReferenceSafeHandle instance, params JValue[] @params)
		{
			JniMembers.CallVoidMethod (instance, this, @params);
		}
	}

}

