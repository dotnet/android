using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{

	public sealed class JniStaticMethodID : JniMethodID
	{
		JniStaticMethodID ()
		{
		}

		public void CallVoidMethod (JniReferenceSafeHandle type)
		{
			JniEnvironment.Members.CallStaticVoidMethod (type, this);
		}

		public void CallVoidMethod (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			JniEnvironment.Members.CallStaticVoidMethod (type, this, parameters);
		}

		public JniLocalReference CallObjectMethod (JniReferenceSafeHandle type)
		{
			return JniEnvironment.Members.CallStaticObjectMethod (type, this);
		}

		public JniLocalReference CallObjectMethod (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniEnvironment.Members.CallStaticObjectMethod (type, this, parameters);
		}

		public bool CallBooleanMethod (JniReferenceSafeHandle type)
		{
			return JniEnvironment.Members.CallStaticBooleanMethod (type, this);
		}

		public bool CallBooleanMethod (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniEnvironment.Members.CallStaticBooleanMethod (type, this, parameters);
		}

		public sbyte CallSByteMethod (JniReferenceSafeHandle type)
		{
			return JniEnvironment.Members.CallStaticSByteMethod (type, this);
		}

		public sbyte CallSByteMethod (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniEnvironment.Members.CallStaticSByteMethod (type, this, parameters);
		}

		public char CallCharMethod (JniReferenceSafeHandle type)
		{
			return JniEnvironment.Members.CallStaticCharMethod (type, this);
		}

		public char CallCharMethod (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniEnvironment.Members.CallStaticCharMethod (type, this, parameters);
		}

		public short CallInt16Method (JniReferenceSafeHandle type)
		{
			return JniEnvironment.Members.CallStaticShortMethod (type, this);
		}

		public short CallInt16Method (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniEnvironment.Members.CallStaticShortMethod (type, this, parameters);
		}

		public int CallInt32Method (JniReferenceSafeHandle type)
		{
			return JniEnvironment.Members.CallStaticIntMethod (type, this);
		}

		public int CallInt32Method (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniEnvironment.Members.CallStaticIntMethod (type, this, parameters);
		}

		public long CallInt64Method (JniReferenceSafeHandle type)
		{
			return JniEnvironment.Members.CallStaticLongMethod (type, this);
		}

		public long CallInt64Method (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniEnvironment.Members.CallStaticLongMethod (type, this, parameters);
		}

		public float CallSingleMethod (JniReferenceSafeHandle type)
		{
			return JniEnvironment.Members.CallStaticFloatMethod (type, this);
		}

		public float CallSingleMethod (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniEnvironment.Members.CallStaticFloatMethod (type, this, parameters);
		}

		public double CallDoubleMethod (JniReferenceSafeHandle type)
		{
			return JniEnvironment.Members.CallStaticDoubleMethod (type, this);
		}

		public double CallDoubleMethod (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniEnvironment.Members.CallStaticDoubleMethod (type, this, parameters);
		}
	}
}
