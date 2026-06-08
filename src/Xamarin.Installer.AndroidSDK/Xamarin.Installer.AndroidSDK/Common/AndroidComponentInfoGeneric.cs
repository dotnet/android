//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft Corp. (http://microsoft.com)
//
//  All rights reserved.
//
using System;

namespace Xamarin.Installer.AndroidSDK.Common
{
	/// <summary>
	/// Information about a generic component. <seealso cref="AndroidComponentInfo"/>
	/// </summary>
#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
	public class AndroidComponentInfoGeneric : AndroidComponentInfo, IEquatable <AndroidComponentInfoGeneric>
#pragma warning restore CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
	{
		public AndroidComponentInfoGeneric (string type) : base (type)
		{}

		/// <summary>
		/// Builds and returns the detailed description of the component type. <seealso cref="AndroidComponentInfo.DetailedDescription"/>.
		/// This override returns an empty string.
		/// </summary>
		/// <returns>Detailed type description</returns>
		protected override string GetDetailedDescription ()
		{
			return String.Empty;
		}

		/// <summary>
		/// Determines whether the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoGeneric"/> is
		/// equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoGeneric"/>.
		/// </summary>
		/// <param name="other">The <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoGeneric"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoGeneric"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoGeneric"/> is
		/// equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoGeneric"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (AndroidComponentInfoGeneric other)
		{
			return Equals ((AndroidComponentInfo)other);
		}

		/// <summary>
		/// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoGeneric"/>.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoGeneric"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoGeneric"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return Equals (obj as AndroidComponentInfoGeneric);
		}
	}
}
