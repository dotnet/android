using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public sealed class JniInstanceMethodID : JniMethodID
	{
		JniInstanceMethodID ()
		{
		}

		public void CallVirtualVoidMethod (JniReferenceSafeHandle @this)
		{
			JniEnvironment.Members.CallVoidMethod (@this, this);
		}

		public unsafe void CallVirtualVoidMethod (JniReferenceSafeHandle @this, JValue* parameters)
		{
			JniEnvironment.Members.CallVoidMethod (@this, this, parameters);
		}

		public JniLocalReference CallVirtualObjectMethod (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.CallObjectMethod (@this, this);
		}

		public unsafe JniLocalReference CallVirtualObjectMethod (JniReferenceSafeHandle @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallObjectMethod (@this, this, parameters);
		}

		public bool CallVirtualBooleanMethod (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.CallBooleanMethod (@this, this);
		}

		public unsafe bool CallVirtualBooleanMethod (JniReferenceSafeHandle @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallBooleanMethod (@this, this, parameters);
		}

		public sbyte CallVirtualSByteMethod (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.CallSByteMethod (@this, this);
		}

		public unsafe sbyte CallVirtualSByteMethod (JniReferenceSafeHandle @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallSByteMethod (@this, this, parameters);
		}

		public char CallVirtualCharMethod (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.CallCharMethod (@this, this);
		}

		public unsafe char CallVirtualCharMethod (JniReferenceSafeHandle @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallCharMethod (@this, this, parameters);
		}

		public short CallVirtualInt16Method (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.CallShortMethod (@this, this);
		}

		public unsafe short CallVirtualInt16Method (JniReferenceSafeHandle @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallShortMethod (@this, this, parameters);
		}

		public int CallVirtualInt32Method (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.CallIntMethod (@this, this);
		}

		public unsafe int CallVirtualInt32Method (JniReferenceSafeHandle @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallIntMethod (@this, this, parameters);
		}

		public long CallVirtualInt64Method (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.CallLongMethod (@this, this);
		}

		public unsafe long CallVirtualInt64Method (JniReferenceSafeHandle @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallLongMethod (@this, this, parameters);
		}

		public float CallVirtualSingleMethod (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.CallFloatMethod (@this, this);
		}

		public unsafe float CallVirtualSingleMethod (JniReferenceSafeHandle @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallFloatMethod (@this, this, parameters);
		}

		public double CallVirtualDoubleMethod (JniReferenceSafeHandle @this)
		{
			return JniEnvironment.Members.CallDoubleMethod (@this, this);
		}

		public unsafe double CallVirtualDoubleMethod (JniReferenceSafeHandle @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallDoubleMethod (@this, this, parameters);
		}

		public void CallNonvirtualVoidMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			JniEnvironment.Members.CallNonvirtualVoidMethod (@this, declaringType, this);
		}

		public unsafe void CallNonvirtualVoidMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, JValue* parameters)
		{
			JniEnvironment.Members.CallNonvirtualVoidMethod (@this, declaringType, this, parameters);
		}

		public JniLocalReference CallNonvirtualObjectMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualObjectMethod (@this, declaringType, this);
		}

		public unsafe JniLocalReference CallNonvirtualObjectMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualObjectMethod (@this, declaringType, this, parameters);
		}

		public bool CallNonvirtualBooleanMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualBooleanMethod (@this, declaringType, this);
		}

		public unsafe bool CallNonvirtualBooleanMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualBooleanMethod (@this, declaringType, this, parameters);
		}

		public sbyte CallNonvirtualSByteMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualSByteMethod (@this, declaringType, this);
		}

		public unsafe sbyte CallNonvirtualSByteMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualSByteMethod (@this, declaringType, this, parameters);
		}

		public char CallNonvirtualCharMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualCharMethod (@this, declaringType, this);
		}

		public unsafe char CallNonvirtualCharMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualCharMethod (@this, declaringType, this, parameters);
		}

		public short CallNonvirtualInt16Method (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualShortMethod (@this, declaringType, this);
		}

		public unsafe short CallNonvirtualInt16Method (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualShortMethod (@this, declaringType, this, parameters);
		}

		public int CallNonvirtualInt32Method (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualIntMethod (@this, declaringType, this);
		}

		public unsafe int CallNonvirtualInt32Method (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualIntMethod (@this, declaringType, this, parameters);
		}

		public long CallNonvirtualInt64Method (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualLongMethod (@this, declaringType, this);
		}

		public unsafe long CallNonvirtualInt64Method (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualLongMethod (@this, declaringType, this, parameters);
		}

		public float CallNonvirtualSingleMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualFloatMethod (@this, declaringType, this);
		}

		public unsafe float CallNonvirtualSingleMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualFloatMethod (@this, declaringType, this, parameters);
		}

		public double CallNonvirtualDoubleMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualDoubleMethod (@this, declaringType, this);
		}

		public unsafe double CallNonvirtualDoubleMethod (JniReferenceSafeHandle @this, JniReferenceSafeHandle declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualDoubleMethod (@this, declaringType, this, parameters);
		}
	}
}

