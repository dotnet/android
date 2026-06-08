//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft Corp. (http://microsoft.com)
//
//  All rights reserved.
//
using System;
using System.Xml;

namespace Xamarin.Installer.AndroidSDK.Common
{
	/// <summary>
	/// Base class for all the types containing detailed information about specific component groups/kinds.
	/// </summary>
	public abstract class AndroidComponentInfo : IEquatable <AndroidComponentInfo>
	{
		/// <summary>
		/// Gets the component type name. The name corresponds to the Google SDK remote package type, not the .NET one.
		/// </summary>
		/// <value>Type name</value>
		public string Type { get; }

		/// <summary>
		/// Gets the printable detailed description of the package type.
		/// </summary>
		/// <value>Detailed type description</value>
		public string DetailedDescription => GetDetailedDescription ();

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfo"/> class.
		/// </summary>
		/// <param name="type">Type name (required to be not null and not empty).</param>
		protected AndroidComponentInfo (string type)
		{
			if (String.IsNullOrEmpty (type))
				throw new ArgumentException ("must not be null or empty", nameof (type));

			Type = type;
		}

		/// <summary>
		/// Builds and returns the detailed description of the component type. <seealso cref="DetailedDescription"/>
		/// </summary>
		/// <returns>Detailed type description</returns>
		protected abstract string GetDetailedDescription ();

		internal void WritePackageXmlInfo (XmlWriter writer, Repository repository)
		{
			writer.WriteStartElement ("type-details");
			writer.WriteAttributeString ("xmlns", "xsi", null, repository.GetNamespaceUri ("xsi"));
			writer.WriteAttributeString ("xsi", "type", null, Type);

			WritePackageXmlInfoDetails (writer, repository);

			writer.WriteEndElement (); // type-details
		}

		/// <summary>
		/// Writes XML representation of the package vendor data.
		/// </summary>
		/// <param name="writer">XML writer</param>
		/// <param name="vendor">Vendor data</param>
		protected void WritePackageXmlVendor (XmlWriter writer, PackageVendor vendor)
		{
			writer.WriteStartElement ("vendor");
			writer.WriteStartElement ("id");
			writer.WriteString (vendor?.ID ?? String.Empty);
			writer.WriteEndElement (); // id

			writer.WriteStartElement ("display");
			writer.WriteString (vendor?.Display ?? String.Empty);
			writer.WriteEndElement (); // display
			writer.WriteEndElement (); // vendor
		}

		/// <summary>
		/// Writes XML representation of the package tag data.
		/// </summary>
		/// <param name="writer">XML Writer</param>
		/// <param name="tag">Tag data</param>
		protected void WritePackageXmlTag (XmlWriter writer, PackageTag tag)
		{
			writer.WriteStartElement ("tag");
			writer.WriteStartElement ("id");
			writer.WriteString (tag?.ID ?? String.Empty);
			writer.WriteEndElement (); // id

			writer.WriteStartElement ("display");
			writer.WriteString (tag?.Display ?? String.Empty);
			writer.WriteEndElement (); // display
			writer.WriteEndElement (); // tag
		}

		internal virtual void WritePackageXmlInfoDetails (XmlWriter writer, Repository repository)
		{
		}

		/// <summary>
		/// Determines whether the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfo"/> is equal
		/// to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfo"/>.
		/// </summary>
		/// <param name="other">The <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfo"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfo"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfo"/> is equal to
		/// the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfo"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (AndroidComponentInfo other)
		{
			if (other == null)
				return false;

			if (ReferenceEquals (this, other))
				return true;

			return String.Compare (Type, other.Type, StringComparison.Ordinal) == 0;
		}

		/// <summary>
		/// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfo"/>.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfo"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfo"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return Equals (obj as AndroidComponentInfo);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="T:Xamarin.Installer.AndroidSDK.Common.AndroidComponentInfo"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			int hashCode = base.GetHashCode ();
			if (Type == null)
				return hashCode;

			return hashCode.XorWith (Type.GetHashCode ());
		}

		/// <summary>
		/// A helper to compare two strings.
		/// </summary>
		/// <returns><c>true</c>, if strings are equal, <c>false</c> otherwise.</returns>
		/// <param name="one">first string</param>
		/// <param name="two">second string</param>
		protected bool AreEqual (string one, string two)
		{
			if (one == null)
				return two == null;

			return String.Compare (one, two, StringComparison.Ordinal) == 0;
		}
	}
}
