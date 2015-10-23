using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public sealed class JniInstanceFieldInfo : JniFieldInfo
	{
		internal JniInstanceFieldInfo (IntPtr fieldID)
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

		public sbyte GetByteValue (JniObjectReference @this)
		{
			return JniEnvironment.InstanceFields.GetByteField (@this, this);
		}

		public char GetCharacterValue (JniObjectReference @this)
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
			JniEnvironment.InstanceFields.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, bool value)
		{
			JniEnvironment.InstanceFields.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, sbyte value)
		{
			JniEnvironment.InstanceFields.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, char value)
		{
			JniEnvironment.InstanceFields.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, short value)
		{
			JniEnvironment.InstanceFields.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, int value)
		{
			JniEnvironment.InstanceFields.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, long value)
		{
			JniEnvironment.InstanceFields.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, float value)
		{
			JniEnvironment.InstanceFields.SetField (@this, this, value);
		}

		public void SetValue (JniObjectReference @this, double value)
		{
			JniEnvironment.InstanceFields.SetField (@this, this, value);
		}
	}
}

