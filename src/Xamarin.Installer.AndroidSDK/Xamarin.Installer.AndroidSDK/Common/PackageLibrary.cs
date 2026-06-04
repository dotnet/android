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
	/// Describes a single library contained in a package
	/// </summary>
	public sealed class PackageLibrary : IEquatable <PackageLibrary>
	{
		/// <summary>
		/// Gets the local path of the library JAR archive.
		/// </summary>
		/// <value>Path to the JAR archive</value>
		public string LocalJarPath { get; internal set; }

		/// <summary>
		/// Gets the library name
		/// </summary>
		/// <value>Library name</value>
		public string Name { get; internal set; }

		/// <summary>
		/// Gets the library description.
		/// </summary>
		/// <value>The description.</value>
		public string Description { get; internal set; }

		/// <summary>
		/// Determines whether the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageLibrary"/> is equal to the
		/// current <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageLibrary"/>.
		/// </summary>
		/// <param name="other">The <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageLibrary"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageLibrary"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageLibrary"/> is equal to the
		/// current <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageLibrary"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (PackageLibrary other)
		{
			if (other == null)
				return false;

			if (!ReferenceEquals (this, other))
				return false;

			return String.Compare (LocalJarPath, other.LocalJarPath, StringComparison.Ordinal) == 0;
		}

		/// <summary>
		/// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageLibrary"/>.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageLibrary"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageLibrary"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return Equals (obj as PackageLibrary);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="T:Xamarin.Installer.AndroidSDK.Common.PackageLibrary"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			int hashCode = base.GetHashCode ();

			if (LocalJarPath != null)
				hashCode = hashCode.XorWith (LocalJarPath.GetHashCode ());

			if (Name != null)
				hashCode = hashCode.XorWith (Name.GetHashCode ());

			if (Description != null)
				hashCode = hashCode.XorWith (Description.GetHashCode ());

			return hashCode;
		}
	}
}
