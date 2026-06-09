//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Marek Habersack
//
//  All rights reserved.
//
using System;
using System.Collections.Generic;

namespace Xamarin.Installer.AndroidSDK
{
	static partial class Extensions
	{
		public static bool AreEqual<T> (this IList<T> one, IList<T> two)
		{
			if (one == null)
				return two == null;
			if (two == null)
				return false;

			if (one.Count != two.Count)
				return false;

			// Order of elements shouldn't matter I think
			foreach (T item in one) {
				if (!two.Contains (item))
					return false;
			}

			return true;
		}
	}
}
