using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public sealed class JniInstanceFieldInfo : JniFieldInfo
	{
		public JniInstanceFieldInfo (IntPtr fieldID)
			: base (fieldID)
		{
		}

		public JniObjectReference GetObjectValue (JniObjectReference @this)
		{
			return JniEnvironment.InstanceFields.GetObjectField (@this, this);
		}

		public bool GetBooleanValue (JniObjectReference @this)
		{
			return JniEnvironment.InstanceFields.GetBooleanField (@this, this);
		}

		public sbyte GetSByteValue (JniObjectReference @this)
		{
			return JniEnvironment.InstanceFields.GetByteField (@this, this);
		}

		public char GetCharValue (JniObjectReference @this)
		{
			return JniEnvironment.InstanceFields.GetCharField (@this, this);
		}

		public short GetInt16Value (JniObjectReference @this)
		{
			return JniEnvironment.InstanceFields.GetShortField (@this, this);
		}

		public int GetInt32Value (JniObjectReference @this)
		{
			return JniEnvironment.InstanceFields.GetIntField (@this, this);
		}

		public long GetInt64Value (JniObjectReference @this)
		{
			return JniEnvironment.InstanceFields.GetLongField (@this, this);
		}

		public float GetSingleValue (JniObjectReference @this)
		{
			return JniEnvironment.InstanceFields.GetFloatField (@this, this);
		}

		public double GetDoubleValue (JniObjectReference @this)
		{
			return JniEnvironment.InstanceFields.GetDoubleField (@this, this);
		}

		public void SetValue (JniObjectReference @this, JniObjectReference value)
		{
			JniEnvironment.InstanceFields.SetObjectField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, bool value)
		{
			JniEnvironment.InstanceFields.SetBooleanField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, sbyte value)
		{
			JniEnvironment.InstanceFields.SetByteField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, char value)
		{
			JniEnvironment.InstanceFields.SetCharField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, short value)
		{
			JniEnvironment.InstanceFields.SetShortField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, int value)
		{
			JniEnvironment.InstanceFields.SetIntField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, long value)
		{
			JniEnvironment.InstanceFields.SetLongField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, float value)
		{
			JniEnvironment.InstanceFields.SetFloatField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, double value)
		{
			JniEnvironment.InstanceFields.SetDoubleField (@this, this, value);
		}
	}
}

