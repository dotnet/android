//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft, Inc
//
//  All rights reserved.
//
using System;

namespace Xamarin.Installer.AndroidSDK.Common
{
	/// <summary>
	/// Represents component License
	/// </summary>
	public sealed class License : ItemWithID, IEquatable <License>
	{
		/// <summary>
		/// Content type. Currently only <c>text</c> is defined.
		/// </summary>
		/// <value>Content type.</value>
		public string Type { get; }

		/// <summary>
		/// Gets text of the license.
		/// </summary>
		/// <value>License text</value>
		public string Text { get; }

		internal License (string id, string type, string text) : base (id)
		{
			Type = type;
			Text = text;
		}

		/// <summary>
		/// Determines whether the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.License"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.License"/>.
		/// </summary>
		/// <param name="other">The <see cref="T:Xamarin.Installer.AndroidSDK.Common.License"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.License"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.License"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.License"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (License other)
		{
			if (other == null)
				return false;

			if (ReferenceEquals (this, other))
				return true;

			if (String.Compare (ID, other.ID, StringComparison.Ordinal) != 0)
				return false;

			if (String.Compare (Type, other.Type, StringComparison.Ordinal) != 0)
				return false;

			if (String.Compare (Text, other.Text, StringComparison.Ordinal) != 0)
				return false;

			return true;
		}

		/// <summary>
		/// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.License"/>.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.License"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.License"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return Equals (obj as License);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="T:Xamarin.Installer.AndroidSDK.Common.License"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			int hashCode = 0;

			hashCode = hashCode.XorWith (ID?.GetHashCode ());
			hashCode = hashCode.XorWith (Type?.GetHashCode ());
			return hashCode.XorWith (Text?.GetHashCode ());
		}
	}
}
