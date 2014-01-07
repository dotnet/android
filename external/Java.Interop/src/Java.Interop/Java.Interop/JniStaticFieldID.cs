using System;
using System.Runtime.InteropServices;

namespace Java.Interop {

	public sealed class JniStaticFieldID : SafeHandle
	{
		JniStaticFieldID ()
			: base (IntPtr.Zero, ownsHandle:false)
		{
			JniEnvironment.Current.JavaVM.Track (this, this);
		}

		protected override bool ReleaseHandle ()
		{
			JniEnvironment.Current.JavaVM.UnTrack (this);
			return true;
		}

		public override bool IsInvalid {
			get {
				return handle == IntPtr.Zero;
			}
		}

		public JniLocalReference GetObjectValue (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetStaticObjectField (@class, this);
		}

		public bool GetBooleanValue (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetStaticBooleanField (@class, this);
		}

		public sbyte GetByteValue (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetStaticByteField (@class, this);
		}

		public char GetCharValue (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetStaticCharField (@class, this);
		}

		public short GetInt16Value (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetStaticShortField (@class, this);
		}

		public int GetInt32Value (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetStaticIntField (@class, this);
		}

		public long GetInt64Value (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetStaticLongField (@class, this);
		}

		public float GetSingleValue (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetStaticFloatField (@class, this);
		}

		public double GetDoubleValue (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetStaticDoubleField (@class, this);
		}

		public void SetValue (JniReferenceSafeHandle @class, JniReferenceSafeHandle value)
		{
			JniMembers.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, bool value)
		{
			JniMembers.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, sbyte value)
		{
			JniMembers.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, char value)
		{
			JniMembers.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, short value)
		{
			JniMembers.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, int value)
		{
			JniMembers.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, long value)
		{
			JniMembers.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, float value)
		{
			JniMembers.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, double value)
		{
			JniMembers.SetStaticField (@class, this, value);
		}
	}
}

