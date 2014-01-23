using System;
using System.Runtime.InteropServices;

namespace Java.Interop {

	public sealed class JniStaticFieldID : JniFieldID
	{
		JniStaticFieldID ()
		{
		}

		public JniLocalReference GetObjectValue (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetStaticObjectField (@class, this);
		}

		public bool GetBooleanValue (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetStaticBooleanField (@class, this);
		}

		public sbyte GetByteValue (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetStaticByteField (@class, this);
		}

		public char GetCharValue (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetStaticCharField (@class, this);
		}

		public short GetInt16Value (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetStaticShortField (@class, this);
		}

		public int GetInt32Value (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetStaticIntField (@class, this);
		}

		public long GetInt64Value (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetStaticLongField (@class, this);
		}

		public float GetSingleValue (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetStaticFloatField (@class, this);
		}

		public double GetDoubleValue (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetStaticDoubleField (@class, this);
		}

		public void SetValue (JniReferenceSafeHandle @class, JniReferenceSafeHandle value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, bool value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, sbyte value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, char value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, short value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, int value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, long value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, float value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, double value)
		{
			JniEnvironment.Members.SetStaticField (@class, this, value);
		}
	}
}

