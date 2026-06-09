//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft Corp. (http://microsoft.com)
//
//  All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Xml;

namespace Xamarin.Installer.AndroidSDK.Common
{
	/// <summary>
	/// Information about the extra Android component
	/// </summary>
	public class AndroidComponentInfoExtra : AndroidComponentInfo, IEquatable <AndroidComponentInfoExtra>
	{
		string detailedDescription;

		/// <summary>
		/// Gets system image vendor information
		/// </summary>
		/// <value>System image vendor</value>
		public PackageVendor Vendor { get; }

		internal AndroidComponentInfoExtra (string type, PackageVendor vendor) : base (type)
		{
			Vendor = vendor;
		}

		internal override void WritePackageXmlInfoDetails (XmlWriter writer, Repository repository)
		{
			WritePackageXmlVendor (writer, Vendor);
		}

		/// <summary>
		/// Gets the detailed description of the component
		/// </summary>
		/// <returns>Description</returns>
		protected override string GetDetailedDescription ()
		{
			if (detailedDescription != null)
				return detailedDescription;

			var desc = new List <string>();
			if (!String.IsNullOrEmpty (Vendor?.Display))
				desc.Add ($"({Vendor.Display})");

			if (desc.Count > 0)
				detailedDescription = $"Extra: {String.Join (" ", desc)}";
			else
				detailedDescription = String.Empty;
			return detailedDescription;
		}

		/// <summary>
		/// Determines whether the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoExtra"/> is
		/// equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoExtra"/>.
		/// </summary>
		/// <param name="other">The <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoExtra"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoExtra"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoExtra"/> is equal
		/// to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoExtra"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (AndroidComponentInfoExtra other)
		{
			if (!Equals ((AndroidComponentInfo)other))
				return false;

			if (Vendor != other.Vendor)
				return false;
			return true;
		}

		/// <summary>
		/// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoExtra"/>.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoExtra"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoExtra"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return Equals (obj as AndroidComponentInfoExtra);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoExtra"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			int hashCode = base.GetHashCode ();

			if (Vendor != null)
				hashCode = hashCode.XorWith (Vendor.GetHashCode ());

			return hashCode;
		}
	}
}
