using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public sealed class JniInstanceMethodID : JniMethodID
	{
		internal JniInstanceMethodID (IntPtr methodID)
			: base (methodID)
		{
		}

		public void CallVirtualVoidMethod (JniObjectReference @this)
		{
			JniEnvironment.Members.CallVoidMethod (@this, this);
		}

		public unsafe void CallVirtualVoidMethod (JniObjectReference @this, JValue* parameters)
		{
			JniEnvironment.Members.CallVoidMethod (@this, this, parameters);
		}

		public JniObjectReference CallVirtualObjectMethod (JniObjectReference @this)
		{
			return JniEnvironment.Members.CallObjectMethod (@this, this);
		}

		public unsafe JniObjectReference CallVirtualObjectMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallObjectMethod (@this, this, parameters);
		}

		public bool CallVirtualBooleanMethod (JniObjectReference @this)
		{
			return JniEnvironment.Members.CallBooleanMethod (@this, this);
		}

		public unsafe bool CallVirtualBooleanMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallBooleanMethod (@this, this, parameters);
		}

		public sbyte CallVirtualSByteMethod (JniObjectReference @this)
		{
			return JniEnvironment.Members.CallSByteMethod (@this, this);
		}

		public unsafe sbyte CallVirtualSByteMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallSByteMethod (@this, this, parameters);
		}

		public char CallVirtualCharMethod (JniObjectReference @this)
		{
			return JniEnvironment.Members.CallCharMethod (@this, this);
		}

		public unsafe char CallVirtualCharMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallCharMethod (@this, this, parameters);
		}

		public short CallVirtualInt16Method (JniObjectReference @this)
		{
			return JniEnvironment.Members.CallShortMethod (@this, this);
		}

		public unsafe short CallVirtualInt16Method (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallShortMethod (@this, this, parameters);
		}

		public int CallVirtualInt32Method (JniObjectReference @this)
		{
			return JniEnvironment.Members.CallIntMethod (@this, this);
		}

		public unsafe int CallVirtualInt32Method (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallIntMethod (@this, this, parameters);
		}

		public long CallVirtualInt64Method (JniObjectReference @this)
		{
			return JniEnvironment.Members.CallLongMethod (@this, this);
		}

		public unsafe long CallVirtualInt64Method (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallLongMethod (@this, this, parameters);
		}

		public float CallVirtualSingleMethod (JniObjectReference @this)
		{
			return JniEnvironment.Members.CallFloatMethod (@this, this);
		}

		public unsafe float CallVirtualSingleMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallFloatMethod (@this, this, parameters);
		}

		public double CallVirtualDoubleMethod (JniObjectReference @this)
		{
			return JniEnvironment.Members.CallDoubleMethod (@this, this);
		}

		public unsafe double CallVirtualDoubleMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.Members.CallDoubleMethod (@this, this, parameters);
		}

		public void CallNonvirtualVoidMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			JniEnvironment.Members.CallNonvirtualVoidMethod (@this, declaringType, this);
		}

		public unsafe void CallNonvirtualVoidMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			JniEnvironment.Members.CallNonvirtualVoidMethod (@this, declaringType, this, parameters);
		}

		public JniObjectReference CallNonvirtualObjectMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualObjectMethod (@this, declaringType, this);
		}

		public unsafe JniObjectReference CallNonvirtualObjectMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualObjectMethod (@this, declaringType, this, parameters);
		}

		public bool CallNonvirtualBooleanMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualBooleanMethod (@this, declaringType, this);
		}

		public unsafe bool CallNonvirtualBooleanMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualBooleanMethod (@this, declaringType, this, parameters);
		}

		public sbyte CallNonvirtualSByteMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualSByteMethod (@this, declaringType, this);
		}

		public unsafe sbyte CallNonvirtualSByteMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualSByteMethod (@this, declaringType, this, parameters);
		}

		public char CallNonvirtualCharMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualCharMethod (@this, declaringType, this);
		}

		public unsafe char CallNonvirtualCharMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualCharMethod (@this, declaringType, this, parameters);
		}

		public short CallNonvirtualInt16Method (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualShortMethod (@this, declaringType, this);
		}

		public unsafe short CallNonvirtualInt16Method (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualShortMethod (@this, declaringType, this, parameters);
		}

		public int CallNonvirtualInt32Method (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualIntMethod (@this, declaringType, this);
		}

		public unsafe int CallNonvirtualInt32Method (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualIntMethod (@this, declaringType, this, parameters);
		}

		public long CallNonvirtualInt64Method (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualLongMethod (@this, declaringType, this);
		}

		public unsafe long CallNonvirtualInt64Method (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualLongMethod (@this, declaringType, this, parameters);
		}

		public float CallNonvirtualSingleMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualFloatMethod (@this, declaringType, this);
		}

		public unsafe float CallNonvirtualSingleMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualFloatMethod (@this, declaringType, this, parameters);
		}

		public double CallNonvirtualDoubleMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.Members.CallNonvirtualDoubleMethod (@this, declaringType, this);
		}

		public unsafe double CallNonvirtualDoubleMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.Members.CallNonvirtualDoubleMethod (@this, declaringType, this, parameters);
		}
	}
}

