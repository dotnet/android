//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft, Inc
//
//  All rights reserved.
//
using System;

using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK.Common
{
	/// <summary>
	/// Describes a single Android SDK channel. Google currently define 4 channels:
	/// <list type="bullet">
	/// <item>
	/// <term>stable</term><description></description>
	/// </item>
	/// <item>
	/// <term>beta</term><description></description>
	/// </item>
	/// <item>
	/// <term>dev</term><description></description>
	/// </item>
	/// <item>
	/// <term>canary</term><description></description>
	/// </item>
	/// </list>
	/// Note however that the list is defined directly in the Android SDK manifest document and may be 
	/// changed by Google at any time.
	/// </summary>
	public sealed class Channel : ItemWithID, IEquatable <Channel>
	{
		/// <summary>
		/// Gets the channel name
		/// </summary>
		/// <value>Channel name</value>
		public string Name { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Xamarin.Installer.AndroidSDK.Common.Channel"/> class.
		/// </summary>
		/// <param name="id">Identifier.</param>
		/// <param name="name">Name.</param>
		public Channel (string id, string name) : base (id)
		{
			if (String.IsNullOrEmpty (name))
				throw new ArgumentException ("Must not be null or empty", nameof (name));
			Name = name;
		}

		/// <summary>
		/// Determines whether the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.Channel"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.Channel"/>.
		/// </summary>
		/// <param name="other">The <see cref="T:Xamarin.Installer.AndroidSDK.Common.Channel"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.Channel"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="T:amarin.Installer.AndroidSDK.Common.Channel"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.Channel"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (Channel other)
		{
			if (other == null)
				return false;

			return Equals (other as ItemWithID);
		}

		/// <summary>
		/// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.Channel"/>.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.Channel"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.Channel"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return Equals (obj as Channel);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="T:Xamarin.Installer.AndroidSDK.Common.Channel"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			return base.GetHashCode ();
		}
	}
}
