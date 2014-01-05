using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{

	public class JniStaticMethodID : SafeHandle
	{
		private JniStaticMethodID ()
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

		public JniLocalReference CallObjectMethod (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallStaticObjectMethod (@this, this);
		}

		public JniLocalReference CallObjectMethod (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallStaticObjectMethod (@this, this, parameters);
		}

		public bool CallBooleanMethod (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallStaticBooleanMethod (@this, this);
		}

		public bool CallBooleanMethod (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallStaticBooleanMethod (@this, this, parameters);
		}

		public sbyte CallByteMethod (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallStaticByteMethod (@this, this);
		}

		public sbyte CallByteMethod (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallStaticByteMethod (@this, this, parameters);
		}

		public char CallCharMethod (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallStaticCharMethod (@this, this);
		}

		public char CallCharMethod (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallStaticCharMethod (@this, this, parameters);
		}

		public short CallInt16Method (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallStaticShortMethod (@this, this);
		}

		public short CallInt16Method (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallStaticShortMethod (@this, this, parameters);
		}

		public int CallInt32Method (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallStaticIntMethod (@this, this);
		}

		public int CallInt32Method (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallStaticIntMethod (@this, this, parameters);
		}

		public long CallInt64Method (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallStaticLongMethod (@this, this);
		}

		public long CallInt64Method (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallStaticLongMethod (@this, this, parameters);
		}

		public float CallSingleMethod (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallStaticFloatMethod (@this, this);
		}

		public float CallSingleMethod (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallStaticFloatMethod (@this, this, parameters);
		}

		public double CallDoubleMethod (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallStaticDoubleMethod (@this, this);
		}

		public double CallDoubleMethod (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallStaticDoubleMethod (@this, this, parameters);
		}
	}
}
