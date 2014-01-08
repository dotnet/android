using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{

	public sealed class JniStaticMethodID : JniMethodID
	{
		JniStaticMethodID ()
		{
		}

		public JniLocalReference CallObjectMethod (JniReferenceSafeHandle type)
		{
			return JniMembers.CallStaticObjectMethod (type, this);
		}

		public JniLocalReference CallObjectMethod (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniMembers.CallStaticObjectMethod (type, this, parameters);
		}

		public bool CallBooleanMethod (JniReferenceSafeHandle type)
		{
			return JniMembers.CallStaticBooleanMethod (type, this);
		}

		public bool CallBooleanMethod (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniMembers.CallStaticBooleanMethod (type, this, parameters);
		}

		public sbyte CallByteMethod (JniReferenceSafeHandle type)
		{
			return JniMembers.CallStaticByteMethod (type, this);
		}

		public sbyte CallByteMethod (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniMembers.CallStaticByteMethod (type, this, parameters);
		}

		public char CallCharMethod (JniReferenceSafeHandle type)
		{
			return JniMembers.CallStaticCharMethod (type, this);
		}

		public char CallCharMethod (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniMembers.CallStaticCharMethod (type, this, parameters);
		}

		public short CallInt16Method (JniReferenceSafeHandle type)
		{
			return JniMembers.CallStaticShortMethod (type, this);
		}

		public short CallInt16Method (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniMembers.CallStaticShortMethod (type, this, parameters);
		}

		public int CallInt32Method (JniReferenceSafeHandle type)
		{
			return JniMembers.CallStaticIntMethod (type, this);
		}

		public int CallInt32Method (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniMembers.CallStaticIntMethod (type, this, parameters);
		}

		public long CallInt64Method (JniReferenceSafeHandle type)
		{
			return JniMembers.CallStaticLongMethod (type, this);
		}

		public long CallInt64Method (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniMembers.CallStaticLongMethod (type, this, parameters);
		}

		public float CallSingleMethod (JniReferenceSafeHandle type)
		{
			return JniMembers.CallStaticFloatMethod (type, this);
		}

		public float CallSingleMethod (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniMembers.CallStaticFloatMethod (type, this, parameters);
		}

		public double CallDoubleMethod (JniReferenceSafeHandle type)
		{
			return JniMembers.CallStaticDoubleMethod (type, this);
		}

		public double CallDoubleMethod (JniReferenceSafeHandle type, params JValue[] parameters)
		{
			return JniMembers.CallStaticDoubleMethod (type, this, parameters);
		}
	}
}
