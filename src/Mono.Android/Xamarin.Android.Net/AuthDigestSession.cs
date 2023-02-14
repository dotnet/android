//
// Adapted from:
//
// System.Net.DigestClient.cs
//
// Authors:
//      Greg Reinacker (gregr@rassoc.com)
//      Sebastien Pouliot (spouliot@motus.com)
//      Gonzalo Paniagua Javier (gonzalo@ximian.com
//
// Copyright 2002-2003 Greg Reinacker, Reinacker & Associates, Inc. All rights reserved.
// Portions (C) 2003 Motus Technologies Inc. (http://www.motus.com)
// (c) 2003 Novell, Inc. (http://www.novell.com)
//
// Original (server-side) source code available at
// http://www.rassoc.com/gregr/weblog/stories/2002/07/09/webServicesSecurityHttpDigestAuthenticationWithoutActiveDirectory.html
//
using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;

using Java.Net;

namespace Xamarin.Android.Net
{
	sealed class AuthDigestSession
	{
		static readonly RandomNumberGenerator rng = RandomNumberGenerator.Create ();

		DateTime lastUse = DateTime.UtcNow;
		int _nc = 1;
		HashAlgorithm? hash;
		AuthDigestHeaderParser? parser;
		string? _cnonce;

		public string? Algorithm {
			get { return parser?.Algorithm; }
		}

		public string? Realm {
			get { return parser?.Realm; }
		}

		public string? Nonce {
			get { return parser?.Nonce; }
		}

		public string? Opaque {
			get { return parser?.Opaque; }
		}

		public string? QOP {
			get { return parser?.QOP; }
		}

		public string CNonce {
			get {
				if (_cnonce == null) {
					// 15 is a multiple of 3 which is better for base64 because it
					// wont end with '=' and risk messing up the server parsing
					var bincnonce = new byte [15];
					rng.GetBytes (bincnonce);
					_cnonce = Convert.ToBase64String (bincnonce);
					Array.Clear (bincnonce, 0, bincnonce.Length);
				}
				return _cnonce;
			}
		}

		public DateTime LastUse {
			get { return lastUse; }
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms", Justification = "Only supported algorithm by RFC2617.")]
		public bool Parse (string challenge)
		{
			parser = new AuthDigestHeaderParser (challenge);
			if (!parser.Parse ())
				return false;

			// build the hash object (only MD5 is defined in RFC2617)
			if ((parser.Algorithm == null) || (parser.Algorithm.StartsWith ("MD5", StringComparison.OrdinalIgnoreCase)))
				hash = MD5.Create (); // lgtm [cs/weak-crypto] This is part of RFC2617 and we cannot change the algorithm here.

			return true;
		}

		string? HashToHexString (string toBeHashed)
		{
			if (hash == null)
				return null;

			hash.Initialize ();
			byte[] result = hash.ComputeHash (Encoding.ASCII.GetBytes (toBeHashed));

			var sb = new StringBuilder ();
			foreach (byte b in result)
				sb.Append (b.ToString ("x2"));
			return sb.ToString ();
		}

		string? HA1 (string username, string password)
		{
			string ha1 = $"{username}:{Realm}:{password}";
			if (String.Compare (Algorithm, "md5-sess", StringComparison.OrdinalIgnoreCase) == 0)
				ha1 = $"{HashToHexString (ha1)}:{Nonce}:{CNonce}";
			return HashToHexString (ha1);
		}

		string? HA2 (HttpURLConnection webRequest)
		{
			var uri = new Uri (webRequest.URL?.ToString ()!);
			string ha2 = $"{webRequest.RequestMethod}:{uri.PathAndQuery}";
			if (QOP == "auth-int") {
				// TODO
				// ha2 += String.Format (":{0}", hentity);
			}
			return HashToHexString (ha2);
		}

		string? Response (string username, string password, HttpURLConnection webRequest)
		{
			string response = $"{HA1 (username, password)}:{Nonce}:";
			if (QOP != null)
				response += $"{_nc.ToString ("X8")}:{CNonce}:{QOP}:";
			response += HA2 (webRequest);
			return HashToHexString (response);
		}

		public Authorization? Authenticate (HttpURLConnection request, ICredentials credentials)
		{
			if (parser == null)
				throw new InvalidOperationException ();
			if (request == null)
				return null;

			lastUse = DateTime.Now;
			var uri = new Uri (request.URL?.ToString ()!);
			NetworkCredential cred = credentials.GetCredential (uri, "digest");
			if (cred == null)
				return null;

			string userName = cred.UserName;
			if (String.IsNullOrEmpty (userName))
				return null;

			string password = cred.Password;
			var auth = new StringBuilder ();
			auth.Append ($"Digest username=\"{userName}\", ");
			auth.Append ($"realm=\"{Realm}\", ");
			auth.Append ($"nonce=\"{Nonce}\", ");
			auth.Append ($"uri=\"{uri.PathAndQuery}\", ");

			if (Algorithm != null) { // hash algorithm (only MD5 in RFC2617)
				auth.Append ($"algorithm=\"{Algorithm}\", ");
			}

			auth.Append ($"response=\"{Response (userName, password, request)}\", ");

			if (QOP != null) { // quality of protection (server decision)
				auth.Append ($"qop=\"{QOP}\", ");
			}

			lock (this) {
				// _nc MUST NOT change from here...
				// number of request using this nonce
				if (QOP != null) {
					auth.Append ($"nc={_nc.ToString ("X8")}, ");
					_nc++;
				}
				// until here, now _nc can change
			}

			if (CNonce != null) // opaque value from the client
				auth.Append ($"cnonce=\"{CNonce}\", ");

			if (Opaque != null) // exact same opaque value as received from server
				auth.Append ($"opaque=\"{Opaque}\", ");

			auth.Length -= 2; // remove ", "
			return new Authorization (auth.ToString ());
		}
	}
}
