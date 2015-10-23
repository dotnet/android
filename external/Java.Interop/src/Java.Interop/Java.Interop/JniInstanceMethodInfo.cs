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

		public void InvokeVirtualVoidMethod (JniObjectReference @this)
		{
			JniEnvironment.InstanceMethods.CallVoidMethod (@this, this);
		}

		public unsafe void InvokeVirtualVoidMethod (JniObjectReference @this, JValue* parameters)
		{
			JniEnvironment.InstanceMethods.CallVoidMethod (@this, this, parameters);
		}

		public JniObjectReference InvokeVirtualObjectMethod (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallObjectMethod (@this, this);
		}

		public unsafe JniObjectReference InvokeVirtualObjectMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallObjectMethod (@this, this, parameters);
		}

		public bool InvokeVirtualBooleanMethod (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallBooleanMethod (@this, this);
		}

		public unsafe bool InvokeVirtualBooleanMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallBooleanMethod (@this, this, parameters);
		}

		public sbyte InvokeVirtualSByteMethod (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallByteMethod (@this, this);
		}

		public unsafe sbyte InvokeVirtualSByteMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallByteMethod (@this, this, parameters);
		}

		public char InvokeVirtualCharMethod (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallCharMethod (@this, this);
		}

		public unsafe char InvokeVirtualCharMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallCharMethod (@this, this, parameters);
		}

		public short InvokeVirtualInt16Method (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallShortMethod (@this, this);
		}

		public unsafe short InvokeVirtualInt16Method (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallShortMethod (@this, this, parameters);
		}

		public int InvokeVirtualInt32Method (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallIntMethod (@this, this);
		}

		public unsafe int InvokeVirtualInt32Method (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallIntMethod (@this, this, parameters);
		}

		public long InvokeVirtualInt64Method (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallLongMethod (@this, this);
		}

		public unsafe long InvokeVirtualInt64Method (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallLongMethod (@this, this, parameters);
		}

		public float InvokeVirtualSingleMethod (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallFloatMethod (@this, this);
		}

		public unsafe float InvokeVirtualSingleMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallFloatMethod (@this, this, parameters);
		}

		public double InvokeVirtualDoubleMethod (JniObjectReference @this)
		{
			return JniEnvironment.InstanceMethods.CallDoubleMethod (@this, this);
		}

		public unsafe double InvokeVirtualDoubleMethod (JniObjectReference @this, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallDoubleMethod (@this, this, parameters);
		}

		public void InvokeNonvirtualVoidMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (@this, declaringType, this);
		}

		public unsafe void InvokeNonvirtualVoidMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (@this, declaringType, this, parameters);
		}

		public JniObjectReference InvokeNonvirtualObjectMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualObjectMethod (@this, declaringType, this);
		}

		public unsafe JniObjectReference InvokeNonvirtualObjectMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualObjectMethod (@this, declaringType, this, parameters);
		}

		public bool InvokeNonvirtualBooleanMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualBooleanMethod (@this, declaringType, this);
		}

		public unsafe bool InvokeNonvirtualBooleanMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualBooleanMethod (@this, declaringType, this, parameters);
		}

		public sbyte InvokeNonvirtualSByteMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualByteMethod (@this, declaringType, this);
		}

		public unsafe sbyte InvokeNonvirtualSByteMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualByteMethod (@this, declaringType, this, parameters);
		}

		public char InvokeNonvirtualCharMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualCharMethod (@this, declaringType, this);
		}

		public unsafe char InvokeNonvirtualCharMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualCharMethod (@this, declaringType, this, parameters);
		}

		public short InvokeNonvirtualInt16Method (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualShortMethod (@this, declaringType, this);
		}

		public unsafe short InvokeNonvirtualInt16Method (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualShortMethod (@this, declaringType, this, parameters);
		}

		public int InvokeNonvirtualInt32Method (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualIntMethod (@this, declaringType, this);
		}

		public unsafe int InvokeNonvirtualInt32Method (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualIntMethod (@this, declaringType, this, parameters);
		}

		public long InvokeNonvirtualInt64Method (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualLongMethod (@this, declaringType, this);
		}

		public unsafe long InvokeNonvirtualInt64Method (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualLongMethod (@this, declaringType, this, parameters);
		}

		public float InvokeNonvirtualSingleMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualFloatMethod (@this, declaringType, this);
		}

		public unsafe float InvokeNonvirtualSingleMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualFloatMethod (@this, declaringType, this, parameters);
		}

		public double InvokeNonvirtualDoubleMethod (JniObjectReference @this, JniObjectReference declaringType)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualDoubleMethod (@this, declaringType, this);
		}

		public unsafe double InvokeNonvirtualDoubleMethod (JniObjectReference @this, JniObjectReference declaringType, JValue* parameters)
		{
			return JniEnvironment.InstanceMethods.CallNonvirtualDoubleMethod (@this, declaringType, this, parameters);
		}
	}
}

