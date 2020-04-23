using System;
using Android.Runtime;

namespace Java.Lang {

	public partial class Integer : IConvertible {

		public static explicit operator int (Java.Lang.Integer value)
		{
			return value.IntValue ();
		}

		//
		// IConvertible Methods
		//
		TypeCode IConvertible.GetTypeCode ()
		{
			return TypeCode.Int32;
		}

		bool IConvertible.ToBoolean (IFormatProvider? provider)
		{
			return Convert.ToBoolean (IntValue ());
		}

		byte IConvertible.ToByte (IFormatProvider? provider)
		{
			return Convert.ToByte (IntValue ());
		}

		char IConvertible.ToChar (IFormatProvider? provider)
		{
			return Convert.ToChar (IntValue ());
		}

		DateTime IConvertible.ToDateTime (IFormatProvider? provider)
		{
			return Convert.ToDateTime (IntValue ());
		}

		decimal IConvertible.ToDecimal (IFormatProvider? provider)
		{
			return Convert.ToDecimal (IntValue ());
		}

		double IConvertible.ToDouble (IFormatProvider? provider)
		{
			return Convert.ToDouble (IntValue ());
		}

		short IConvertible.ToInt16 (IFormatProvider? provider)
		{
			return Convert.ToInt16 (IntValue ());
		}

		int IConvertible.ToInt32 (IFormatProvider? provider)
		{
			return Convert.ToInt32 (IntValue ());
		}

		long IConvertible.ToInt64 (IFormatProvider? provider)
		{
			return Convert.ToInt64 (IntValue ());
		}

		sbyte IConvertible.ToSByte (IFormatProvider? provider)
		{
			return Convert.ToSByte (IntValue ());
		}

		float IConvertible.ToSingle (IFormatProvider? provider)
		{
			return Convert.ToSingle (IntValue ());
		}

		string IConvertible.ToString (IFormatProvider? provider)
		{
			return Convert.ToString (IntValue ());
		}

		object IConvertible.ToType (Type conversionType, IFormatProvider? provider)
		{
			return System.Convert.ChangeType (IntValue (), conversionType, provider);
		}

		ushort IConvertible.ToUInt16 (IFormatProvider? provider)
		{
			return Convert.ToUInt16 (IntValue ());
		}

		uint IConvertible.ToUInt32 (IFormatProvider? provider)
		{
			return Convert.ToUInt32 (IntValue ());
		}

		ulong IConvertible.ToUInt64 (IFormatProvider? provider)
		{
			return Convert.ToUInt64 (IntValue ());
		}
	}
}
