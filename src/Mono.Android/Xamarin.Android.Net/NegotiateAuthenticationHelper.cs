using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Net
{
	// This code is heavily inspired by System.Net.Http.AuthenticationHelper
	internal static class NegotiateAuthenticationHelper
	{
		const int MaxRequests = 10;

		internal class RequestedNegotiateAuthenticationData
		{
			public required string AuthType { get; init; }
			public bool IsProxyAuth { get; init; }
			public required NetworkCredential Credential { get; init; }
		}

		internal static bool RequestNeedsNegotiateAuthentication (
			AndroidMessageHandler handler,
			HttpRequestMessage request,
			[NotNullWhen (true)] out RequestedNegotiateAuthenticationData? requestedAuth)
		{
			requestedAuth = null;

			IEnumerable<AuthenticationData> authenticationData = handler.RequestedAuthentication ?? Array.Empty<AuthenticationData> ();
			foreach (var auth in authenticationData) {
				if (TryGetSupportedAuthType (auth.Challenge, out var authType)) {
					var credentials = auth.UseProxyAuthentication ? handler.Proxy?.Credentials : handler.Credentials;
					var correspondingCredential = credentials?.GetCredential (request.RequestUri, authType);

					if (correspondingCredential != null) {
						requestedAuth = new RequestedNegotiateAuthenticationData {
							IsProxyAuth = auth.UseProxyAuthentication,
							AuthType = authType,
							Credential = correspondingCredential
						};

						return true;
					}
				}
			}

			return false;
		}

		internal static async Task <HttpResponseMessage?> SendWithAuthAsync (
			AndroidMessageHandler handler,
			HttpRequestMessage request,
			RequestedNegotiateAuthenticationData requestedAuth,
			CancellationToken cancellationToken)
		{
			using var authContext = new NegotiateAuthentication (
				new NegotiateAuthenticationClientOptions {
					Package = requestedAuth.AuthType,
					Credential = requestedAuth.Credential,
					TargetName = await GetTargetName (handler, request, requestedAuth.IsProxyAuth, cancellationToken).ConfigureAwait (false),
					RequiredProtectionLevel = requestedAuth.IsProxyAuth ? ProtectionLevel.None : ProtectionLevel.Sign,
				}
			);

			// we need to make sure that the handler doesn't override the authorization header
			// with the user defined pre-authentication data
			var originalPreAuthenticate = handler.PreAuthenticate;
			handler.PreAuthenticate = false;

			try {
				return await DoSendWithAuthAsync (handler, request, authContext, requestedAuth, cancellationToken);
			} finally {
				handler.PreAuthenticate = originalPreAuthenticate;
			}
		}

		static bool TryGetSupportedAuthType (string challenge, out string authType)
		{
			var spaceIndex = challenge.IndexOf (' ');
			authType = spaceIndex == -1 ? challenge : challenge.Substring (0, spaceIndex);

			return authType.Equals ("NTLM", StringComparison.OrdinalIgnoreCase) ||
				authType.Equals ("Negotiate", StringComparison.OrdinalIgnoreCase);
		}

		static async Task <HttpResponseMessage?> DoSendWithAuthAsync (
			AndroidMessageHandler handler,
			HttpRequestMessage request,
			NegotiateAuthentication authContext,
			RequestedNegotiateAuthenticationData requestedAuth,
			CancellationToken cancellationToken)
		{
			HttpResponseMessage? response = null;

			int requestCounter = 0;
			string? challengeData = null;

			while (requestCounter++ < MaxRequests) {
				var challengeResponse = authContext.GetOutgoingBlob (challengeData, out var statusCode);

				if (challengeResponse is null || statusCode > NegotiateAuthenticationStatusCode.ContinueNeeded) {
					// Response indicated denial even after login, so stop processing and return current response.
					break;
				}

				if (response is not null) {
					// We need to drain the content otherwise the next request
					// won't reuse the same TCP socket and persistent auth won't work.
					await response.Content.LoadIntoBufferAsync ().ConfigureAwait (false);
				}

				SetAuthorizationHeader (request, requestedAuth, challengeResponse);
				response = await handler.DoSendAsync (request, cancellationToken).ConfigureAwait (false);

				if (authContext.IsAuthenticated || !TryGetChallenge (response, requestedAuth, out challengeData)) {
					break;
				}

				if (!IsAuthenticationChallenge (response, requestedAuth))
				{
					// Tail response for Negotiate on successful authentication. Validate it before we proceed.
					authContext.GetOutgoingBlob(challengeData, out statusCode);
					if (statusCode > NegotiateAuthenticationStatusCode.ContinueNeeded)
					{
						throw new HttpRequestException($"Authentication validation failed with error - {statusCode}.", null, HttpStatusCode.Unauthorized);
					}
					break;
				}
			}

			return response;
		}

		static async Task<string> GetTargetName (
			AndroidMessageHandler handler,
			HttpRequestMessage request,
			bool isProxyAuth,
			CancellationToken cancellationToken)
		{
			var hostName = await GetHostName (handler, request, isProxyAuth, cancellationToken);
			return $"HTTP/{hostName}";
		}

		static async Task<string> GetHostName (
			AndroidMessageHandler handler,
			HttpRequestMessage request,
			bool isProxyAuth,
			CancellationToken cancellationToken)
		{
			// Calculate SPN (Service Principal Name) using the host name of the request.
			// Use the request's 'Host' header if available. Otherwise, use the request uri.
			// Ignore the 'Host' header if this is proxy authentication since we need to use
			// the host name of the proxy itself for SPN calculation.
			if (!isProxyAuth && request.Headers.Host != null) {
				// Use the host name without any normalization.
				return request.Headers.Host;
			}

			var requestUri = request.RequestUri!;
			var authUri = isProxyAuth ? handler.Proxy?.GetProxy (requestUri) ?? requestUri : requestUri;

			// Need to use FQDN normalized host so that CNAME's are traversed.
			// Use DNS to do the forward lookup to an A (host) record.
			// But skip DNS lookup on IP literals. Otherwise, we would end up
			// doing an unintended reverse DNS lookup.
			if (authUri.HostNameType == UriHostNameType.IPv6 || authUri.HostNameType == UriHostNameType.IPv4) {
				return authUri.IdnHost;
			} else {
				IPHostEntry result = await Dns.GetHostEntryAsync (authUri.IdnHost, cancellationToken).ConfigureAwait (false);
				return result.HostName;
			}
		}

		static void SetAuthorizationHeader (HttpRequestMessage request, RequestedNegotiateAuthenticationData requestedAuth, string challengeResponse)
		{
			var headerValue = new AuthenticationHeaderValue (requestedAuth.AuthType, challengeResponse);
			if (requestedAuth.IsProxyAuth) {
				request.Headers.ProxyAuthorization = headerValue;
			} else {
				request.Headers.Authorization = headerValue;
			}
		}

		static bool TryGetChallenge (HttpResponseMessage? response, RequestedNegotiateAuthenticationData requestedAuth, [NotNullWhen (true)] out string? challengeData)
		{
			challengeData = null;

			var responseHeaderValues = requestedAuth.IsProxyAuth ? response?.Headers.ProxyAuthenticate : response?.Headers.WwwAuthenticate;
			if (responseHeaderValues is not null) {
				foreach (var headerValue in responseHeaderValues) {
					if (headerValue.Scheme == requestedAuth.AuthType) {
						challengeData = headerValue.Parameter;
						break;
					}
				}
			}

			return !string.IsNullOrEmpty (challengeData);
		}

		static bool IsAuthenticationChallenge (HttpResponseMessage response, RequestedNegotiateAuthenticationData requestedAuth)
			=> requestedAuth.IsProxyAuth
				? response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired
				: response.StatusCode == HttpStatusCode.Unauthorized;
	}
}
