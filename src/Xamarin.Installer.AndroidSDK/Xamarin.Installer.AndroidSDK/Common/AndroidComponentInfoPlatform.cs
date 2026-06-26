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
	/// Information about the API level (platform) component
	/// </summary>
	public class AndroidComponentInfoPlatform : AndroidComponentInfo, IAndroidApiLevel,IEquatable <AndroidComponentInfoPlatform>
	{
		string detailedDescription;

		/// <summary>
		/// Gets the API level
		/// </summary>
		/// <value>The API level.</value>
		public string ApiLevel { get; }

		/// <summary>
		/// Gets the platform code name.
		/// </summary>
		/// <value>Platfor code name</value>
		public string CodeName { get; }

		/// <summary>
		/// Gets the layout library API version
		/// </summary>
		/// <value>Layout library API version</value>
		public string LayoutLibApi { get; }

		/// <summary>
		/// Description of the API level.
		/// </summary>
		/// <value>Platform description.</value>
		public string Description { get; internal set; }

		/// <summary>
		/// Is the Platform API level a preview?
		/// </summary>
		/// <value><see langword="true"/> if the Platform is not API stable.</value>
		public bool Preview { get; internal set; }

		internal AndroidComponentInfoPlatform (string type, string apiLevel, string codeName, string layoutLibApi) : base (type)
		{
			if (String.IsNullOrEmpty (apiLevel))
				throw new ArgumentException ("must not be null or empty", nameof (apiLevel));
			if (String.IsNullOrEmpty (layoutLibApi))
				throw new ArgumentException ("must not be null or empty", nameof (layoutLibApi));

			ApiLevel = apiLevel;
			CodeName = codeName;
			LayoutLibApi = layoutLibApi;
		}

		internal override void WritePackageXmlInfoDetails (XmlWriter writer, Repository repository)
		{
			writer.WriteStartElement ("api-level");
			writer.WriteString (ApiLevel ?? String.Empty);
			writer.WriteEndElement (); // api-level

			writer.WriteStartElement ("codename");
			writer.WriteString (CodeName ?? String.Empty);
			writer.WriteEndElement (); // codename

			writer.WriteStartElement ("preview");
			writer.WriteString (Preview.ToString ());
			writer.WriteEndElement (); // preview

			writer.WriteStartElement ("layoutlib");
			writer.WriteAttributeString ("api", ApiLevel ?? String.Empty);
			writer.WriteEndElement (); // layoutlib
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

			if (desc.Count > 0)
				detailedDescription = $"Platform: {String.Join (" ", desc)}";
			else
				detailedDescription = String.Empty;
			return detailedDescription;
		}

		/// <summary>
		/// Determines whether the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoPlatform"/> is
		/// equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoPlatform"/>.
		/// </summary>
		/// <param name="other">The <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoPlatform"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoPlatform"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoPlatform"/> is
		/// equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoPlatform"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (AndroidComponentInfoPlatform other)
		{
			if (!Equals ((AndroidComponentInfo)other))
				return false;

			if (!AreEqual (ApiLevel, other.ApiLevel))
				return false;

			if (!AreEqual (CodeName, other.CodeName))
				return false;

			if (Preview != other.Preview)
				return false;

			return AreEqual (LayoutLibApi, other.LayoutLibApi);
		}

		/// <summary>
		/// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoPlatform"/>.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoPlatform"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoPlatform"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return Equals (obj as AndroidComponentInfoPlatform);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoPlatform"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			int hashCode = base.GetHashCode ();

			if (ApiLevel != null)
				hashCode = hashCode.XorWith (ApiLevel.GetHashCode ());

			if (CodeName != null)
				hashCode = hashCode.XorWith (CodeName.GetHashCode ());

			hashCode = hashCode.XorWith (Preview.GetHashCode ());

			if (LayoutLibApi != null)
				hashCode = hashCode.XorWith (LayoutLibApi.GetHashCode ());
			
			return hashCode;
		}
	}
}
