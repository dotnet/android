using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using Android.Runtime;

using Java.Net;

namespace Xamarin.Android.Net
{
	sealed class AuthModuleDigest : IAndroidAuthenticationModule
	{
		const string LOG_APP = AndroidMessageHandler.LOG_APP + "-digest-auth";

		static readonly object cache_lock = new object ();
		static readonly Dictionary <int, AuthDigestSession> cache = new Dictionary <int, AuthDigestSession> ();

		public AuthenticationScheme Scheme { get; } = AuthenticationScheme.Digest;
		public string AuthenticationType { get; } = "Digest";
		public bool CanPreAuthenticate { get; } = true;
		
		static Dictionary <int, AuthDigestSession> Cache {
			get {
				lock (cache_lock) {
					CheckExpired (cache.Count);
				}
				
				return cache;
			}
		}

		static void CheckExpired (int count)
		{
			if (count < 10)
				return;

			DateTime t = DateTime.MaxValue;
			DateTime now = DateTime.Now;
			List <int>? list = null;
			foreach (KeyValuePair <int, AuthDigestSession> kvp in cache) {
				AuthDigestSession elem = kvp.Value;
				if (elem.LastUse < t && (elem.LastUse - now).Ticks > TimeSpan.TicksPerMinute * 10) {
					t = elem.LastUse;
					if (list == null)
						list = new List <int> ();

					list.Add (kvp.Key);
				}
			}

			if (list != null) {
				foreach (int k in list)
					cache.Remove (k);
			}
		}
		
		public Authorization? Authenticate (string challenge, HttpURLConnection request, ICredentials credentials) 
		{
			if (credentials == null || challenge == null) {
				Logger.Log (LogLevel.Info, LOG_APP, "No credentials or no challenge");
				return null;
			}
	
			string header = challenge.Trim ();
			if (header.IndexOf ("digest", StringComparison.OrdinalIgnoreCase) == -1) {
				Logger.Log (LogLevel.Info, LOG_APP, "Not a digest auth request");
				return null;
			}

			var currDS = new AuthDigestSession();
			if (!currDS.Parse (challenge)) {
				Logger.Log (LogLevel.Info, LOG_APP, "Unable to parse challenge");
				return null;
			}

			var uri = new Uri (request.URL?.ToString ()!);
			int hashcode = uri.GetHashCode () ^ credentials.GetHashCode () ^ (currDS.Nonce?.GetHashCode () ?? 1);
			AuthDigestSession? ds = null;
			bool addDS = false;
			if (!Cache.TryGetValue (hashcode, out ds) || ds == null)
				addDS = true;

			if (addDS)
				ds = currDS;
			else if (!ds!.Parse (challenge)) {
				Logger.Log (LogLevel.Info, LOG_APP, "Current DS failed to parse the challenge");
				return null;
			}

			if (addDS)
				Cache.Add (hashcode, ds);

			return ds.Authenticate (request, credentials);
		}

		public Authorization? PreAuthenticate (HttpURLConnection request, ICredentials credentials) 
		{
			if (request == null || credentials == null) {
				Logger.Log (LogLevel.Info, LOG_APP, "No credentials or no challenge");
				return null;
			}

			var uri = new Uri (request.URL?.ToString ()!);
			int hashcode = uri.GetHashCode () ^ credentials.GetHashCode ();
			AuthDigestSession? ds = null;
			if (!Cache.TryGetValue (hashcode, out ds) || ds == null)
				return null;

			return ds.Authenticate (request, credentials);
		}
	}
}
