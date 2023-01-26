//
// CryptoConvert.cs - Crypto Conversion Routines
//
// Author:
//	Sebastien Pouliot  <sebastien@ximian.com>
//
// (C) 2003 Motus Technologies Inc. (http://www.motus.com)
// Copyright (C) 2004-2006 Novell Inc. (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// Original source can be found at
// https://github.com/mono/mono/blob/e2c5f4b0ad1a6b21ca0735f0b35b8611d4ad87b3/mcs/class/Mono.Security/Mono.Security.Cryptography/CryptoConvert.cs#L4

using System;
using System.Security.Cryptography;

namespace Mono.Security.Cryptography
{
  internal static class CryptoConvert
  {
    static private int ToInt32LE(byte[] bytes, int offset)
    {
      return (bytes[offset + 3] << 24) | (bytes[offset + 2] << 16) | (bytes[offset + 1] << 8) | bytes[offset];
    }

    static private uint ToUInt32LE(byte[] bytes, int offset)
    {
      return (uint)((bytes[offset + 3] << 24) | (bytes[offset + 2] << 16) | (bytes[offset + 1] << 8) | bytes[offset]);
    }

    static private byte[] GetBytesLE(int val)
    {
      return new byte[] {
        (byte) (val & 0xff),
        (byte) ((val >> 8) & 0xff),
        (byte) ((val >> 16) & 0xff),
        (byte) ((val >> 24) & 0xff)
      };
    }

    static private byte[] Trim(byte[] array)
    {
      for (int i = 0; i < array.Length; i++)
      {
        if (array[i] != 0x00)
        {
          byte[] result = new byte[array.Length - i];
          Buffer.BlockCopy(array, i, result, 0, result.Length);
          return result;
        }
      }

#pragma warning disable S1168 // Empty arrays and collections should be returned instead of null
      return null;
#pragma warning restore S1168 // Empty arrays and collections should be returned instead of null
    }

    private static RSA FromCapiPrivateKeyBlob(byte[] blob, int offset)
    {
      var rsap = new RSAParameters();

      try
      {
        if ((blob[offset] != 0x07) ||       // PRIVATEKEYBLOB (0x07)
            (blob[offset + 1] != 0x02) ||       // Version (0x02)
            (blob[offset + 2] != 0x00) ||       // Reserved (word)
            (blob[offset + 3] != 0x00) ||
            (ToUInt32LE(blob, offset + 8) != 0x32415352)) // DWORD magic = RSA2
        {
          throw new CryptographicException("Invalid blob header");
        }

        //// ALGID (CALG_RSA_SIGN, CALG_RSA_KEYX, ...)
        //// int algId = ToInt32LE (blob, offset+4);

        //// DWORD bitlen
        int bitLen = ToInt32LE(blob, offset + 12);

        //// DWORD public exponent
        byte[] exp = new byte[4];
        Buffer.BlockCopy(blob, offset + 16, exp, 0, 4);
        Array.Reverse(exp);
        rsap.Exponent = Trim(exp);

        int pos = offset + 20;
        //// BYTE modulus[rsapubkey.bitlen/8];
        int byteLen = (bitLen >> 3);
        rsap.Modulus = new byte[byteLen];
        Buffer.BlockCopy(blob, pos, rsap.Modulus, 0, byteLen);
        Array.Reverse(rsap.Modulus);
        pos += byteLen;

        //// BYTE prime1[rsapubkey.bitlen/16];
        int byteHalfLen = (byteLen >> 1);
        rsap.P = new byte[byteHalfLen];
        Buffer.BlockCopy(blob, pos, rsap.P, 0, byteHalfLen);
        Array.Reverse(rsap.P);
        pos += byteHalfLen;

        //// BYTE prime2[rsapubkey.bitlen/16];
        rsap.Q = new byte[byteHalfLen];
        Buffer.BlockCopy(blob, pos, rsap.Q, 0, byteHalfLen);
        Array.Reverse(rsap.Q);
        pos += byteHalfLen;

        //// BYTE exponent1[rsapubkey.bitlen/16];
        rsap.DP = new byte[byteHalfLen];
        Buffer.BlockCopy(blob, pos, rsap.DP, 0, byteHalfLen);
        Array.Reverse(rsap.DP);
        pos += byteHalfLen;

        //// BYTE exponent2[rsapubkey.bitlen/16];
        rsap.DQ = new byte[byteHalfLen];
        Buffer.BlockCopy(blob, pos, rsap.DQ, 0, byteHalfLen);
        Array.Reverse(rsap.DQ);
        pos += byteHalfLen;

        //// BYTE coefficient[rsapubkey.bitlen/16];
        rsap.InverseQ = new byte[byteHalfLen];
        Buffer.BlockCopy(blob, pos, rsap.InverseQ, 0, byteHalfLen);
        Array.Reverse(rsap.InverseQ);
        pos += byteHalfLen;

        // ok, this is hackish but CryptoAPI support it so...
        // note: only works because CRT is used by default
        // http://bugzilla.ximian.com/show_bug.cgi?id=57941
        rsap.D = new byte[byteLen]; // must be allocated
        if (pos + byteLen + offset <= blob.Length)
        {
          //// BYTE privateExponent[rsapubkey.bitlen/8];
          Buffer.BlockCopy(blob, pos, rsap.D, 0, byteLen);
          Array.Reverse(rsap.D);
        }
      }
      catch (Exception e)
      {
        throw new CryptographicException("Invalid blob.", e);
      }

      RSA rsa = null;
      try
      {
        rsa = RSA.Create();
        rsa.ImportParameters(rsap);
      }
      catch (CryptographicException)
      {
        // this may cause problem when this code is run under
        // the SYSTEM identity on Windows (e.g. ASP.NET). See
        // http://bugzilla.ximian.com/show_bug.cgi?id=77559
        bool throws = false;
        try
        {
          var csp = new CspParameters
          {
            Flags = CspProviderFlags.UseMachineKeyStore
          };

#pragma warning disable S4426 // Cryptographic keys should be robust
          rsa = new RSACryptoServiceProvider(csp);
#pragma warning restore S4426 // Cryptographic keys should be robust
          rsa.ImportParameters(rsap);
        }
        catch
        {
          throws = true;
        }

        if (throws)
        {
          // rethrow original, not the latter, exception if this fails
          throw;
        }
      }
      return rsa;
    }

    private static RSA FromCapiPublicKeyBlob(byte[] blob, int offset)
    {
      try
      {
        if ((blob[offset] != 0x06) ||       // PUBLICKEYBLOB (0x06)
            (blob[offset + 1] != 0x02) ||       // Version (0x02)
            (blob[offset + 2] != 0x00) ||       // Reserved (word)
            (blob[offset + 3] != 0x00) ||
            (ToUInt32LE(blob, offset + 8) != 0x31415352)) // DWORD magic = RSA1
        {
          throw new CryptographicException("Invalid blob header");
        }

        //// ALGID (CALG_RSA_SIGN, CALG_RSA_KEYX, ...)
        //// int algId = ToInt32LE (blob, offset+4);

        // DWORD bitlen
        int bitLen = ToInt32LE(blob, offset + 12);

        //// DWORD public exponent
        var rsap = new RSAParameters
        {
          Exponent = new byte[3]
        };

        rsap.Exponent[0] = blob[offset + 18];
        rsap.Exponent[1] = blob[offset + 17];
        rsap.Exponent[2] = blob[offset + 16];

        int pos = offset + 20;
        //// BYTE modulus[rsapubkey.bitlen/8];
        int byteLen = bitLen >> 3;
        rsap.Modulus = new byte[byteLen];
        Buffer.BlockCopy(blob, pos, rsap.Modulus, 0, byteLen);
        Array.Reverse(rsap.Modulus);

        RSA rsa = null;
        try
        {
          rsa = RSA.Create();
          rsa.ImportParameters(rsap);
        }
        catch (CryptographicException)
        {
          // this may cause problem when this code is run under
          // the SYSTEM identity on Windows (e.g. ASP.NET). See
          // http://bugzilla.ximian.com/show_bug.cgi?id=77559
          var csp = new CspParameters
          {
            Flags = CspProviderFlags.UseMachineKeyStore
          };

#pragma warning disable S4426 // Cryptographic keys should be robust
          rsa = new RSACryptoServiceProvider(csp);
#pragma warning restore S4426 // Cryptographic keys should be robust
          rsa.ImportParameters(rsap);
        }

        return rsa;
      }
      catch (Exception e)
      {
        throw new CryptographicException("Invalid blob.", e);
      }
    }

    // PRIVATEKEYBLOB
    // PUBLICKEYBLOB
    static public RSA FromCapiKeyBlob(byte[] blob)
    {
      return FromCapiKeyBlob(blob, 0);
    }

    static public RSA FromCapiKeyBlob(byte[] blob, int offset)
    {
      if (blob == null)
      {
        throw new ArgumentNullException(nameof(blob));
      }

      if (offset >= blob.Length)
      {
        throw new ArgumentException("blob is too small.");
      }

      switch (blob[offset])
      {
        case 0x00:
          // this could be a public key inside an header
          // like "sn -e" would produce
          if (blob[offset + 12] == 0x06)
          {
            return FromCapiPublicKeyBlob(blob, offset + 12);
          }
          break;
        case 0x06:
          return FromCapiPublicKeyBlob(blob, offset);
        case 0x07:
          return FromCapiPrivateKeyBlob(blob, offset);
      }
      throw new CryptographicException("Unknown blob format.");
    }

    static public byte[] ToCapiPublicKeyBlob(RSA rsa)
    {
      var p = rsa.ExportParameters(false);
      int keyLength = p.Modulus.Length; // in bytes
      byte[] blob = new byte[20 + keyLength];

      blob[0] = 0x06; // Type - PUBLICKEYBLOB (0x06)
      blob[1] = 0x02; // Version - Always CUR_BLOB_VERSION (0x02)
                      // [2], [3]		// RESERVED - Always 0
      blob[5] = 0x24; // ALGID - Always 00 24 00 00 (for CALG_RSA_SIGN)
      blob[8] = 0x52; // Magic - RSA1 (ASCII in hex)
      blob[9] = 0x53;
      blob[10] = 0x41;
      blob[11] = 0x31;

      byte[] bitlen = GetBytesLE(keyLength << 3);
      blob[12] = bitlen[0]; // bitlen
      blob[13] = bitlen[1];
      blob[14] = bitlen[2];
      blob[15] = bitlen[3];

      // public exponent (DWORD)
      int pos = 16;
      int n = p.Exponent.Length;
      while (n > 0)
      {
        blob[pos++] = p.Exponent[--n];
      }

      // modulus
      pos = 20;
      byte[] part = p.Modulus;
      int len = part.Length;
      Array.Reverse(part, 0, len);
      Buffer.BlockCopy(part, 0, blob, pos, len);

      return blob;
    }
  }
}
