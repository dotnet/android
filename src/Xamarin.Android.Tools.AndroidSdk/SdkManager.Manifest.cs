// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Xamarin.Android.Tools;

public partial class SdkManager
{
	// --- Manifest Parsing ---

	/// <summary>
	/// Downloads and parses the Android manifest feed to discover available components.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A list of manifest components available for the current platform.</returns>
	internal async Task<IReadOnlyList<SdkManifestComponent>> GetManifestComponentsAsync (CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed ();
		logger (TraceLevel.Info, $"Downloading manifest from {ManifestFeedUrl}...");
		// netstandard2.0 GetStringAsync has no CancellationToken overload; use GetAsync instead
		using var response = await httpClient.GetAsync (ManifestFeedUrl, cancellationToken).ConfigureAwait (false);
		response.EnsureSuccessStatusCode ();
		var xml = await DownloadUtils.ReadAsStringAsync (response.Content, cancellationToken).ConfigureAwait (false);
		return ParseManifest (xml);
	}

	/// <summary>
	/// Parses the Android manifest XML and returns components for the current platform.
	/// Uses XmlReader for better performance than XDocument/XElement.
	/// </summary>
	internal IReadOnlyList<SdkManifestComponent> ParseManifest (string xml)
	{
		var hostOs = GetManifestHostOs ();
		var hostArch = GetManifestHostArch ();
		var components = new List<SdkManifestComponent> ();

		using var stringReader = new StringReader (xml);
		using var reader = XmlReader.Create (stringReader, new XmlReaderSettings { IgnoreWhitespace = true });

		while (reader.Read ()) {
			if (reader.NodeType != XmlNodeType.Element)
				continue;

			// Skip root element
			if (reader.Depth == 0)
				continue;

			var elementName = reader.LocalName;
			var revision = reader.GetAttribute ("revision");
			if (string.IsNullOrEmpty (revision))
				continue;

			var component = new SdkManifestComponent {
				ElementName = elementName,
				Revision = revision!,
				Path = reader.GetAttribute ("path"),
				FilesystemPath = reader.GetAttribute ("filesystem-path"),
				Description = reader.GetAttribute ("description"),
				IsObsolete = string.Equals (reader.GetAttribute ("obsolete"), "True", StringComparison.OrdinalIgnoreCase),
			};

			// Read child elements to find matching URL
			if (!reader.IsEmptyElement) {
				var componentDepth = reader.Depth;
				while (reader.Read () && reader.Depth > componentDepth) {
					if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "urls") {
						var urlsDepth = reader.Depth;
						while (reader.Read () && reader.Depth > urlsDepth) {
							if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "url") {
								var urlHostOs = reader.GetAttribute ("host-os");
								var urlHostArch = reader.GetAttribute ("host-arch");

								if (!MatchesPlatform (urlHostOs, hostOs))
									continue;

								if (!string.IsNullOrEmpty (urlHostArch) && !string.Equals (urlHostArch, hostArch, StringComparison.OrdinalIgnoreCase))
									continue;

								var checksumTypeStr = reader.GetAttribute ("checksum-type");
								if (string.Equals (checksumTypeStr, "sha-256", StringComparison.OrdinalIgnoreCase))
									component.ChecksumType = ChecksumType.Sha256;
								else
									component.ChecksumType = ChecksumType.Sha1;
								component.Checksum = reader.GetAttribute ("checksum");

								var sizeStr = reader.GetAttribute ("size");
								if (long.TryParse (sizeStr, out var size))
									component.Size = size;

								// Read the URL text content
								component.DownloadUrl = reader.ReadElementContentAsString ()?.Trim ();
								break;
							}
						}
					}
				}
			}

			if (!string.IsNullOrEmpty (component.DownloadUrl))
				components.Add (component);
		}

		logger (TraceLevel.Verbose, $"Parsed {components.Count} components from manifest.");
		return components.AsReadOnly ();
	}

	static bool MatchesPlatform (string? urlHostOs, string hostOs)
	{
		if (string.IsNullOrEmpty (urlHostOs))
			return true; // No filter means any platform
		return string.Equals (urlHostOs, hostOs, StringComparison.OrdinalIgnoreCase);
	}

	static string GetManifestHostOs ()
	{
		if (OS.IsWindows) return "windows";
		if (OS.IsMac) return "macosx";
		if (OS.IsLinux) return "linux";
		throw new PlatformNotSupportedException ($"Unsupported operating system for Android SDK manifest.");
	}

	static string GetManifestHostArch ()
	{
		var arch = RuntimeInformation.OSArchitecture;
		switch (arch) {
			case Architecture.Arm64:
				return "aarch64";
			case Architecture.X64:
				return "x64";
			case Architecture.X86:
				return "x86";
			default:
				throw new PlatformNotSupportedException ($"Unsupported architecture '{arch}' for Android SDK manifest.");
		}
	}
}
