//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft Corp. (http://microsoft.com)
//
//  All rights reserved.
//
using System;
using System.Xml.Linq;

namespace Xamarin.Installer.AndroidSDK
{
	static partial class Extensions
	{
		public static string GetFullName (this XName name)
		{
			if (name == null)
				return String.Empty;

			if (!String.IsNullOrEmpty (name.NamespaceName))
				return $"{name.NamespaceName}:{name.LocalName}";

			return name.LocalName;
		}
	}
}
