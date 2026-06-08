//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Marek Habersack
//
//  All rights reserved.
//
using System;

namespace Xamarin.Installer.AndroidSDK.Common
{
	/// <summary>
	/// Base class for all types with a string identifier
	/// </summary>
	public abstract class ItemWithID : IEquatable<ItemWithID>
	{
		/// <summary>
		/// Gets the identifier string.
		/// </summary>
		/// <value>The identifier string</value>
		public string ID { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Xamarin.Installer.AndroidSDK.Common.ItemWithID"/> class.
		/// </summary>
		/// <param name="id">Identifier (required)</param>
		protected ItemWithID (string id)
		{
			if (String.IsNullOrEmpty (id))
				throw new ArgumentException ("Must not be null or empty", nameof (id));
			ID = id;
		}

		public bool Equals (ItemWithID other)
		{
			if (other == null)
				return false;

			if (ReferenceEquals (this, other))
				return true;

			if (String.Compare (ID, other.ID, StringComparison.Ordinal) != 0)
				return false;

			return true;
		}

		public override bool Equals (object obj)
		{
			return Equals (obj as ItemWithID);
		}

		public override int GetHashCode ()
		{
			return ID?.GetHashCode () ?? 0;
		}
	}
}
