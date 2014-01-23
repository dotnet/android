using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public sealed class JniInstanceFieldID : JniFieldID
	{
		JniInstanceFieldID ()
		{
		}

		public JniLocalReference GetObjectValue (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetObjectField (@class, this);
		}

		public bool GetBooleanValue (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetBooleanField (@class, this);
		}

		public sbyte GetByteValue (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetByteField (@class, this);
		}

		public char GetCharValue (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetCharField (@class, this);
		}

		public short GetInt16Value (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetShortField (@class, this);
		}

		public int GetInt32Value (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetIntField (@class, this);
		}

		public long GetInt64Value (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetLongField (@class, this);
		}

		public float GetSingleValue (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetFloatField (@class, this);
		}

		public double GetDoubleValue (JniReferenceSafeHandle @class)
		{
			return JniEnvironment.Members.GetDoubleField (@class, this);
		}

		public void SetValue (JniReferenceSafeHandle @class, JniReferenceSafeHandle value)
		{
			JniEnvironment.Members.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, bool value)
		{
			JniEnvironment.Members.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, sbyte value)
		{
			JniEnvironment.Members.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, char value)
		{
			JniEnvironment.Members.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, short value)
		{
			JniEnvironment.Members.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, int value)
		{
			JniEnvironment.Members.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, long value)
		{
			JniEnvironment.Members.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, float value)
		{
			JniEnvironment.Members.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, double value)
		{
			JniEnvironment.Members.SetField (@class, this, value);
		}
	}
}

