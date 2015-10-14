using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public sealed class JniInstanceFieldID : JniFieldID
	{
		internal JniInstanceFieldID (IntPtr fieldID)
			: base (fieldID)
		{
		}

		public JniObjectReference GetObjectValue (JniObjectReference @this)
		{
			return JniEnvironment.Members.GetObjectField (@this, this);
		}

		public bool GetBooleanValue (JniObjectReference @this)
		{
			return JniEnvironment.Members.GetBooleanField (@this, this);
		}

		public sbyte GetByteValue (JniObjectReference @this)
		{
			return JniEnvironment.Members.GetByteField (@this, this);
		}

		public char GetCharValue (JniObjectReference @this)
		{
			return JniEnvironment.Members.GetCharField (@this, this);
		}

		public short GetInt16Value (JniObjectReference @this)
		{
			return JniEnvironment.Members.GetShortField (@this, this);
		}

		public int GetInt32Value (JniObjectReference @this)
		{
			return JniEnvironment.Members.GetIntField (@this, this);
		}

		public long GetInt64Value (JniObjectReference @this)
		{
			return JniEnvironment.Members.GetLongField (@this, this);
		}

		public float GetSingleValue (JniObjectReference @this)
		{
			return JniEnvironment.Members.GetFloatField (@this, this);
		}

		public double GetDoubleValue (JniObjectReference @this)
		{
			return JniEnvironment.Members.GetDoubleField (@this, this);
		}

		public void SetValue (JniObjectReference @this, JniObjectReference value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, bool value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, sbyte value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, char value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, short value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, int value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, long value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, float value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, double value)
		{
			JniEnvironment.Members.SetField (@this, this, value);
		}
	}
}

