using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Authentication.ExtendedProtection;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Net
{
	// This code is heavily inspired by System.Net.Http.AuthenticationHelper
	internal static class NegotiateAuthenticationHelper
	{
		const int MaxRequests = 10;

		internal static bool RequestNeedsNegotiateAuthentication (
			AndroidMessageHandler handler,
			HttpRequestMessage request,
			[NotNullWhen (true)] out AuthenticationData? requestedAuthentication,
			[NotNullWhen (true)] out NetworkCredential? correspondingCredentials)
		{
			IEnumerable<AuthenticationData> authenticationData = handler.RequestedAuthentication ?? Enumerable.Empty<AuthenticationData> ();
			foreach (var auth in authenticationData) {
				if (TryGetSupportedAuthType (auth.Challenge, out var authType)) {
					var credentials = auth.UseProxyAuthentication ? handler.Proxy?.Credentials : handler.Credentials;
					correspondingCredentials = credentials?.GetCredential (request.RequestUri, authType);

					if (correspondingCredentials != null) {
						requestedAuthentication = auth;
						return true;
					}
				}
			}

			requestedAuthentication = null;
			correspondingCredentials = null;
			return false;
		}

		internal static async Task <HttpResponseMessage?> SendWithAuthAsync (
			AndroidMessageHandler handler,
			HttpRequestMessage request,
			AuthenticationData requestedAuthentication,
			NetworkCredential credentials,
			CancellationToken cancellationToken)
		{
			if (!TryGetSupportedAuthType (requestedAuthentication.Challenge, out var authType)) {
				return null;
			}

			var isProxyAuth = requestedAuthentication.UseProxyAuthentication;
			var spn = await GetSpn (handler, request, isProxyAuth, cancellationToken).ConfigureAwait (false);
			// TODO: replace with NegotiateAuthentication once it's available in dotnet/runtime
			var authContext = new NTAuthentication (
				isServer: false,
				authType,
				credentials,
				spn: spn,
				requestedContextFlags: GetRequestedContextFlags (isProxyAuth),
				channelBinding: null);

			// we need to make sure that the handler doesn't override the authorization header
			// with the user defined pre-authentication data
			var originalPreAuthenticate = handler.PreAuthenticate;
			handler.PreAuthenticate = false;

			try {
				return await DoSendWithAuthAsync (handler, request, authContext, authType, isProxyAuth, cancellationToken);
			} finally {
				handler.PreAuthenticate = originalPreAuthenticate;
				authContext.CloseContext ();
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
			NTAuthentication authContext,
			string authType,
			bool isProxyAuth,
			CancellationToken cancellationToken)
		{
			HttpResponseMessage? response = null;

			string? challenge = null;
			int requestCounter = 0;

			while (requestCounter++ < MaxRequests) {
				var challengeResponse = authContext.GetOutgoingBlob (challenge);
				if (challengeResponse == null) {
					// response indicated denial even after login, so stop processing
					// and return current response
					break;
				}

				var headerValue = new AuthenticationHeaderValue (authType, challengeResponse);
				if (isProxyAuth) {
					request.Headers.ProxyAuthorization = headerValue;
				} else {
					request.Headers.Authorization = headerValue;
				}

				response = await handler.DoSendAsync (request, cancellationToken).ConfigureAwait (false);

				// we need to drain the content otherwise the next request
				// won't reuse the same TCP socket and persistent auth won't work
				await response.Content.LoadIntoBufferAsync ().ConfigureAwait (false);

				if (authContext.IsCompleted || !TryGetChallenge (response, authType, isProxyAuth, out challenge)) {
					break;
				}

				if (!IsAuthenticationChallenge (response, isProxyAuth)) {
					// Tail response for Negotiate on successful authentication.
					// Validate it before we proceed.
					authContext.GetOutgoingBlob (challenge);
					break;
				}
			}

			return response;
		}

		static async Task<string> GetSpn (
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

		static int GetRequestedContextFlags (bool isProxyAuth)
		{
			// the ContextFlagsPal is internal type in dotnet/runtime and we can't
			// use it directly here so we have to use ints directly
			int contextFlags = 0x00000800; // ContextFlagsPal.Connection

			// When connecting to proxy server don't enforce the integrity to avoid
			// compatibility issues. The assumption is that the proxy server comes
			// from a trusted source.
			if (!isProxyAuth) {
				contextFlags |= 0x00010000; // ContextFlagsPal.InitIntegrity
			}

			return contextFlags;
		}

		static bool TryGetChallenge (HttpResponseMessage response, string authType, bool isProxyAuth, [NotNullWhen (true)] out string? challenge)
		{
			var responseHeaderValues = isProxyAuth ? response.Headers.ProxyAuthenticate : response.Headers.WwwAuthenticate;
			challenge = responseHeaderValues?.FirstOrDefault (headerValue => headerValue.Scheme == authType)?.Parameter;
			return !string.IsNullOrEmpty (challenge);
		}

		static bool IsAuthenticationChallenge (HttpResponseMessage response, bool isProxyAuth)
			=> isProxyAuth
				? response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired
				: response.StatusCode == HttpStatusCode.Unauthorized;

		// This class will be removed once the new NegotiateAuthentication class is available in dotnet/runtime
		private sealed class NTAuthentication
		{
			const string AssemblyName = "System.Net.Http";
			const string TypeName = "System.Net.NTAuthentication";
			const string ContextFlagsPalTypeName = "System.Net.ContextFlagsPal";

			const string ConstructorDescription = "#ctor(System.Boolean,System.String,System.Net.NetworkCredential,System.String,System.Net.ContextFlagsPal,System.Security.Authentication.ExtendedProtection.ChannelBinding)";
			const string IsCompletedPropertyName = "IsCompleted";
			const string GetOutgoingBlobMethodName = "GetOutgoingBlob";
			const string CloseContextMethodName = "CloseContext";

			const BindingFlags InstanceBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

			static Lazy<Type> s_NTAuthenticationType = new (() => FindType (TypeName, AssemblyName));
			static Lazy<ConstructorInfo> s_NTAuthenticationConstructorInfo = new (() => GetNTAuthenticationConstructor ());
			static Lazy<PropertyInfo> s_IsCompletedPropertyInfo = new (() => GetProperty (IsCompletedPropertyName));
			static Lazy<MethodInfo> s_GetOutgoingBlobMethodInfo = new (() => GetMethod (GetOutgoingBlobMethodName));
			static Lazy<MethodInfo> s_CloseContextMethodInfo = new (() => GetMethod (CloseContextMethodName));

			static Type FindType (string typeName, string assemblyName)
				=> Type.GetType ($"{typeName}, {assemblyName}", throwOnError: true)!;

			static ConstructorInfo GetNTAuthenticationConstructor ()
			{
				var contextFlagsPalType = FindType (ContextFlagsPalTypeName, AssemblyName);
				var paramTypes = new[] {
					typeof (bool),
					typeof (string),
					typeof (NetworkCredential),
					typeof (string),
					contextFlagsPalType,
					typeof (ChannelBinding)
				};

				return s_NTAuthenticationType.Value.GetConstructor (InstanceBindingFlags, paramTypes)
					?? throw new MissingMemberException (TypeName, ConstructorInfo.ConstructorName);
			}

			static PropertyInfo GetProperty (string name)
				=> s_NTAuthenticationType.Value.GetProperty (name, InstanceBindingFlags)
					?? throw new MissingMemberException (TypeName, name);

			static MethodInfo GetMethod (string name)
				=> s_NTAuthenticationType.Value.GetMethod (name, InstanceBindingFlags)
					?? throw new MissingMemberException (TypeName, name);

			object _instance;

			[DynamicDependency (ConstructorDescription, TypeName, AssemblyName)]
			internal NTAuthentication (
				bool isServer,
				string package,
				NetworkCredential credential,
				string? spn,
				int requestedContextFlags,
				ChannelBinding? channelBinding)
			{
				var constructorParams = new object?[] { isServer, package, credential, spn, requestedContextFlags, channelBinding };
				_instance = s_NTAuthenticationConstructorInfo.Value.Invoke (constructorParams);
			}

			public bool IsCompleted
				=> GetIsCompleted ();

			[DynamicDependency ($"get_{IsCompletedPropertyName}", TypeName, AssemblyName)]
			bool GetIsCompleted ()
				=> (bool)(s_IsCompletedPropertyInfo.Value.GetValue (_instance) ?? false);

			[DynamicDependency (GetOutgoingBlobMethodName, TypeName, AssemblyName)]
			public string? GetOutgoingBlob (string? incomingBlob)
				=> (string?)s_GetOutgoingBlobMethodInfo.Value.Invoke (_instance, new object?[] { incomingBlob });

			[DynamicDependency (CloseContextMethodName, TypeName, AssemblyName)]
			public void CloseContext ()
				=> s_CloseContextMethodInfo.Value.Invoke (_instance, null);
		}
	}
}
