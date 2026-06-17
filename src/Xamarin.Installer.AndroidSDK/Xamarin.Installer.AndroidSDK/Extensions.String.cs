//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Marek Habersack
//
//  All rights reserved.
//
using System;

namespace Xamarin.Installer.AndroidSDK
{
	static partial class Extensions
	{
		public static uint AsUInt (this string s, bool throwOnError = false)
		{
			if (String.IsNullOrEmpty (s))
				return 0;

			uint ret;
			if (UInt32.TryParse (s, out ret))
				return ret;

			if (throwOnError)
				throw new InvalidOperationException ($"Unable to convert string '{s}' to an unsigned integer");
			return 0;
		}

		public static ulong AsULong (this string s, bool throwOnError = false)
		{
			if (String.IsNullOrEmpty (s))
				return 0;

			ulong ret;
			if (UInt64.TryParse (s, out ret))
				return ret;

			if (throwOnError)
				throw new InvalidOperationException ($"Unable to convert string '{s}' to an unsigned long integer");
			return 0;
		}

		public static bool AsBool (this string s, bool throwOnError = false)
		{
			if (String.IsNullOrEmpty (s))
				return false;

			bool ret;
			if (Boolean.TryParse (s, out ret))
				return ret;

			if (throwOnError)
				throw new InvalidOperationException ($"Unable to convert string '{s}' to boolean");

			return false;
		}

		public static Uri AsUri (this string s, bool throwOnError = false)
		{
			if (String.IsNullOrEmpty (s))
				return null;

			Uri ret;
			if (Uri.TryCreate (s, UriKind.RelativeOrAbsolute, out ret))
				return ret;

			if (throwOnError)
				throw new InvalidOperationException ($"Unable to parse string '{s}' as a valid URI");

			return null;
		}
	}
}
