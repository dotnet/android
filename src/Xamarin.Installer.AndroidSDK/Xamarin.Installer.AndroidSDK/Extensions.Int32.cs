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
		public static int XorWith (this int value, int? maybeValue)
		{
			if (!maybeValue.HasValue)
				return value;

			return XorWith (value, maybeValue.Value);
		}

		public static int XorWith (this int value, int otherValue)
		{
			return value ^ otherValue;

		}
	}
}
