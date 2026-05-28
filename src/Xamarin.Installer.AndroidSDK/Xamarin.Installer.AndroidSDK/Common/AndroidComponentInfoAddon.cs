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
	/// Information about the addon Android component
	/// </summary>
	public class AndroidComponentInfoAddon : AndroidComponentInfo, IAndroidApiLevel, IEquatable <AndroidComponentInfoAddon>
	{
		string detailedDescription;

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

		/// <summary>
		/// List of libraries shipped with this addon
		/// </summary>
		/// <value>Libraries shipped with this addon</value>
		public IList<PackageLibrary> Libraries { get; }

		internal AndroidComponentInfoAddon(string type, string apiLevel, string codeName, PackageTag tag, PackageVendor vendor, IList <PackageLibrary> libraries) : base (type)
		{
			if (String.IsNullOrEmpty (apiLevel))
				throw new ArgumentException ("must not be null or empty", nameof (apiLevel));

			ApiLevel = apiLevel;
			CodeName = codeName;
			Tag = tag;
			Vendor = vendor;
			Libraries = libraries;
		}

		internal override void WritePackageXmlInfoDetails (XmlWriter writer, Repository repository)
		{
			writer.WriteStartElement ("api-level");
			writer.WriteString (ApiLevel ?? String.Empty);
			writer.WriteEndElement (); // api-level

			writer.WriteStartElement ("codename");
			writer.WriteString (CodeName ?? String.Empty);
			writer.WriteEndElement (); // codename

			WritePackageXmlVendor (writer, Vendor);
			WritePackageXmlTag (writer, Tag);

			writer.WriteStartElement ("libraries");
			if (Libraries != null && Libraries.Count > 0) {
				foreach (PackageLibrary lib in Libraries) {
					if (lib == null)
						continue;
					writer.WriteStartElement ("library");
					writer.WriteAttributeString ("localJarPath", lib?.LocalJarPath ?? String.Empty);
					writer.WriteAttributeString ("name", lib?.Name ?? String.Empty);
					writer.WriteStartElement ("description");
					writer.WriteString (lib?.Description ?? String.Empty);
					writer.WriteEndElement (); // description
					writer.WriteEndElement (); // library
				}
			}
			writer.WriteEndElement (); // libraries
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
			if (!String.IsNullOrEmpty (ApiLevel))
				desc.Add ($"API {ApiLevel}");
			if (!String.IsNullOrEmpty (CodeName))
				desc.Add ($"\"{CodeName}\"");
			if (!String.IsNullOrEmpty (Vendor?.Display))
				desc.Add ($"({Vendor.Display})");

			if (desc.Count > 0)
				detailedDescription = $"Addon: {String.Join (" ", desc)}";
			else
				detailedDescription = String.Empty;
			return detailedDescription;
		}

		/// <summary>
		/// Determines whether the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoAddon"/> is
		/// equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoAddon"/>.
		/// </summary>
		/// <param name="other">The <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoAddon"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoAddon"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoAddon"/> is equal
		/// to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoAddon"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (AndroidComponentInfoAddon other)
		{
			if (!Equals ((AndroidComponentInfo)other))
				return false;

			if (!AreEqual (ApiLevel, other.ApiLevel))
				return false;

			if (Tag != other.Tag)
				return false;

			if (Vendor != other.Vendor)
				return false;

			if (!AreEqual (CodeName, other.CodeName))
				return false;

			return Libraries.AreEqual (other.Libraries);
		}

		/// <summary>
		/// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoAddon"/>.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoAddon"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoAddon"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return Equals (obj as AndroidComponentInfoAddon);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoAddon"/> object.
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
