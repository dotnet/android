//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft, Inc (http://microsoft.com)
//
//  All rights reserved.
//
using System;

namespace Xamarin.Installer.AndroidSDK.Common
{
	/// <summary>
	/// Abstract base class for all the clases that have some sort of identifier and a display name.
	/// </summary>
	public abstract class ItemWithIDAndDisplay : ItemWithID
	{
		/// <summary>
		/// Gets the display name of the item
		/// </summary>
		/// <value>Item display name</value>
		public string Display { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Xamarin.Installer.AndroidSDK.Common.ItemWithIDAndDisplay"/> class.
		/// </summary>
		/// <param name="id">Identifier.</param>
		/// <param name="display">Display.</param>
		protected ItemWithIDAndDisplay (string id, string display) : base (id)
		{
			Display = display ?? String.Empty;
		}
	}
}
