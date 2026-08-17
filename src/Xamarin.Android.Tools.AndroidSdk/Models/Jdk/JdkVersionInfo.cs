// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xamarin.Android.Tools
{
	/// <summary>
	/// Represents information about an available JDK version from Microsoft OpenJDK.
	/// </summary>
	public class JdkVersionInfo
	{
		/// <summary>Major version number (e.g. 17, 21).</summary>
		public int MajorVersion { get; }

		/// <summary>Display name for the version (e.g. "Microsoft OpenJDK 17").</summary>
		public string DisplayName { get; }

		/// <summary>Download URL for the current platform.</summary>
		public string DownloadUrl { get; }

		/// <summary>URL for the SHA-256 checksum file.</summary>
		public string ChecksumUrl { get; }

		/// <summary>Expected file size in bytes, or 0 if unknown.</summary>
		public long Size { get; internal set; }

		/// <summary>SHA-256 checksum for download verification, if fetched.</summary>
		public string? Checksum { get; internal set; }

		/// <summary>The actual download URL after following redirects (reveals the specific version).</summary>
		public string? ResolvedUrl { get; internal set; }

		public JdkVersionInfo (int majorVersion, string displayName, string downloadUrl, string checksumUrl, long size = 0, string? checksum = null)
		{
			MajorVersion = majorVersion;
			DisplayName = displayName;
			DownloadUrl = downloadUrl;
			ChecksumUrl = checksumUrl;
			Size = size;
			Checksum = checksum;
		}

		public override string ToString () => DisplayName;
	}
}
