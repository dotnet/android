using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public sealed class JniInstanceMethodID : JniMethodID
	{
		JniInstanceMethodID ()
		{
		}

		public JniLocalReference CallVirtualObjectMethod (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallObjectMethod (@this, this);
		}

		public JniLocalReference CallVirtualObjectMethod (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallObjectMethod (@this, this, parameters);
		}

		public bool CallVirtualBooleanMethod (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallBooleanMethod (@this, this);
		}

		public bool CallVirtualBooleanMethod (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallBooleanMethod (@this, this, parameters);
		}

		public sbyte CallVirtualByteMethod (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallByteMethod (@this, this);
		}

		public sbyte CallVirtualByteMethod (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallByteMethod (@this, this, parameters);
		}

		public char CallVirtualCharMethod (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallCharMethod (@this, this);
		}

		public char CallVirtualCharMethod (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallCharMethod (@this, this, parameters);
		}

		public short CallVirtualInt16Method (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallShortMethod (@this, this);
		}

		public short CallVirtualInt16Method (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallShortMethod (@this, this, parameters);
		}

		public int CallVirtualInt32Method (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallIntMethod (@this, this);
		}

		public int CallVirtualInt32Method (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallIntMethod (@this, this, parameters);
		}

		public long CallVirtualInt64Method (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallLongMethod (@this, this);
		}

		public long CallVirtualInt64Method (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallLongMethod (@this, this, parameters);
		}

		public float CallVirtualSingleMethod (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallFloatMethod (@this, this);
		}

		public float CallVirtualSingleMethod (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallFloatMethod (@this, this, parameters);
		}

		public double CallVirtualDoubleMethod (JniReferenceSafeHandle @this)
		{
			return JniMembers.CallDoubleMethod (@this, this);
		}

		public double CallVirtualDoubleMethod (JniReferenceSafeHandle @this, params JValue[] parameters)
		{
			return JniMembers.CallDoubleMethod (@this, this, parameters);
		}

		public JniLocalReference CallNonvirtualObjectMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniMembers.CallNonvirtualObjectMethod (@this, declaringType, this);
		}

		public JniLocalReference CallNonvirtualObjectMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, params JValue[] parameters)
		{
			return JniMembers.CallNonvirtualObjectMethod (@this, declaringType, this, parameters);
		}

		public bool CallNonvirtualBooleanMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniMembers.CallNonvirtualBooleanMethod (@this, declaringType, this);
		}

		public bool CallNonvirtualBooleanMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, params JValue[] parameters)
		{
			return JniMembers.CallNonvirtualBooleanMethod (@this, declaringType, this, parameters);
		}

		public sbyte CallNonvirtualByteMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniMembers.CallNonvirtualByteMethod (@this, declaringType, this);
		}

		public sbyte CallNonvirtualByteMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, params JValue[] parameters)
		{
			return JniMembers.CallNonvirtualByteMethod (@this, declaringType, this, parameters);
		}

		public char CallNonvirtualCharMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniMembers.CallNonvirtualCharMethod (@this, declaringType, this);
		}

		public char CallNonvirtualCharMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, params JValue[] parameters)
		{
			return JniMembers.CallNonvirtualCharMethod (@this, declaringType, this, parameters);
		}

		public short CallNonvirtualInt16Method (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniMembers.CallNonvirtualShortMethod (@this, declaringType, this);
		}

		public short CallNonvirtualInt16Method (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, params JValue[] parameters)
		{
			return JniMembers.CallNonvirtualShortMethod (@this, declaringType, this, parameters);
		}

		public int CallNonvirtualInt32Method (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniMembers.CallNonvirtualIntMethod (@this, declaringType, this);
		}

		public int CallNonvirtualInt32Method (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, params JValue[] parameters)
		{
			return JniMembers.CallNonvirtualIntMethod (@this, declaringType, this, parameters);
		}

		public long CallNonvirtualInt64Method (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniMembers.CallNonvirtualLongMethod (@this, declaringType, this);
		}

		public long CallNonvirtualInt64Method (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, params JValue[] parameters)
		{
			return JniMembers.CallNonvirtualLongMethod (@this, declaringType, this, parameters);
		}

		public float CallNonvirtualSingleMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniMembers.CallNonvirtualFloatMethod (@this, declaringType, this);
		}

		public float CallNonvirtualSingleMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, params JValue[] parameters)
		{
			return JniMembers.CallNonvirtualFloatMethod (@this, declaringType, this, parameters);
		}

		public double CallNonvirtualDoubleMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniMembers.CallNonvirtualDoubleMethod (@this, declaringType, this);
		}

		public double CallNonvirtualDoubleMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, params JValue[] parameters)
		{
			return JniMembers.CallNonvirtualDoubleMethod (@this, declaringType, this, parameters);
		}
	}
}

