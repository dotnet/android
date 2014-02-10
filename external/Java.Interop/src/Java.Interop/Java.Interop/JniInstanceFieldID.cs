using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public sealed class JniInstanceFieldID : JniFieldID
	{
		JniInstanceFieldID ()
		{
		}

		public JniLocalReference GetObjectValue (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.GetObjectField (@this, this);
		}

		public bool GetBooleanValue (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.GetBooleanField (@this, this);
		}

		public sbyte GetByteValue (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.GetByteField (@this, this);
		}

		public char GetCharValue (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.GetCharField (@this, this);
		}

		public short GetInt16Value (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.GetShortField (@this, this);
		}

		public int GetInt32Value (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.GetIntField (@this, this);
		}

		public long GetInt64Value (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.GetLongField (@this, this);
		}

		public float GetSingleValue (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.GetFloatField (@this, this);
		}

		public double GetDoubleValue (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.GetDoubleField (@this, this);
		}

		public void SetValue (JniReferenceSafeHandle @this, JniReferenceSafeHandle value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @this, bool value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @this, sbyte value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @this, char value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @this, short value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @this, int value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @this, long value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @this, float value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @this, double value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}
	}
}

