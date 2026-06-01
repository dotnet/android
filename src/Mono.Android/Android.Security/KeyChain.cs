#nullable enable
using System;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Android.Security
{
	public partial class KeyChain
	{
		/// <summary>
		/// Retrieves an <see cref="X509Certificate2"/> with its associated private key from the Android system KeyChain.
		/// </summary>
		/// <param name="context">The Android context used to access the KeyChain.</param>
		/// <param name="alias">The alias of the private key and certificate chain to retrieve.</param>
		/// <returns>
		/// An <see cref="X509Certificate2"/> containing the certificate and private key,
		/// or <see langword="null"/> if the private key or certificate chain is not available for the given alias.
		/// </returns>
		/// <remarks>
		/// This method combines <see cref="KeyChain.GetPrivateKey"/> and <see cref="KeyChain.GetCertificateChain"/>
		/// into a single call that returns a .NET <see cref="X509Certificate2"/> suitable for use with
		/// <see cref="System.Net.Http.HttpClientHandler.ClientCertificates"/> or other TLS APIs.
		/// </remarks>
		public static X509Certificate2? GetX509Certificate2WithPrivateKey (Android.Content.Context context, string alias)
		{
			var privateKey = KeyChain.GetPrivateKey (context, alias);
			if (privateKey is null) {
				return null;
			}

			var chain = KeyChain.GetCertificateChain (context, alias);
			if (chain is null) {
				return null;
			}

			var privateKeyEntry = new Java.Security.KeyStore.PrivateKeyEntry (privateKey, chain);
			var certificate = new X509Certificate2 (privateKeyEntry.Handle);
			GC.KeepAlive (privateKeyEntry);
			return certificate;
		}

		/// <summary>
		/// Displays the system UI for the user to select a private key alias, filtering by URI.
		/// </summary>
		/// <param name="activity">The activity to use as the parent for the certificate selection UI.</param>
		/// <param name="keyTypes">The acceptable types of asymmetric keys, or <see langword="null"/> to allow any type.</param>
		/// <param name="issuers">The acceptable certificate issuers for the certificate matching the private key, or <see langword="null"/> to allow any issuer.</param>
		/// <param name="uri">The URI to filter by, or <see langword="null"/> to allow the user to choose any alias.</param>
		/// <param name="alias">The initial alias to preselect if available, or <see langword="null"/> for no preselection.</param>
		/// <returns>
		/// A <see cref="Task{TResult}"/> representing the asynchronous operation. The result is the alias chosen by the user,
		/// or <see langword="null"/> if the user cancelled the selection.
		/// </returns>
		/// <remarks>
		/// This is an async wrapper around <see cref="KeyChain.ChoosePrivateKeyAlias(Android.App.Activity, IKeyChainAliasCallback, string[], Java.Security.IPrincipal[], Android.Net.Uri, string)"/>.
		/// This overload requires Android API 23 or later.
		/// </remarks>
		[SupportedOSPlatform("android23.0")]
		public static async Task<string?> ChoosePrivateKeyAliasAsync (
			Android.App.Activity activity,
			string[]? keyTypes,
			Java.Security.IPrincipal[]? issuers,
			Android.Net.Uri? uri,
			string? alias)
		{
			var tcs = new TaskCompletionSource<string?> ();
			KeyChain.ChoosePrivateKeyAlias (activity, new KeyChainAliasCallback(tcs), keyTypes, issuers, uri, alias);
			return await tcs.Task;
		}

		/// <summary>
		/// Displays the system UI for the user to select a private key alias, filtering by host and port.
		/// </summary>
		/// <param name="activity">The activity to use as the parent for the certificate selection UI.</param>
		/// <param name="keyTypes">The acceptable types of asymmetric keys, or <see langword="null"/> to allow any type.</param>
		/// <param name="issuers">The acceptable certificate issuers for the certificate matching the private key, or <see langword="null"/> to allow any issuer.</param>
		/// <param name="host">The host name of the server requesting the certificate, or <see langword="null"/> for no host filtering.</param>
		/// <param name="port">The port number of the server requesting the certificate, or <c>-1</c> if unavailable.</param>
		/// <param name="alias">The initial alias to preselect if available, or <see langword="null"/> for no preselection.</param>
		/// <returns>
		/// A <see cref="Task{TResult}"/> representing the asynchronous operation. The result is the alias chosen by the user,
		/// or <see langword="null"/> if the user cancelled the selection.
		/// </returns>
		/// <remarks>
		/// This is an async wrapper around <see cref="KeyChain.ChoosePrivateKeyAlias(Android.App.Activity, IKeyChainAliasCallback, string[], Java.Security.IPrincipal[], string, int, string)"/>.
		/// </remarks>
		public static async Task<string?> ChoosePrivateKeyAliasAsync (
			Android.App.Activity activity,
			string[]? keyTypes,
			Java.Security.IPrincipal[]? issuers,
			string? host,
			int port,
			string? alias)
		{
			var tcs = new TaskCompletionSource<string?> ();
			KeyChain.ChoosePrivateKeyAlias (activity, new KeyChainAliasCallback(tcs), keyTypes, issuers, host, port, alias);
			return await tcs.Task;
		}

		/// <summary>
		/// Displays the system UI for the user to select a certificate, then retrieves the corresponding
		/// <see cref="X509Certificate2"/> with its private key, filtering by URI.
		/// </summary>
		/// <param name="activity">The activity to use as the parent for the certificate selection UI.</param>
		/// <param name="keyTypes">The acceptable types of asymmetric keys, or <see langword="null"/> to allow any type.</param>
		/// <param name="issuers">The acceptable certificate issuers for the certificate matching the private key, or <see langword="null"/> to allow any issuer.</param>
		/// <param name="uri">The URI to filter by, or <see langword="null"/> to allow the user to choose any alias.</param>
		/// <param name="alias">The initial alias to preselect if available, or <see langword="null"/> for no preselection.</param>
		/// <returns>
		/// A <see cref="Task{TResult}"/> representing the asynchronous operation. The result is an <see cref="X509Certificate2"/>
		/// containing the certificate and private key, or <see langword="null"/> if the user cancelled the selection
		/// or the certificate could not be retrieved.
		/// </returns>
		/// <remarks>
		/// This method combines <see cref="ChoosePrivateKeyAliasAsync(Android.App.Activity, string[], Java.Security.IPrincipal[], Android.Net.Uri, string)"/>
		/// and <see cref="GetX509Certificate2WithPrivateKey"/> into a single call for a one-step TLS client certificate workflow.
		/// This overload requires Android API 23 or later.
		/// </remarks>
		[SupportedOSPlatform("android23.0")]
		public static async Task<X509Certificate2?> ChooseX509Certificate2WithPrivateKeyAsync (
			Android.App.Activity activity,
			string[]? keyTypes,
			Java.Security.IPrincipal[]? issuers,
			Android.Net.Uri? uri,
			string? alias)
		{
			alias = await ChoosePrivateKeyAliasAsync (activity, keyTypes, issuers, uri, alias);
			if (alias is null) {
				return null;
			}

			return GetX509Certificate2WithPrivateKey (activity, alias);
		}

		/// <summary>
		/// Displays the system UI for the user to select a certificate, then retrieves the corresponding
		/// <see cref="X509Certificate2"/> with its private key, filtering by host and port.
		/// </summary>
		/// <param name="activity">The activity to use as the parent for the certificate selection UI.</param>
		/// <param name="keyTypes">The acceptable types of asymmetric keys, or <see langword="null"/> to allow any type.</param>
		/// <param name="issuers">The acceptable certificate issuers for the certificate matching the private key, or <see langword="null"/> to allow any issuer.</param>
		/// <param name="host">The host name of the server requesting the certificate, or <see langword="null"/> for no host filtering.</param>
		/// <param name="port">The port number of the server requesting the certificate, or <c>-1</c> if unavailable.</param>
		/// <param name="alias">The initial alias to preselect if available, or <see langword="null"/> for no preselection.</param>
		/// <returns>
		/// A <see cref="Task{TResult}"/> representing the asynchronous operation. The result is an <see cref="X509Certificate2"/>
		/// containing the certificate and private key, or <see langword="null"/> if the user cancelled the selection
		/// or the certificate could not be retrieved.
		/// </returns>
		/// <remarks>
		/// This method combines <see cref="ChoosePrivateKeyAliasAsync(Android.App.Activity, string[], Java.Security.IPrincipal[], string, int, string)"/>
		/// and <see cref="GetX509Certificate2WithPrivateKey"/> into a single call for a one-step TLS client certificate workflow.
		/// </remarks>
		public static async Task<X509Certificate2?> ChooseX509Certificate2WithPrivateKeyAsync (
			Android.App.Activity activity,
			string[]? keyTypes,
			Java.Security.IPrincipal[]? issuers,
			string? host,
			int port,
			string? alias)
		{
			alias = await ChoosePrivateKeyAliasAsync (activity, keyTypes, issuers, host, port, alias);
			if (alias is null) {
				return null;
			}

			return GetX509Certificate2WithPrivateKey (activity, alias);
		}

		private sealed class KeyChainAliasCallback(TaskCompletionSource<string?> tcs)
			: Java.Lang.Object, IKeyChainAliasCallback
		{
			public void Alias (string? alias) => tcs.SetResult (alias);
		}
	}
}
