using System;
using System.Globalization;
using Android.Runtime;

namespace Java.Lang {

	public partial class Long : IConvertible {

		public static explicit operator long (Java.Lang.Long value)
		{
			return value.LongValue ();
		}

		//
		// IConvertible Methods
		//
		TypeCode IConvertible.GetTypeCode ()
		{
			return TypeCode.Int64;
		}

		bool IConvertible.ToBoolean (IFormatProvider? provider)
		{
			return Convert.ToBoolean (LongValue ());
		}

		byte IConvertible.ToByte (IFormatProvider? provider)
		{
			return Convert.ToByte (LongValue ());
		}

		char IConvertible.ToChar (IFormatProvider? provider)
		{
			return Convert.ToChar (LongValue ());
		}

		DateTime IConvertible.ToDateTime (IFormatProvider? provider)
		{
			return Convert.ToDateTime (LongValue ());
		}

		decimal IConvertible.ToDecimal (IFormatProvider? provider)
		{
			return Convert.ToDecimal (LongValue ());
		}

		double IConvertible.ToDouble (IFormatProvider? provider)
		{
			return Convert.ToDouble (LongValue ());
		}

		short IConvertible.ToInt16 (IFormatProvider? provider)
		{
			return Convert.ToInt16 (LongValue ());
		}

		int IConvertible.ToInt32 (IFormatProvider? provider)
		{
			return Convert.ToInt32 (LongValue ());
		}

		long IConvertible.ToInt64 (IFormatProvider? provider)
		{
			return Convert.ToInt64 (LongValue ());
		}

		sbyte IConvertible.ToSByte (IFormatProvider? provider)
		{
			return Convert.ToSByte (LongValue ());
		}

		float IConvertible.ToSingle (IFormatProvider? provider)
		{
			return Convert.ToSingle (LongValue ());
		}

		string IConvertible.ToString (IFormatProvider? provider)
		{
			return Convert.ToString (LongValue (), CultureInfo.InvariantCulture);
		}

		object IConvertible.ToType (Type conversionType, IFormatProvider? provider)
		{
			return System.Convert.ChangeType (LongValue (), conversionType, provider);
		}

		ushort IConvertible.ToUInt16 (IFormatProvider? provider)
		{
			return Convert.ToUInt16 (LongValue ());
		}

		uint IConvertible.ToUInt32 (IFormatProvider? provider)
		{
			return Convert.ToUInt32 (LongValue ());
		}

		ulong IConvertible.ToUInt64 (IFormatProvider? provider)
		{
			return Convert.ToUInt64 (LongValue ());
		}
	}
}
