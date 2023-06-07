using System;
using Android.Runtime;

namespace Java.Lang {

	public partial class Boolean : IConvertible {

		public static explicit operator bool (Java.Lang.Boolean value)
		{
			return value.BooleanValue ();
		}

		//
		// IConvertible Methods
		//
		TypeCode IConvertible.GetTypeCode ()
		{
			return TypeCode.Boolean;
		}

		bool IConvertible.ToBoolean (IFormatProvider? provider)
		{
			return BooleanValue ();
		}

		byte IConvertible.ToByte (IFormatProvider? provider)
		{
			return Convert.ToByte (BooleanValue ());
		}

		char IConvertible.ToChar (IFormatProvider? provider)
		{
			return Convert.ToChar (BooleanValue ());
		}

		DateTime IConvertible.ToDateTime (IFormatProvider? provider)
		{
			return Convert.ToDateTime (BooleanValue ());
		}

		decimal IConvertible.ToDecimal (IFormatProvider? provider)
		{
			return Convert.ToDecimal (BooleanValue ());
		}

		double IConvertible.ToDouble (IFormatProvider? provider)
		{
			return Convert.ToDouble (BooleanValue ());
		}

		short IConvertible.ToInt16 (IFormatProvider? provider)
		{
			return Convert.ToInt16 (BooleanValue ());
		}

		int IConvertible.ToInt32 (IFormatProvider? provider)
		{
			return Convert.ToInt32 (BooleanValue ());
		}

		long IConvertible.ToInt64 (IFormatProvider? provider)
		{
			return Convert.ToInt64 (BooleanValue ());
		}

		sbyte IConvertible.ToSByte (IFormatProvider? provider)
		{
			return Convert.ToSByte (BooleanValue ());
		}

		float IConvertible.ToSingle (IFormatProvider? provider)
		{
			return Convert.ToSingle (BooleanValue ());
		}

		string IConvertible.ToString (IFormatProvider? provider)
		{
			return Convert.ToString (BooleanValue (), provider);
		}

		object IConvertible.ToType (Type conversionType, IFormatProvider? provider)
		{
			return Convert.ChangeType (BooleanValue (), conversionType, provider);
		}

		ushort IConvertible.ToUInt16 (IFormatProvider? provider)
		{
			return Convert.ToUInt16 (BooleanValue ());
		}

		uint IConvertible.ToUInt32 (IFormatProvider? provider)
		{
			return Convert.ToUInt32 (BooleanValue ());
		}

		ulong IConvertible.ToUInt64 (IFormatProvider? provider)
		{
			return Convert.ToUInt64 (BooleanValue ());
		}
	}
}
