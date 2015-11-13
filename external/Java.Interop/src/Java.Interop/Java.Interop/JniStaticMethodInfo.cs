using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public sealed class JniStaticMethodInfo : JniMethodInfo
	{
		public JniStaticMethodInfo (IntPtr methodID)
			: base (methodID)
		{
		}

		public void InvokeVoidMethod (JniObjectReference type)
		{
			JniEnvironment.StaticMethods.CallStaticVoidMethod (type, this);
		}

		public unsafe void InvokeVoidMethod (JniObjectReference type, JniArgumentValue* parameters)
		{
			JniEnvironment.StaticMethods.CallStaticVoidMethod (type, this, parameters);
		}

		public JniObjectReference InvokeObjectMethod (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticObjectMethod (type, this);
		}

		public unsafe JniObjectReference InvokeObjectMethod (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticObjectMethod (type, this, parameters);
		}

		public bool InvokeBooleanMethod (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticBooleanMethod (type, this);
		}

		public unsafe bool InvokeBooleanMethod (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticBooleanMethod (type, this, parameters);
		}

		public sbyte InvokeSByteMethod (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticByteMethod (type, this);
		}

		public unsafe sbyte InvokeSByteMethod (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticByteMethod (type, this, parameters);
		}

		public char InvokeCharMethod (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticCharMethod (type, this);
		}

		public unsafe char InvokeCharMethod (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticCharMethod (type, this, parameters);
		}

		public short InvokeInt16Method (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticShortMethod (type, this);
		}

		public unsafe short InvokeInt16Method (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticShortMethod (type, this, parameters);
		}

		public int InvokeInt32Method (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticIntMethod (type, this);
		}

		public unsafe int InvokeInt32Method (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticIntMethod (type, this, parameters);
		}

		public long InvokeInt64Method (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticLongMethod (type, this);
		}

		public unsafe long InvokeInt64Method (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticLongMethod (type, this, parameters);
		}

		public float InvokeSingleMethod (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticFloatMethod (type, this);
		}

		public unsafe float InvokeSingleMethod (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticFloatMethod (type, this, parameters);
		}

		public double InvokeDoubleMethod (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticDoubleMethod (type, this);
		}

		public unsafe double InvokeDoubleMethod (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticDoubleMethod (type, this, parameters);
		}
	}
}
