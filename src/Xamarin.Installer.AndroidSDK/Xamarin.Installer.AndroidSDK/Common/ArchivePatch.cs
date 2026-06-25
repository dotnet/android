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
	/// Represents a single archive patch instance. <seealso cref="Archive.Patches"/>
	/// </summary>
	public sealed class ArchivePatch : IEquatable <ArchivePatch>
	{
		/// <summary>
		/// Gets the revision of the parent archive this patch must be applied to.
		/// </summary>
		/// <value>Base archive revision</value>
		public AndroidRevision BasedOn { get; internal set; }

		/// <summary>
		/// Gets the patch download size
		/// </summary>
		/// <value>The size.</value>
		public ulong Size { get; internal set; }

		/// <summary>
		/// Gets the checksum of the patch. The algorithm is identified by <see cref="ChecksumType"/>;
		/// when <see cref="ChecksumType"/> is null or empty the checksum is assumed to be SHA-1.
		/// </summary>
		/// <value>Checksum digest as a hex string</value>
		public string Checksum { get; internal set; }

		/// <summary>
		/// Gets the checksum algorithm declared by the manifest for <see cref="Checksum"/>,
		/// for example <c>sha1</c> or <c>sha256</c>. A null or empty value means SHA-1.
		/// </summary>
		/// <value>Checksum algorithm name</value>
		public string ChecksumType { get; internal set; }

		/// <summary>
		/// Gets the patch URL
		/// </summary>
		/// <value>Archive URL</value>
		public Uri Url { get; internal set; }

		internal ArchivePatch ()
		{
		}

		/// <summary>
		/// Determines whether the specified <see cref="t:Xamarin.Installer.AndroidSDK.Common.ArchivePatch"/> is equal to the
		/// current <see cref="T:Xamarin.Installer.AndroidSDK.Common.ArchivePatch"/>.
		/// </summary>
		/// <param name="other">The <see cref="t:Xamarin.Installer.AndroidSDK.Common.ArchivePatch"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.ArchivePatch"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="t:Xamarin.Installer.AndroidSDK.Common.ArchivePatch"/> is equal to the
		/// current <see cref="T:Xamarin.Installer.AndroidSDK.Common.ArchivePatch"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (ArchivePatch other)
		{
			if (other == null)
				return false;

			if (ReferenceEquals (this, other))
				return true;

			if (Size != other.Size)
				return false;

			if (Url != other.Url)
				return false;

			if (String.Compare (Checksum, other.Checksum, StringComparison.Ordinal) != 0)
				return false;

			if (BasedOn == null) {
				if (other.BasedOn != null)
					return false;
			} else if (BasedOn != other.BasedOn)
				return false;

			return true;
		}

		/// <summary>
		/// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.ArchivePatch"/>.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.ArchivePatch"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.ArchivePatch"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return Equals (obj as ArchivePatch);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="T:Xamarin.Installer.AndroidSDK.Common.ArchivePatch"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			int hashCode = base.GetHashCode ();

			hashCode = hashCode.XorWith (Size.GetHashCode ());
			hashCode = hashCode.XorWith (Url?.GetHashCode ());
			hashCode = hashCode.XorWith (Checksum?.GetHashCode ());
			return hashCode.XorWith (BasedOn?.GetHashCode ());
		}
	}
}
