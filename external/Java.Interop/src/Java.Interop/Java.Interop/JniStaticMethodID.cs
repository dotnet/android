using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{

	public sealed class JniStaticMethodID : JniMethodID
	{
		internal JniStaticMethodID (IntPtr methodID)
			: base (methodID)
		{
		}

		public void CallVoidMethod (JniObjectReference type)
		{
			JniEnvironment.Members.CallStaticVoidMethod (type, this);
		}

		public unsafe void CallVoidMethod (JniObjectReference type, JValue* parameters)
		{
			JniEnvironment.Members.CallStaticVoidMethod (type, this, parameters);
		}

		public JniObjectReference CallObjectMethod (JniObjectReference type)
		{
			return JniEnvironment.Members.CallStaticObjectMethod (type, this);
		}

		public unsafe JniObjectReference CallObjectMethod (JniObjectReference type, JValue* parameters)
		{
			return JniEnvironment.Members.CallStaticObjectMethod (type, this, parameters);
		}

		public bool CallBooleanMethod (JniObjectReference type)
		{
			return JniEnvironment.Members.CallStaticBooleanMethod (type, this);
		}

		public unsafe bool CallBooleanMethod (JniObjectReference type, JValue* parameters)
		{
			return JniEnvironment.Members.CallStaticBooleanMethod (type, this, parameters);
		}

		public sbyte CallSByteMethod (JniObjectReference type)
		{
			return JniEnvironment.Members.CallStaticSByteMethod (type, this);
		}

		public unsafe sbyte CallSByteMethod (JniObjectReference type, JValue* parameters)
		{
			return JniEnvironment.Members.CallStaticSByteMethod (type, this, parameters);
		}

		public char CallCharMethod (JniObjectReference type)
		{
			return JniEnvironment.Members.CallStaticCharMethod (type, this);
		}

		public unsafe char CallCharMethod (JniObjectReference type, JValue* parameters)
		{
			return JniEnvironment.Members.CallStaticCharMethod (type, this, parameters);
		}

		public short CallInt16Method (JniObjectReference type)
		{
			return JniEnvironment.Members.CallStaticShortMethod (type, this);
		}

		public unsafe short CallInt16Method (JniObjectReference type, JValue* parameters)
		{
			return JniEnvironment.Members.CallStaticShortMethod (type, this, parameters);
		}

		public int CallInt32Method (JniObjectReference type)
		{
			return JniEnvironment.Members.CallStaticIntMethod (type, this);
		}

		public unsafe int CallInt32Method (JniObjectReference type, JValue* parameters)
		{
			return JniEnvironment.Members.CallStaticIntMethod (type, this, parameters);
		}

		public long CallInt64Method (JniObjectReference type)
		{
			return JniEnvironment.Members.CallStaticLongMethod (type, this);
		}

		public unsafe long CallInt64Method (JniObjectReference type, JValue* parameters)
		{
			return JniEnvironment.Members.CallStaticLongMethod (type, this, parameters);
		}

		public float CallSingleMethod (JniObjectReference type)
		{
			return JniEnvironment.Members.CallStaticFloatMethod (type, this);
		}

		public unsafe float CallSingleMethod (JniObjectReference type, JValue* parameters)
		{
			return JniEnvironment.Members.CallStaticFloatMethod (type, this, parameters);
		}

		public double CallDoubleMethod (JniObjectReference type)
		{
			return JniEnvironment.Members.CallStaticDoubleMethod (type, this);
		}

		public unsafe double CallDoubleMethod (JniObjectReference type, JValue* parameters)
		{
			return JniEnvironment.Members.CallStaticDoubleMethod (type, this, parameters);
		}
	}
}
