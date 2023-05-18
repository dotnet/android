using System;
using Android.Runtime;

namespace Java.Lang {

	public partial class Float : IConvertible {

		public static explicit operator float (Java.Lang.Float value)
		{
			return value.FloatValue ();
		}

		//
		// IConvertible Methods
		//
		TypeCode IConvertible.GetTypeCode ()
		{
			return TypeCode.Single;
		}

		bool IConvertible.ToBoolean (IFormatProvider? provider)
		{
			return Convert.ToBoolean (FloatValue ());
		}

		byte IConvertible.ToByte (IFormatProvider? provider)
		{
			return Convert.ToByte (FloatValue ());
		}

		char IConvertible.ToChar (IFormatProvider? provider)
		{
			return Convert.ToChar (FloatValue ());
		}

		DateTime IConvertible.ToDateTime (IFormatProvider? provider)
		{
			return Convert.ToDateTime (FloatValue ());
		}

		decimal IConvertible.ToDecimal (IFormatProvider? provider)
		{
			return Convert.ToDecimal (FloatValue ());
		}

		double IConvertible.ToDouble (IFormatProvider? provider)
		{
			return Convert.ToDouble (FloatValue ());
		}

		short IConvertible.ToInt16 (IFormatProvider? provider)
		{
			return Convert.ToInt16 (FloatValue ());
		}

		int IConvertible.ToInt32 (IFormatProvider? provider)
		{
			return Convert.ToInt32 (FloatValue ());
		}

		long IConvertible.ToInt64 (IFormatProvider? provider)
		{
			return Convert.ToInt64 (FloatValue ());
		}

		sbyte IConvertible.ToSByte (IFormatProvider? provider)
		{
			return Convert.ToSByte (FloatValue ());
		}

		float IConvertible.ToSingle (IFormatProvider? provider)
		{
			return Convert.ToSingle (FloatValue ());
		}

		string IConvertible.ToString (IFormatProvider? provider)
		{
			return Convert.ToString (FloatValue (), provider);
		}

		object IConvertible.ToType (Type conversionType, IFormatProvider? provider)
		{
			return System.Convert.ChangeType (FloatValue (), conversionType, provider);
		}

		ushort IConvertible.ToUInt16 (IFormatProvider? provider)
		{
			return Convert.ToUInt16 (FloatValue ());
		}

		uint IConvertible.ToUInt32 (IFormatProvider? provider)
		{
			return Convert.ToUInt32 (FloatValue ());
		}

		ulong IConvertible.ToUInt64 (IFormatProvider? provider)
		{
			return Convert.ToUInt64 (FloatValue ());
		}
	}
}
