using System;
using System.Runtime.InteropServices;

namespace Java.Interop {

	public sealed class JniStaticFieldID : JniFieldID
	{
		internal JniStaticFieldID (IntPtr fieldID)
			: base (fieldID)
		{
		}

		public JniObjectReference GetObjectValue (JniObjectReference @class)
		{
			return JniEnvironment.Members.GetStaticObjectField (@class, this);
		}

		public bool GetBooleanValue (JniObjectReference @class)
		{
			return JniEnvironment.Members.GetStaticBooleanField (@class, this);
		}

		public sbyte GetByteValue (JniObjectReference @class)
		{
			return JniEnvironment.Members.GetStaticByteField (@class, this);
		}

		public char GetCharValue (JniObjectReference @class)
		{
			return JniEnvironment.Members.GetStaticCharField (@class, this);
		}

		public short GetInt16Value (JniObjectReference @class)
		{
			return JniEnvironment.Members.GetStaticShortField (@class, this);
		}

		public int GetInt32Value (JniObjectReference @class)
		{
			return JniEnvironment.Members.GetStaticIntField (@class, this);
		}

		public long GetInt64Value (JniObjectReference @class)
		{
			return JniEnvironment.Members.GetStaticLongField (@class, this);
		}

		public float GetSingleValue (JniObjectReference @class)
		{
			return JniEnvironment.Members.GetStaticFloatField (@class, this);
		}

		public double GetDoubleValue (JniObjectReference @class)
		{
			return JniEnvironment.Members.GetStaticDoubleField (@class, this);
		}

		public void SetValue (JniObjectReference @class, JniObjectReference value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniObjectReference @class, bool value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniObjectReference @class, sbyte value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniObjectReference @class, char value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniObjectReference @class, short value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniObjectReference @class, int value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniObjectReference @class, long value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniObjectReference @class, float value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniObjectReference @class, double value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}
	}
}

