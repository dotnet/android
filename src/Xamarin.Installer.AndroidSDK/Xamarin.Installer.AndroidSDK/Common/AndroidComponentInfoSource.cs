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
	/// Information about the platform source code Android component
	/// </summary>
	public class AndroidComponentInfoSource : AndroidComponentInfo, IAndroidApiLevel, IEquatable <AndroidComponentInfoSource>
	{
		string detailedDescription;

		/// <summary>
		/// Gets the API level
		/// </summary>
		/// <value>The API level.</value>
		public string ApiLevel { get; }

		/// <summary>
		/// Gets the platform source code name.
		/// </summary>
		/// <value>Platfor code name</value>
		public string CodeName { get; }

		internal AndroidComponentInfoSource (string type, string apiLevel, string codeName) : base (type)
		{
			if (String.IsNullOrEmpty (apiLevel))
				throw new ArgumentException ("must not be null or empty", nameof (apiLevel));

			ApiLevel = apiLevel;
			CodeName = codeName;
		}

		internal override void WritePackageXmlInfoDetails (XmlWriter writer, Repository repository)
		{
			writer.WriteStartElement ("api-level");
			writer.WriteString (ApiLevel ?? String.Empty);
			writer.WriteEndElement (); // api-level

			writer.WriteStartElement ("codename");
			writer.WriteString (CodeName ?? String.Empty);
			writer.WriteEndElement (); // codename
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
				detailedDescription = $"Platform Source: {String.Join (" ", desc)}";
			else
				detailedDescription = String.Empty;
			return detailedDescription;
		}

		/// <summary>
		/// Determines whether the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSource"/> is
		/// equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSource"/>.
		/// </summary>
		/// <param name="other">The <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSource"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSource"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSource"/> is equal
		/// to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSource"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (AndroidComponentInfoSource other)
		{
			if (!Equals ((AndroidComponentInfo)other))
				return false;

			if (!AreEqual (ApiLevel, other.ApiLevel))
				return false;

			return AreEqual (CodeName, other.CodeName);
		}

		/// <summary>
		/// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSource"/>.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSource"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSource"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return Equals (obj as AndroidComponentInfoSource);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfoSource"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			int hashCode = base.GetHashCode ();

			if (ApiLevel != null)
				hashCode = hashCode.XorWith (ApiLevel.GetHashCode ());

			if (CodeName != null)
				hashCode = hashCode.XorWith (CodeName.GetHashCode ());

			return hashCode;
		}
	}
}
