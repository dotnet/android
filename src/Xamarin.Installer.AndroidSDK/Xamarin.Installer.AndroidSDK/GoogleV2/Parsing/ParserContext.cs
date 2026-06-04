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
using System.IO;

using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	public class ParserContext : IDisposable
	{
		Uri currentManifestURL;
		Uri currentManifestBaseURL;

		public IParserErrorHandler ErrorHandler { get; }
		public Uri BaseURL { get; }
		public Uri RepositoryManifestURL { get; }
		public Uri AddonsListManifestURL { get; }
		public Stream RepositoryManifest { get; }
		public Stream AddonsListManifest { get; }

		public Uri CurrentManifestBaseURL => GetCurrentManifestBaseURL ();

		public Uri CurrentManifestURL {
			get { return currentManifestURL; }
			set {
				currentManifestURL = value;
				currentManifestBaseURL = null;
			}
		}


		public IDictionary<string, License> Licenses { get; private set; }
		public IDictionary<string, Channel> Channels { get; private set; }

		/// <summary>
		/// Parse Context
		/// </summary>
		/// <param name="errorHandler"></param>
		/// <param name="baseURL"></param>
		/// <param name="repositoryManifestURL"></param>
		/// <param name="repositoryManifest"></param>
		/// <param name="addonsListManifestURL"></param>
		/// <param name="addonsListManifest">If <c>null</c>, addons are not available</param>
		public ParserContext (IParserErrorHandler errorHandler, Uri baseURL, Uri repositoryManifestURL, Stream repositoryManifest, Uri addonsListManifestURL, Stream addonsListManifest = null)
		{
			ErrorHandler = errorHandler ?? throw new ArgumentNullException (nameof (errorHandler));
			BaseURL = baseURL ?? throw new ArgumentNullException (nameof (baseURL));
			RepositoryManifestURL = repositoryManifestURL ?? throw new ArgumentNullException (nameof (repositoryManifestURL));
			RepositoryManifest = repositoryManifest ?? throw new ArgumentNullException (nameof (repositoryManifest));
			AddonsListManifestURL = addonsListManifestURL ?? throw new ArgumentNullException (nameof (addonsListManifestURL));
			AddonsListManifest = addonsListManifest;
		}

		public ParserContext (IParserErrorHandler errorHandler, Uri repositoryManifestURL)
		{
			ErrorHandler = errorHandler ?? throw new ArgumentNullException (nameof (errorHandler));
			RepositoryManifestURL = repositoryManifestURL ?? throw new ArgumentNullException (nameof (repositoryManifestURL));
		}

		public void Dispose ()
		{
			RepositoryManifest?.Dispose ();
			AddonsListManifest?.Dispose ();
		}

		Uri GetCurrentManifestBaseURL ()
		{
			if (currentManifestBaseURL != null)
				return currentManifestBaseURL;
			currentManifestBaseURL = currentManifestURL == null ? null : Helpers.GetBaseURL (currentManifestURL);
			return currentManifestBaseURL;
		}
	}
}
