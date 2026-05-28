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
	/// Describes a single package dependency
	/// </summary>
	public sealed class Dependency : IEquatable<Dependency>
	{
		/// <summary>
		/// Manifest path to the dependency
		/// </summary>
		/// <value>Dependency path</value>
		public string Path { get; }

		/// <summary>
		/// Gets the minimum revision of the dependency package
		/// </summary>
		/// <value>The minimum revision.</value>
		public AndroidRevision MinRevision { get; }

		public AndroidSDKPlatform Platform { get; set; } = AndroidSDKPlatform.Unknown;
		public bool IsPlatformSpecific => Platform != AndroidSDKPlatform.Any && Platform != AndroidSDKPlatform.Unknown;

		internal Dependency (string path, AndroidRevision minRevision = null)
		{
			if (String.IsNullOrEmpty (path))
				throw new ArgumentException ("must not be null or empty", nameof (path));

			Path = path;
			MinRevision = minRevision;
		}

		internal Dependency (Dependency other)
		{
			if (other == null)
				throw new ArgumentNullException (nameof (other));

			Path = other.Path;
			if (other.MinRevision != null)
				MinRevision = new AndroidRevision (other.MinRevision);
			Platform = other.Platform;
		}

		/// <summary>
		/// Determines whether the specified <see cref="t:Xamarin.Installer.AndroidSDK.Common.Dependency"/> is equal to the
		/// current <see cref="T:Xamarin.Installer.AndroidSDK.Common.Dependency"/>.
		/// </summary>
		/// <param name="other">The <see cref="t:Xamarin.Installer.AndroidSDK.Common.Dependency"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.Dependency"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="t:Xamarin.Installer.AndroidSDK.Common.Dependency"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.Dependency"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (Dependency other)
		{
			if (other == null)
				return false;

			if (ReferenceEquals (this, other))
				return true;

			if (String.Compare (Path, other.Path, StringComparison.Ordinal) != 0)
				return false;

			if (MinRevision == null) {
				if (other.MinRevision != null)
					return false;
			} else if (!MinRevision.Equals (other.MinRevision))
				return false;

			return Platform == other.Platform;
		}

		/// <summary>
		/// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.Dependency"/>.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.Dependency"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Common.Dependency"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return Equals (obj as Dependency);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="T:Xamarin.Installer.AndroidSDK.Common.Dependency"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			int hashCode = base.GetHashCode ();
			hashCode = hashCode.XorWith (Path?.GetHashCode ());
			return hashCode.XorWith (MinRevision?.GetHashCode ());
		}
	}
}
