// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xamarin.Android.Tools;

internal record SdkManifestComponent
{
	public string ElementName { get; set; } = "";
	public string Revision { get; set; } = "";
	public string? Path { get; set; }
	public string? FilesystemPath { get; set; }
	public string? Description { get; set; }
	public string? DownloadUrl { get; set; }
	public long Size { get; set; }
	public string? Checksum { get; set; }
	public ChecksumType ChecksumType { get; set; } = ChecksumType.Sha1;
	public bool IsObsolete { get; set; }
}
