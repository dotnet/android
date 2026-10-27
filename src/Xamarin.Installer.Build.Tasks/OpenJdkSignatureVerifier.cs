using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Installer.Build.Tasks
{
	internal static class OpenJdkSignatureVerifier
	{
		// Source: https://learn.microsoft.com/java/openjdk/download#public-key-to-verify-signature-files.
		// Hash the decoded key ring, not the armor. Built-in key updates require a reviewed hash change.
		internal const string PublicKeyUrl = "https://download.visualstudio.microsoft.com/download/pr/b90071e2-e0cf-4411-98be-dbeb09d67bf0/8622862bcd54206e158c5abca0582c9b/464279_464280_aoc_20210208.asc";
		internal const string PublicKeySha256 = "055050CF6D100B4BDD040F92F81A313C09D2D62EC172ADF889C21DABEE992889";

		// Only the RFC 4880 v4 binary-document RSA signatures used by Microsoft OpenJDK are supported.
		internal static void Verify (Stream archive, byte [] signature, byte [] publicKey, string trustedKeySha256, CancellationToken cancellationToken, ArrayPool<byte> bufferPool = null)
		{
			cancellationToken.ThrowIfCancellationRequested ();
			var keyBytes = DecodeArmor (publicKey, "PUBLIC KEY BLOCK");
			using (var sha256 = SHA256.Create ()) {
				if (!string.Equals (HexUtilities.ToHexString (sha256.ComputeHash (keyBytes)), trustedKeySha256, StringComparison.OrdinalIgnoreCase))
					throw new InvalidDataException ("The downloaded OpenJDK public key does not match the trusted SHA-256 hash.");
			}
			var key = new PacketReader (keyBytes).ReadPacket (6);
			if (key.ReadByte () != 4)
				throw new InvalidDataException ("Unsupported OpenJDK public key version.");
			key.ReadBytes (4);
			RequireRsa (key.ReadByte ());
			var modulus = key.ReadInteger ();
			var exponent = key.ReadInteger ();
			key.RequireEnd ();

			var packets = new PacketReader (DecodeArmor (signature, "SIGNATURE"));
			var packet = packets.ReadPacket (2);
			packets.RequireEnd ();
			if (packet.ReadByte () != 4 || packet.ReadByte () != 0)
				throw new InvalidDataException ("Expected a version 4 binary-document OpenJDK signature.");
			RequireRsa (packet.ReadByte ());
			HashAlgorithmName hashAlgorithm;
			switch (packet.ReadByte ()) {
				case 8: hashAlgorithm = HashAlgorithmName.SHA256; break;
				case 9: hashAlgorithm = HashAlgorithmName.SHA384; break;
				case 10: hashAlgorithm = HashAlgorithmName.SHA512; break;
				default: throw new InvalidDataException ("Unsupported OpenJDK signature hash algorithm.");
			}
			CheckSubpackets (packet.ReadSlice (packet.ReadUInt16 ()));
			int hashedLength = packet.Position;
			CheckSubpackets (packet.ReadSlice (packet.ReadUInt16 ()));
			var prefix = packet.ReadBytes (2);
			var signatureInteger = packet.ReadInteger ();
			packet.RequireEnd ();
			if (signatureInteger.Length > modulus.Length)
				throw new InvalidDataException ("The OpenJDK RSA signature is too large.");
			// The netstandard2.0 RSA APIs require exact-sized arrays, not oversized pooled buffers.
			var rsaSignature = new byte [modulus.Length];
			signatureInteger.CopyTo (rsaSignature.AsSpan (rsaSignature.Length - signatureInteger.Length));

			using (var hash = IncrementalHash.CreateHash (hashAlgorithm))
			using (var rsa = RSA.Create ()) {
				const int bufferSize = 16 * 1024;
				var pool = bufferPool ?? ArrayPool<byte>.Shared;
				var buffer = pool.Rent (bufferSize);
				try {
					int count;
					while ((count = archive.Read (buffer, 0, buffer.Length)) > 0) {
						cancellationToken.ThrowIfCancellationRequested ();
						hash.AppendData (buffer, 0, count);
					}
					// RFC 4880 section 5.2.4: hash the signed fields and the v4 length trailer.
					hash.AppendData (packet.Data, packet.Offset, hashedLength);
					buffer [0] = 4;
					buffer [1] = 255;
					BinaryPrimitives.WriteInt32BigEndian (buffer.AsSpan (2, 4), hashedLength);
					hash.AppendData (buffer, 0, 6);
					var digest = hash.GetHashAndReset ();
					cancellationToken.ThrowIfCancellationRequested ();
					rsa.ImportParameters (new RSAParameters { Modulus = modulus.ToArray (), Exponent = exponent.ToArray () });
					if (digest [0] != prefix [0] || digest [1] != prefix [1] ||
						!rsa.VerifyHash (digest, rsaSignature, hashAlgorithm, RSASignaturePadding.Pkcs1))
						throw new InvalidDataException ("The OpenJDK archive signature is invalid.");
				} finally {
					pool.Return (buffer);
				}
			}
		}

		static void RequireRsa (int algorithm)
		{
			if (algorithm != 1 && algorithm != 3)
				throw new InvalidDataException ("Only RSA OpenJDK signatures are supported.");
		}

		static void CheckSubpackets (PacketReader reader)
		{
			while (!reader.AtEnd) {
				int length = reader.ReadLength ();
				if (length == 0 || (reader.ReadByte () & 128) != 0)
					throw new InvalidDataException ("Unsupported critical OpenJDK signature subpacket.");
				reader.ReadBytes (length - 1);
			}
		}

		static byte [] DecodeArmor (byte [] data, string label)
		{
			if (data.Length > 0 && (data [0] & 128) != 0)
				return data;
			using (var reader = new StringReader (Encoding.ASCII.GetString (data))) {
				if (reader.ReadLine () != $"-----BEGIN PGP {label}-----")
					throw new InvalidDataException ("Invalid OpenJDK OpenPGP armor header.");
				string line;
				while ((line = reader.ReadLine ()) != "") {
					if (line == null || line.IndexOf (':') < 1)
						throw new InvalidDataException ("Invalid OpenJDK OpenPGP armor metadata.");
				}
				var encoded = new StringBuilder ();
				bool checksumSeen = false;
				while ((line = reader.ReadLine ()) != $"-----END PGP {label}-----") {
					if (line == null)
						throw new InvalidDataException ("Missing OpenJDK OpenPGP armor footer.");
					if (line.StartsWith ("=", StringComparison.Ordinal) && !checksumSeen) {
						if (Convert.FromBase64String (line.Substring (1)).Length != 3)
							throw new InvalidDataException ("Invalid OpenJDK OpenPGP armor checksum.");
						checksumSeen = true;
					} else {
						if (checksumSeen)
							throw new InvalidDataException ("Unexpected data after OpenJDK OpenPGP armor checksum.");
						encoded.Append (line);
					}
				}
				if (!string.IsNullOrWhiteSpace (reader.ReadToEnd ()))
					throw new InvalidDataException ("Unexpected data after OpenJDK OpenPGP armor footer.");
				return Convert.FromBase64String (encoded.ToString ());
			}
		}

		struct PacketReader
		{
			internal byte [] Data { get; }
			internal int Offset { get; }
			int Length { get; }
			internal int Position { get; private set; }
			internal bool AtEnd => Position == Length;

			internal PacketReader (byte [] data) : this (data, 0, data.Length) { }

			PacketReader (byte [] data, int offset, int length)
			{
				Data = data;
				Offset = offset;
				Length = length;
				Position = 0;
			}

			internal int ReadByte ()
			{
				if (Position == Length)
					throw new InvalidDataException ("Truncated OpenJDK OpenPGP packet.");
				return Data [Offset + Position++];
			}

			internal int ReadUInt16 () => BinaryPrimitives.ReadUInt16BigEndian (ReadBytes (2));

			int ReadUInt32 ()
			{
				uint value = BinaryPrimitives.ReadUInt32BigEndian (ReadBytes (4));
				if (value > int.MaxValue)
					throw new InvalidDataException ("The OpenJDK OpenPGP packet is too large.");
				return (int) value;
			}

			internal ReadOnlySpan<byte> ReadBytes (int count)
			{
				if (count < 0 || count > Length - Position)
					throw new InvalidDataException ("Truncated OpenJDK OpenPGP packet.");
				var result = Data.AsSpan (Offset + Position, count);
				Position += count;
				return result;
			}

			internal PacketReader ReadSlice (int count)
			{
				int offset = Offset + Position;
				ReadBytes (count);
				return new PacketReader (Data, offset, count);
			}

			internal int ReadLength ()
			{
				int first = ReadByte ();
				if (first < 192)
					return first;
				if (first < 224)
					return ((first - 192) << 8) + ReadByte () + 192;
				if (first == 255)
					return ReadUInt32 ();
				throw new InvalidDataException ("Partial OpenJDK OpenPGP packet lengths are not supported.");
			}

			internal PacketReader ReadPacket (int expectedTag)
			{
				int header = ReadByte ();
				bool newFormat = (header & 64) != 0;
				int tag = newFormat ? header & 63 : (header >> 2) & 15;
				if ((header & 128) == 0 || tag != expectedTag)
					throw new InvalidDataException ("Unexpected OpenJDK OpenPGP packet type.");
				int length;
				if (newFormat) {
					length = ReadLength ();
				} else {
					switch (header & 3) {
						case 0: length = ReadByte (); break;
						case 1: length = ReadUInt16 (); break;
						case 2: length = ReadUInt32 (); break;
						default: throw new InvalidDataException ("Indeterminate OpenJDK OpenPGP packet lengths are not supported.");
					}
				}
				return ReadSlice (length);
			}

			internal ReadOnlySpan<byte> ReadInteger ()
			{
				int bits = ReadUInt16 ();
				var value = ReadBytes ((bits + 7) / 8);
				if (bits == 0 || value [0] >> ((bits - 1) % 8) != 1)
					throw new InvalidDataException ("Invalid OpenJDK OpenPGP integer.");
				return value;
			}

			internal void RequireEnd ()
			{
				if (!AtEnd)
					throw new InvalidDataException ("Unexpected trailing OpenJDK OpenPGP data.");
			}
		}
	}
}
