//
// KeystoreEntry.cs
//
// Author:
//       Greg Munn <greg.munn@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
//

using System;

namespace Xamarin.AndroidTools.PublicationUtilities
{
	public sealed class KeystoreEntry
	{
		public KeystoreEntry (string keystore, string alias, DateTime creationDate, string validityInfo)
		{
			Keystore = keystore;
			Alias = alias;
			CreationDate = creationDate;
			ValidityInfo = validityInfo;
		}

		public string Keystore { get; private set; }

		public string Alias { get; private set; }

		public DateTime CreationDate { get; private set; }

		public string ValidityInfo { get; private set; }
	}
}
