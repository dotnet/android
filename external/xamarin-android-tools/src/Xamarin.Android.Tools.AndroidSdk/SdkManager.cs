// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net.Http;

namespace Xamarin.Android.Tools;
/// <summary>
/// Provides Android SDK bootstrap and management capabilities using the <c>sdkmanager</c> CLI.
/// </summary>
/// <remarks>
/// <para>
/// Downloads the Android command-line tools from the Xamarin Android manifest feed,
/// extracts them to <c>cmdline-tools/&lt;version&gt;/</c>, then uses the included <c>sdkmanager</c>
/// to install, uninstall, list, and update SDK packages.
/// </para>
/// <para>
/// The manifest feed URL defaults to <c>https://aka.ms/AndroidManifestFeed/d18-0</c>
/// but can be configured via the <see cref="ManifestFeedUrl"/> property.
/// </para>
/// </remarks>
public partial class SdkManager : IDisposable
{
	/// <summary>Default manifest feed URL (Xamarin/Microsoft).</summary>
	public const string DefaultManifestFeedUrl = "https://aka.ms/AndroidManifestFeed/d18-0";

	const int StdinPollDelayMs = 500;

	static readonly HttpClient httpClient = new HttpClient ();
	readonly Action<TraceLevel, string> logger;
	bool disposed;

	/// <summary>
	/// Gets or sets the manifest feed URL used to discover command-line tools.
	/// Defaults to <see cref="DefaultManifestFeedUrl"/>.
	/// </summary>
	public string ManifestFeedUrl { get; set; } = DefaultManifestFeedUrl;

	/// <summary>
	/// Gets or sets the Android SDK root path. Used to locate and invoke <c>sdkmanager</c>.
	/// </summary>
	public string? AndroidSdkPath { get; set; }

	/// <summary>
	/// Gets or sets the Java SDK (JDK) home path. Set as <c>JAVA_HOME</c> when invoking <c>sdkmanager</c>.
	/// </summary>
	public string? JavaSdkPath { get; set; }

	/// <summary>
	/// Creates a new <see cref="SdkManager"/> instance.
	/// </summary>
	/// <param name="logger">Optional logger callback. Defaults to <see cref="AndroidSdkInfo.DefaultConsoleLogger"/>.</param>
	public SdkManager (Action<TraceLevel, string>? logger = null)
	{
		this.logger = logger ?? AndroidSdkInfo.DefaultConsoleLogger;
	}

	/// <summary>
	/// Disposes the <see cref="SdkManager"/>.
	/// </summary>
	public void Dispose ()
	{
		if (disposed)
			return;
		disposed = true;
	}

	void ThrowIfDisposed ()
	{
		if (disposed)
			throw new ObjectDisposedException (nameof (SdkManager));
	}

	string RequireSdkManagerPath ()
	{
		ThrowIfDisposed ();
		return FindSdkManagerPath ()
			?? throw new InvalidOperationException ("sdkmanager not found. Run BootstrapAsync first.");
	}

	void ThrowOnSdkManagerFailure (int exitCode, string operation, string stderr)
	{
		if (exitCode == 0)
			return;
		logger (TraceLevel.Error, $"{operation} failed (exit code {exitCode}): {stderr}");
		throw new InvalidOperationException ($"{operation} failed: {stderr}");
	}
}
