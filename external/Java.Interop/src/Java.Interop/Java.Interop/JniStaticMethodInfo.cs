using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{

	public sealed class JniStaticMethodInfo : JniMethodInfo
	{
		internal JniStaticMethodInfo (IntPtr methodID)
			: base (methodID)
		{
		}

		public void CallVoidMethod (JniObjectReference type)
		{
			JniEnvironment.StaticMethods.CallStaticVoidMethod (type, this);
		}

		public unsafe void CallVoidMethod (JniObjectReference type, JniArgumentValue* parameters)
		{
			JniEnvironment.StaticMethods.CallStaticVoidMethod (type, this, parameters);
		}

		public JniObjectReference CallObjectMethod (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticObjectMethod (type, this);
		}

		public unsafe JniObjectReference CallObjectMethod (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticObjectMethod (type, this, parameters);
		}

		public bool CallBooleanMethod (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticBooleanMethod (type, this);
		}

		public unsafe bool CallBooleanMethod (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticBooleanMethod (type, this, parameters);
		}

		public sbyte CallSByteMethod (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticByteMethod (type, this);
		}

		public unsafe sbyte CallSByteMethod (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticByteMethod (type, this, parameters);
		}

		public char CallCharMethod (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticCharMethod (type, this);
		}

		public unsafe char CallCharMethod (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticCharMethod (type, this, parameters);
		}

		public short CallInt16Method (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticShortMethod (type, this);
		}

		public unsafe short CallInt16Method (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticShortMethod (type, this, parameters);
		}

		public int CallInt32Method (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticIntMethod (type, this);
		}

		public unsafe int CallInt32Method (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticIntMethod (type, this, parameters);
		}

		public long CallInt64Method (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticLongMethod (type, this);
		}

		public unsafe long CallInt64Method (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticLongMethod (type, this, parameters);
		}

		public float CallSingleMethod (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticFloatMethod (type, this);
		}

		public unsafe float CallSingleMethod (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticFloatMethod (type, this, parameters);
		}

		public double CallDoubleMethod (JniObjectReference type)
		{
			return JniEnvironment.StaticMethods.CallStaticDoubleMethod (type, this);
		}

		public unsafe double CallDoubleMethod (JniObjectReference type, JniArgumentValue* parameters)
		{
			return JniEnvironment.StaticMethods.CallStaticDoubleMethod (type, this, parameters);
		}
	}
}
