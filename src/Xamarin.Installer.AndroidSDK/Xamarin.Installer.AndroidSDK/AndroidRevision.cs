//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft, Inc
//
//  All rights reserved.
//
using System;
using System.Text;

using Xamarin.Installer.Common;

namespace Xamarin.Installer.AndroidSDK
{
	/// <summary>
	/// Represents Android component revision. Only the <see cref="Major"/> component is guaranteed to be present.
	/// </summary>
	public sealed class AndroidRevision : IEquatable <AndroidRevision>, IComparable <AndroidRevision>
	{
		/// <summary>
		/// Gets the major revision number
		/// </summary>
		/// <value>Major revision number</value>
		public int Major { get; private set; }

		/// <summary>
		/// Gets the minor revision number. Value is valid if it's equal or higer than zero.
		/// </summary>
		/// <value>Minor revision number</value>
		public int Minor { get; private set; }

		/// <summary>
		/// Gets the micro revision number. Value is valid if it's equal or higer than zero.
		/// </summary>
		/// <value>Micro revision number</value>
		public int Micro { get; private set; }

		/// <summary>
		/// Gets the minor preview number. Value is valid if it's equal or higer than zero.
		/// </summary>
		/// <value>Preview revision number</value>
		public int Preview { get; private set; }

		/// <summary>
		/// Indicates whether or not this revision instance represents a valid revision
		/// </summary>
		/// <value><c>true</c> if valid; otherwise, <c>false</c>.</value>
		public bool IsValid => Major > 0;

		/// <summary>
		/// If set to <c>true</c>, indicates that <see cref="Equals(AndroidRevision)"/> should consider comparing only
		/// the <see cref="Major"/> and <see cref="Minor"/> properties.
		/// </summary>
		/// <value><c>true</c> to compare only major and minor components; otherwise, <c>false</c>.</value>
		public bool ConsiderOnlyMajorMinor { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> class.
		/// </summary>
		/// <param name="major">Major revision number (required, must be higher than or equal to zero)</param>
		/// <param name="minor">Minor revision number (optional)</param>
		/// <param name="micro">Micro revision number (optional)</param>
		/// <param name="preview">Preview revision number (optional)</param>
		/// <param name="throwIfInvalid">If set to <c>true</c> throw an exception if revision is invalid.</param>
		public AndroidRevision (int major, int minor = -1, int micro = -1, int preview = -1, bool throwIfInvalid = true)
		{
			Init (major, minor, micro, preview, throwIfInvalid);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> class. Attempts to
		/// parse the revision string passed in the <paramref name="rev"/> parameter.
		/// </summary>
		/// <param name="rev">Revision string to parse</param>
		/// <param name="throwIfInvalid">If set to <c>true</c> throw an exception if revision is invalid.</param>
		public AndroidRevision (string rev, bool throwIfInvalid = true)
		{
			if (String.IsNullOrEmpty (rev))
				goto setDefault;

			if (rev.IndexOf ('.') < 0) {
				int v;

				if (!Int32.TryParse (rev, out v)) {
					Logger.Info ($"Version is not an integer: {rev}");
					goto setDefault;
				}

				Init (v, throwIfInvalid: throwIfInvalid);
				return;
			}

			Version ver;
			if (!Version.TryParse (rev, out ver)) {
				Logger.Info ($"Package revision is not a valid version: {rev}");
				goto setDefault;
			}

			Init (ver.Major, ver.Minor, ver.Build, ver.Revision, throwIfInvalid);
			return;

		setDefault:
			Init (0, throwIfInvalid: throwIfInvalid);
		}

		public AndroidRevision (AndroidRevision other, bool throwIfInvalid = true)
		{
			if (other == null)
				throw new ArgumentNullException (nameof (other));
			Init (other.Major, other.Minor, other.Micro, other.Preview, throwIfInvalid);
		}

		void Init (int major, int minor = -1, int micro = -1, int preview = -1, bool throwIfInvalid = true)
		{
			if (major < 0 && throwIfInvalid)
				throw new ArgumentOutOfRangeException(nameof(major), "Must not be less than zero");

			Major = EnsureValidValue (major);
			Minor = EnsureValidValue (minor);
			Micro = EnsureValidValue (micro);
			Preview = EnsureValidValue (preview);
		}

		int EnsureValidValue (int value)
		{
			if (value < 0)
				return -1;
			return value;
		}

		/// <summary>
		/// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> in the standard 
		/// dotted format.
		/// </summary>
		/// <returns>A <see cref="T:System.String"/> that represents the current <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/>.</returns>
		public override string ToString ()
		{
			var sb = new StringBuilder ();
			sb.Append (Major);
			AppendValue (Minor, sb);
			AppendValue (Micro, sb);
			AppendValue (Preview, sb);

			return sb.ToString ();
		}

		void AppendValue (int value, StringBuilder sb)
		{
			if (value < 0)
				return;
			sb.Append ('.');
			sb.Append (value);
		}

		/// <summary>
		/// Determines whether the specified <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> is equal to the
		/// current <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/>.
		/// </summary>
		/// <param name="other">The <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (AndroidRevision other)
		{
			if (other == null)
				return false;

			if (ReferenceEquals (this, other))
				return true;

			if (Major != other.Major)
				return false;

			if (Minor != other.Minor)
				return false;

			if (ConsiderOnlyMajorMinor)
				return true;
			
			if (Micro != other.Micro)
				return false;

			return Preview == other.Preview;
		}

		public bool EqualsValid(AndroidRevision other)
		{
			if (other == null)
				return false;

			if (ReferenceEquals(this, other))
				return true;

			if (!IsValid || !other.IsValid)
				return false;

			if (Major != other.Major)
				return false;

			if (Minor == -1 || other.Minor == -1)
				return true;
			if (Minor != other.Minor)
				return false;

			if (Micro == -1 || other.Micro == -1)
				return true;
			if (Micro != other.Micro)
				return false;

			if (Preview == -1 || other.Preview == -1)
				return true;
			return Preview == other.Preview;
		}

		/// <summary>
		/// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/>.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
		/// <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return Equals (obj as AndroidRevision);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			int hashCode = Major.GetHashCode ();
			hashCode = hashCode.XorWith (Minor);
			hashCode = hashCode.XorWith (Micro);
			return hashCode.XorWith (Preview);
		}

		/// <summary>
		/// Compares this instance of <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> to another one.
		/// </summary>
		/// <returns>
		/// <list type="bullet">
		/// <item>
		/// <term>1</term><description>if this instance is bigger than <paramref name="other"/></description>
		/// </item>
		/// <item>
		/// <term>0</term><description>if this instance is equal to <paramref name="other"/></description>
		/// </item>
		/// <item>
		/// <term>-1</term><description>if this instance is smaller than <paramref name="other"/></description>
		/// </item>
		/// </list>"></returns>
		/// <param name="other">Other.</param>
		public int CompareTo (AndroidRevision other)
		{
			if (other == null)
				return 1;

			if (Major != other.Major)
				return Major > other.Major ? 1 : -1;

			if (Minor != other.Minor)
				return Minor > other.Minor ? 1 : -1;

			if (Micro != other.Micro)
				return Micro > other.Micro ? 1 : -1;

			if (Preview != other.Preview)
				return Preview > other.Preview ? 1 : -1;

			return 0;
		}

		/// <summary>
		/// Determines whether a specified instance of <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> is equal to
		/// another specified <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/>.
		/// </summary>
		/// <param name="rev1">The first <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> to compare.</param>
		/// <param name="rev2">The second <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> to compare.</param>
		/// <returns><c>true</c> if <c>rev1</c> and <c>rev2</c> are equal; otherwise, <c>false</c>.</returns>
		public static bool operator == (AndroidRevision rev1, AndroidRevision rev2)
		{
			if (Object.ReferenceEquals (rev1, null)) {
				return Object.ReferenceEquals (rev2, null);
			}

			return rev1.Equals (rev2);
		}

		/// <summary>
		/// Determines whether a specified instance of <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> is not equal
		/// to another specified <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/>.
		/// </summary>
		/// <param name="rev1">The first <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> to compare.</param>
		/// <param name="rev2">The second <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> to compare.</param>
		/// <returns><c>true</c> if <c>rev1</c> and <c>rev2</c> are not equal; otherwise, <c>false</c>.</returns>
		public static bool operator != (AndroidRevision rev1, AndroidRevision rev2)
		{
			return !(rev1 == rev2);
		}

		/// <summary>
		/// Determines whether one specified <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> is lower than another
		/// specfied <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/>.
		/// </summary>
		/// <param name="rev1">The first <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> to compare.</param>
		/// <param name="rev2">The second <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> to compare.</param>
		/// <returns><c>true</c> if <c>rev1</c> is lower than <c>rev2</c>; otherwise, <c>false</c>.</returns>
		public static bool operator < (AndroidRevision rev1, AndroidRevision rev2)
		{
			if (rev1 == null)
				throw new ArgumentNullException (nameof (rev1));
			return rev1.CompareTo (rev2) < 0;
		}

		/// <summary>
		/// Determines whether one specified <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> is lower than or equal
		/// to another specfied <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/>.
		/// </summary>
		/// <param name="rev1">The first <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> to compare.</param>
		/// <param name="rev2">The second <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> to compare.</param>
		/// <returns><c>true</c> if <c>rev1</c> is lower than or equal to <c>rev2</c>; otherwise, <c>false</c>.</returns>
		public static bool operator <= (AndroidRevision rev1, AndroidRevision rev2)
		{
			if (rev1 == null)
				throw new ArgumentNullException (nameof (rev1));
			return rev1.CompareTo (rev2) <= 0;
		}

		/// <summary>
		/// Determines whether one specified <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> is greater than
		/// another specfied <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/>.
		/// </summary>
		/// <param name="rev1">The first <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> to compare.</param>
		/// <param name="rev2">The second <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> to compare.</param>
		/// <returns><c>true</c> if <c>rev1</c> is greater than <c>rev2</c>; otherwise, <c>false</c>.</returns>
		public static bool operator > (AndroidRevision rev1, AndroidRevision rev2)
		{
			return rev2 < rev1;
		}

		/// <summary>
		/// Determines whether one specified <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> is greater than or
		/// equal to another specfied <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/>.
		/// </summary>
		/// <param name="rev1">The first <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> to compare.</param>
		/// <param name="rev2">The second <see cref="T:Xamarin.Installer.AndroidSDK.AndroidRevision"/> to compare.</param>
		/// <returns><c>true</c> if <c>rev1</c> is greater than or equal to <c>rev2</c>; otherwise, <c>false</c>.</returns>
		public static bool operator >= (AndroidRevision rev1, AndroidRevision rev2)
		{
			return rev2 <= rev1;
		}
	}
}
