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
			return JniMembers.GetObjectField (@class, this);
		}

		public bool GetBooleanValue (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetBooleanField (@class, this);
		}

		public sbyte GetByteValue (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetByteField (@class, this);
		}

		public char GetCharValue (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetCharField (@class, this);
		}

		public short GetInt16Value (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetShortField (@class, this);
		}

		public int GetInt32Value (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetIntField (@class, this);
		}

		public long GetInt64Value (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetLongField (@class, this);
		}

		public float GetSingleValue (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetFloatField (@class, this);
		}

		public double GetDoubleValue (JniReferenceSafeHandle @class)
		{
			return JniMembers.GetDoubleField (@class, this);
		}

		public void SetValue (JniReferenceSafeHandle @class, JniReferenceSafeHandle value)
		{
			JniMembers.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, bool value)
		{
			JniMembers.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, sbyte value)
		{
			JniMembers.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, char value)
		{
			JniMembers.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, short value)
		{
			JniMembers.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, int value)
		{
			JniMembers.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, long value)
		{
			JniMembers.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, float value)
		{
			JniMembers.SetField (@class, this, value);
		}

		public void SetValue (JniReferenceSafeHandle @class, double value)
		{
			JniMembers.SetField (@class, this, value);
		}
	}
}

