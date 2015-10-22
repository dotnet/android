using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public sealed class JniInstanceMethodInfo : JniMethodInfo
	{
		internal JniInstanceMethodInfo (IntPtr methodID)
			: base (methodID)
		{
		}

		public void CallVirtualVoidMethod (JniObjectReference @this)
		{
			JniEnvironment.InstanceMethods.CallVoidMethod (@this, this);
		}

		public unsafe void CallVirtualVoidMethod (JniObjectReference @this, JValue* parameters)
		{
			JniEnvironment.InstanceMethods.CallVoidMethod (@this, this, parameters);
		}

		public JniObjectReference CallVirtualObjectMethod (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallObjectMethod (@this, this);
		}

		public unsafe JniObjectReference CallVirtualObjectMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallObjectMethod (@this, this, parameters);
		}

		public bool CallVirtualBooleanMethod (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallBooleanMethod (@this, this);
		}

		public unsafe bool CallVirtualBooleanMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallBooleanMethod (@this, this, parameters);
		}

		public sbyte CallVirtualSByteMethod (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallByteMethod (@this, this);
		}

		public unsafe sbyte CallVirtualSByteMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallByteMethod (@this, this, parameters);
		}

		public char CallVirtualCharMethod (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallCharMethod (@this, this);
		}

		public unsafe char CallVirtualCharMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallCharMethod (@this, this, parameters);
		}

		public short CallVirtualInt16Method (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallShortMethod (@this, this);
		}

		public unsafe short CallVirtualInt16Method (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallShortMethod (@this, this, parameters);
		}

		public int CallVirtualInt32Method (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallIntMethod (@this, this);
		}

		public unsafe int CallVirtualInt32Method (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallIntMethod (@this, this, parameters);
		}

		public long CallVirtualInt64Method (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallLongMethod (@this, this);
		}

		public unsafe long CallVirtualInt64Method (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallLongMethod (@this, this, parameters);
		}

		public float CallVirtualSingleMethod (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallFloatMethod (@this, this);
		}

		public unsafe float CallVirtualSingleMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallFloatMethod (@this, this, parameters);
		}

		public double CallVirtualDoubleMethod (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallDoubleMethod (@this, this);
		}

		public unsafe double CallVirtualDoubleMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallDoubleMethod (@this, this, parameters);
		}

		public void CallNonvirtualVoidMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (@this, declaringType, this);
		}

		public unsafe void CallNonvirtualVoidMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (@this, declaringType, this, parameters);
		}

		public JniObjectReference CallNonvirtualObjectMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualObjectMethod (@this, declaringType, this);
		}

		public unsafe JniObjectReference CallNonvirtualObjectMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualObjectMethod (@this, declaringType, this, parameters);
		}

		public bool CallNonvirtualBooleanMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualBooleanMethod (@this, declaringType, this);
		}

		public unsafe bool CallNonvirtualBooleanMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualBooleanMethod (@this, declaringType, this, parameters);
		}

		public sbyte CallNonvirtualSByteMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualByteMethod (@this, declaringType, this);
		}

		public unsafe sbyte CallNonvirtualSByteMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualByteMethod (@this, declaringType, this, parameters);
		}

		public char CallNonvirtualCharMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualCharMethod (@this, declaringType, this);
		}

		public unsafe char CallNonvirtualCharMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualCharMethod (@this, declaringType, this, parameters);
		}

		public short CallNonvirtualInt16Method (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualShortMethod (@this, declaringType, this);
		}

		public unsafe short CallNonvirtualInt16Method (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualShortMethod (@this, declaringType, this, parameters);
		}

		public int CallNonvirtualInt32Method (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualIntMethod (@this, declaringType, this);
		}

		public unsafe int CallNonvirtualInt32Method (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualIntMethod (@this, declaringType, this, parameters);
		}

		public long CallNonvirtualInt64Method (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualLongMethod (@this, declaringType, this);
		}

		public unsafe long CallNonvirtualInt64Method (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualLongMethod (@this, declaringType, this, parameters);
		}

		public float CallNonvirtualSingleMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualFloatMethod (@this, declaringType, this);
		}

		public unsafe float CallNonvirtualSingleMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualFloatMethod (@this, declaringType, this, parameters);
		}

		public double CallNonvirtualDoubleMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualDoubleMethod (@this, declaringType, this);
		}

		public unsafe double CallNonvirtualDoubleMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualDoubleMethod (@this, declaringType, this, parameters);
		}
	}
}

