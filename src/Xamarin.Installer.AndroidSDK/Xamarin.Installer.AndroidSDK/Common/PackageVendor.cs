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
	/// Describes a package vendor
	/// </summary>
	public sealed class PackageVendor : ItemWithIDAndDisplay, IEquatable <PackageVendor>
	{
		public PackageVendor (string id, string display) : base (id, display)
		{
		}

		/// <summary>
		/// Determines whether the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageVendor"/> is equal to the
		/// current <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageVendor"/>.
		/// </summary>
		/// <param name="other">The <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageVendor"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageVendor"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageVendor"/> is equal to the
		/// current <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageVendor"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (PackageVendor other)
		{
			if (other == null)
				return false;

			if (!ReferenceEquals (this, other))
				return false;

			if (String.Compare (ID, other.ID, StringComparison.Ordinal) != 0)
				return false;

			return String.Compare (Display, other.Display, StringComparison.Ordinal) == 0;
		}

		/// <summary>
		/// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageVendor"/>.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageVendor"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageVendor"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return Equals (obj as PackageVendor);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageVendor"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			int hashCode = base.GetHashCode ();

			if (ID != null)
				hashCode = hashCode.XorWith (ID.GetHashCode ());
			if (Display != null)
				hashCode = hashCode.XorWith (Display.GetHashCode ());

			return hashCode;
		}
	}
}
