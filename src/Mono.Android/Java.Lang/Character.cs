using System;
using Android.Runtime;

namespace Java.Lang {

	public partial class Character : IConvertible {

		public static explicit operator char (Java.Lang.Character value)
		{
			return value.CharValue ();
		}

		//
		// IConvertible Methods
		//
		TypeCode IConvertible.GetTypeCode ()
		{
			return TypeCode.Char;
		}

		bool IConvertible.ToBoolean (IFormatProvider? provider)
		{
			return Convert.ToBoolean (CharValue ());
		}

		byte IConvertible.ToByte (IFormatProvider? provider)
		{
			return Convert.ToByte (CharValue ());
		}

		char IConvertible.ToChar (IFormatProvider? provider)
		{
			return Convert.ToChar (CharValue ());
		}

		DateTime IConvertible.ToDateTime (IFormatProvider? provider)
		{
			return Convert.ToDateTime (CharValue ());
		}

		decimal IConvertible.ToDecimal (IFormatProvider? provider)
		{
			return Convert.ToDecimal (CharValue ());
		}

		double IConvertible.ToDouble (IFormatProvider? provider)
		{
			return Convert.ToDouble (CharValue ());
		}

		short IConvertible.ToInt16 (IFormatProvider? provider)
		{
			return Convert.ToInt16 (CharValue ());
		}

		int IConvertible.ToInt32 (IFormatProvider? provider)
		{
			return Convert.ToInt32 (CharValue ());
		}

		long IConvertible.ToInt64 (IFormatProvider? provider)
		{
			return Convert.ToInt64 (CharValue ());
		}

		sbyte IConvertible.ToSByte (IFormatProvider? provider)
		{
			return Convert.ToSByte (CharValue ());
		}

		float IConvertible.ToSingle (IFormatProvider? provider)
		{
			return Convert.ToSingle (CharValue ());
		}

		string IConvertible.ToString (IFormatProvider? provider)
		{
			return Convert.ToString (CharValue (), provider);
		}

		object IConvertible.ToType (Type conversionType, IFormatProvider? provider)
		{
			return System.Convert.ChangeType (CharValue (), conversionType, provider);
		}

		ushort IConvertible.ToUInt16 (IFormatProvider? provider)
		{
			return Convert.ToUInt16 (CharValue ());
		}

		uint IConvertible.ToUInt32 (IFormatProvider? provider)
		{
			return Convert.ToUInt32 (CharValue ());
		}

		ulong IConvertible.ToUInt64 (IFormatProvider? provider)
		{
			return Convert.ToUInt64 (CharValue ());
		}
	}
}
