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
	/// Information about the system image Android component
	/// </summary>
	public class AndroidComponentInfoSystemImage : AndroidComponentInfo, IAndroidApiLevel, IEquatable <AndroidComponentInfoSystemImage>
	{
		/// <summary>
		/// The default architecture
		/// </summary>
		public static readonly AndroidSystemImageAbi DefaultAbi = AndroidSystemImageAbi.X86;

		string detailedDescription;

		/// <summary>
		/// System image ABI
		/// </summary>
		/// <value>System image ABI</value>
		public AndroidSystemImageAbi Abi { get; }

		/// <summary>
		/// Gets the textual version of the system image ABI
		/// </summary>
		/// <value>Name of the ABI</value>
		public string AbiName { get; }

		/// <summary>
		/// Gets the API level.
		/// </summary>
		/// <value>The API level.</value>
		public string ApiLevel { get; }

		/// <summary>
		/// Gets the platform source code name.
		/// </summary>
		/// <value>Platfor code name</value>
		public string CodeName { get; }

		/// <summary>
		/// Gets the tag associated with the system image
		/// </summary>
		/// <value>Package tag</value>
		public PackageTag Tag { get; }

		/// <summary>
		/// Gets system image vendor information
		/// </summary>
		/// <value>System image vendor</value>
		public PackageVendor Vendor { get; }

		internal AndroidComponentInfoSystemImage (string type, AndroidSystemImageAbi abi, string abiName, string apiLevel, string codeName, PackageTag tag, PackageVendor vendor) : base (type)
		{
			if (String.IsNullOrEmpty (apiLevel))
				throw new ArgumentException ("must not be null or empty", nameof (apiLevel));

			Abi = abi;
			AbiName = abiName;
			ApiLevel = apiLevel;
			CodeName = codeName;
			Tag = tag;
			Vendor = vendor;
		}

		internal override void WritePackageXmlInfoDetails (XmlWriter writer, Repository repository)
		{
			writer.WriteStartElement ("api-level");
			writer.WriteString (ApiLevel ?? String.Empty);
			writer.WriteEndElement (); // api-level

			writer.WriteStartElement ("codename");
			writer.WriteString (CodeName ?? String.Empty);
			writer.WriteEndElement (); // codename

			WritePackageXmlTag (writer, Tag);
			WritePackageXmlVendor (writer, Vendor);

			writer.WriteStartElement ("abi");
			writer.WriteString (GetAbiManifestString ());
			writer.WriteEndElement (); // abi
		}

		public string GetAbiManifestString ()
		{
			return String.IsNullOrEmpty (AbiName) ? Abi.ToString ().ToLowerInvariant () : AbiName.ToLowerInvariant ();
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
			desc.Add ($"{Abi}");
			if (!String.IsNullOrEmpty (ApiLevel))
				desc.Add ($"API {ApiLevel}");
			if (!String.IsNullOrEmpty (CodeName))
				desc.Add ($"\"{CodeName}\"");
			if (!String.IsNullOrEmpty (Vendor?.Display))
				desc.Add ($"({Vendor.Display})");

			detailedDescription = $"System Image: {String.Join (" ", desc)}";
			return detailedDescription;
		}

		/// <summary>
		/// Determines whether the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSystemImage"/>
		/// is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSystemImage"/>.
		/// </summary>
		/// <param name="other">The <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSystemImage"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSystemImage"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSystemImage"/> is
		/// equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSystemImage"/>;
		/// otherwise, <c>false</c>.</returns>
		public bool Equals (AndroidComponentInfoSystemImage other)
		{
			if (!Equals ((AndroidComponentInfo)other))
				return false;

			if (Abi != other.Abi)
				return false;

			if (!AreEqual (ApiLevel, other.ApiLevel))
				return false;

			if (Tag != other.Tag)
				return false;

			if (Vendor != other.Vendor)
				return false;

			if (!AreEqual (CodeName, other.CodeName))
				return false;

			return true;
		}

		/// <summary>
		/// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSystemImage"/>.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSystemImage"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSystemImage"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return Equals (obj as AndroidComponentInfoSystemImage);
		}

		/// <summary>
		/// Serves as a hash function for a
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSystemImage"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			int hashCode = base.GetHashCode ();

			if (ApiLevel != null)
				hashCode = hashCode.XorWith (ApiLevel.GetHashCode ());

			if (CodeName != null)
				hashCode = hashCode.XorWith (CodeName.GetHashCode ());

			if (Tag != null)
				hashCode = hashCode.XorWith (Tag.GetHashCode ());

			if (Vendor != null)
				hashCode = hashCode.XorWith (Vendor.GetHashCode ());

			return hashCode;
		}
	}
}
