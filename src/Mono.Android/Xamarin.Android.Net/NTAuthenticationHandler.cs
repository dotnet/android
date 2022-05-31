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
	internal sealed class NTAuthenticationHandler
	{
		const int MaxRequests = 10;

		internal sealed class Helper
		{
			readonly AndroidMessageHandler _handler;

			internal Helper (AndroidMessageHandler handler)
			{
				_handler = handler;
			}

			internal bool RequestNeedsNTAuthentication (HttpRequestMessage request, [NotNullWhen (true)] out NTAuthenticationHandler? ntAuthHandler)
			{
				IEnumerable<AuthenticationData> requestedAuthentication = _handler.RequestedAuthentication ?? Enumerable.Empty<AuthenticationData> ();
				foreach (var auth in requestedAuthentication) {
					if (TryGetSupportedAuthType (auth.Challenge, out var authType)) {
						var credentials = auth.UseProxyAuthentication ? _handler.Proxy?.Credentials : _handler.Credentials;
						var correspondingCredentials = credentials?.GetCredential (request.RequestUri, authType);

						if (correspondingCredentials != null) {
							ntAuthHandler = new NTAuthenticationHandler (_handler, request, authType, auth.UseProxyAuthentication, correspondingCredentials);
							return true;
						}
					}
				}

				ntAuthHandler = null;
				return false;
			}

			static bool TryGetSupportedAuthType (string challenge, out string authType)
			{
				var spaceIndex = challenge.IndexOf (' ');
				authType = spaceIndex == -1 ? challenge : challenge.Substring (0, spaceIndex);

				return authType.Equals ("NTLM", StringComparison.OrdinalIgnoreCase) ||
					authType.Equals ("Negotiate", StringComparison.OrdinalIgnoreCase);
			}
		}

		readonly AndroidMessageHandler _handler;
		readonly HttpRequestMessage _request;
		readonly string _authType;
		readonly bool _isProxyAuth;
		readonly NetworkCredential _credentials;

		private NTAuthenticationHandler (
			AndroidMessageHandler handler,
			HttpRequestMessage request,
			string authType,
			bool isProxyAuth,
			NetworkCredential credentials)
		{
			_handler = handler;
			_request = request;
			_authType = authType;
			_isProxyAuth = isProxyAuth;
			_credentials = credentials;
		}

		internal async Task <HttpResponseMessage?> ResendRequestWithAuthAsync (CancellationToken cancellationToken)
		{
			var authContext = new NTAuthentication (
				isServer: false,
				_authType,
				_credentials,
				spn: await GetSpn (cancellationToken).ConfigureAwait (false),
				requestedContextFlags: GetRequestedContextFlags (),
				channelBinding: null);

			// we need to make sure that the handler doesn't override the authorization header
			// with the user defined pre-authentication data
			var originalPreAuthenticate = _handler.PreAuthenticate;
			_handler.PreAuthenticate = false;

			try {
				return await DoResendRequestWithAuthAsync (authContext, cancellationToken);
			} finally {
				_handler.PreAuthenticate = originalPreAuthenticate;
				authContext.CloseContext ();
			}
		}

		async Task <HttpResponseMessage?> DoResendRequestWithAuthAsync (NTAuthentication authContext, CancellationToken cancellationToken)
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

				var headerValue = new AuthenticationHeaderValue (_authType, challengeResponse);
				if (_isProxyAuth) {
					_request.Headers.ProxyAuthorization = headerValue;
				} else {
					_request.Headers.Authorization = headerValue;
				}

				response = await _handler.DoSendAsync (_request, cancellationToken).ConfigureAwait (false);

				// we need to drain the content otherwise the next request
				// won't reuse the same TCP socket and persistent auth won't work
				await response.Content.LoadIntoBufferAsync ().ConfigureAwait (false);

				if (authContext.IsCompleted || !TryGetChallenge (response, out challenge)) {
					break;
				}

				if (!IsAuthenticationChallenge (response)) {
					// Tail response for Negotiate on successful authentication.
					// Validate it before we proceed.
					authContext.GetOutgoingBlob (challenge);
					break;
				}
			}

			return response;
		}

		async Task<string> GetSpn (CancellationToken cancellationToken)
		{
			var hostName = await GetHostName (cancellationToken);
			return $"HTTP/{hostName}";
		}

		async Task<string> GetHostName (CancellationToken cancellationToken)
		{
			// Calculate SPN (Service Principal Name) using the host name of the request.
			// Use the request's 'Host' header if available. Otherwise, use the request uri.
			// Ignore the 'Host' header if this is proxy authentication since we need to use
			// the host name of the proxy itself for SPN calculation.
			if (!_isProxyAuth && _request.Headers.Host != null) {
				// Use the host name without any normalization.
				return _request.Headers.Host;
			}

			var requestUri = _request.RequestUri!;
			var authUri = _isProxyAuth ? _handler.Proxy?.GetProxy (requestUri) ?? requestUri : requestUri;

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

		int GetRequestedContextFlags ()
		{
			// the ContextFlagsPal is internal type in dotnet/runtime and we can't
			// use it directly here so we have to use ints directly
			int contextFlags = 0x00000800; // ContextFlagsPal.Connection

			// When connecting to proxy server don't enforce the integrity to avoid
			// compatibility issues. The assumption is that the proxy server comes
			// from a trusted source.
			if (!_isProxyAuth) {
				contextFlags |= 0x00010000; // ContextFlagsPal.InitIntegrity
			}

			return contextFlags;
		}

		bool TryGetChallenge (HttpResponseMessage response, [NotNullWhen (true)] out string? challenge)
		{
			var responseHeaderValues = _isProxyAuth ? response.Headers.ProxyAuthenticate : response.Headers.WwwAuthenticate;
			challenge = responseHeaderValues?.FirstOrDefault (headerValue => headerValue.Scheme == _authType)?.Parameter;
			return !string.IsNullOrEmpty (challenge);
		}

		bool IsAuthenticationChallenge (HttpResponseMessage response)
			=> _isProxyAuth
				? response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired
				: response.StatusCode == HttpStatusCode.Unauthorized;

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
