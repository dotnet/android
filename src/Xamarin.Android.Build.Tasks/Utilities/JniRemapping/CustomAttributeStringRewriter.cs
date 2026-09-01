#nullable enable

using System;
using System.IO;
using System.Text;

namespace Xamarin.Android.Tasks.JniRemapping
{
	/// <summary>
	/// Rewrites specific fixed string arguments within a CustomAttribute value blob
	/// (ECMA-335 II.23.3), leaving the prolog, any other fixed arguments, and every named
	/// argument byte-for-byte untouched.
	///
	/// This only supports (and only needs to support) the attributes this task rewrites -
	/// Android.Runtime.RegisterAttribute, Java.Interop.JniTypeSignatureAttribute,
	/// Java.Interop.JniMethodSignatureAttribute, Java.Interop.JniConstructorSignatureAttribute,
	/// System.Runtime.InteropServices.TypeMapAttribute, and Java.Interop.JavaPeerAliasesAttribute.
	/// </summary>
	static class CustomAttributeStringRewriter
	{
		/// <summary>
		/// Rewrites the leading fixed string arguments of a CustomAttribute value blob.
		/// <paramref name="rewriteArg"/>
		/// is invoked with the (0-based) argument index and its original value, and should return
		/// the replacement value, or null if that argument should be left unchanged.
		/// Bytes following those leading strings are copied verbatim, allowing a string prefix to
		/// be changed even when later fixed arguments have other SerString-encoded types.
		///
		/// Returns null if no argument was rewritten (i.e. the blob does not need to change).
		/// </summary>
		public static byte []? TryRewrite (byte [] originalContent, int fixedArgCount, Func<int, string?, string?> rewriteArg)
		{
			if (originalContent.Length < 2) {
				throw new JniRewriteException ("Malformed custom attribute value blob: missing 2-byte prolog.");
			}
			if (originalContent [0] != 0x01 || originalContent [1] != 0x00) {
				throw new JniRewriteException ("Malformed custom attribute value blob: expected 0x0001 prolog.");
			}

			using var ms = new MemoryStream (originalContent.Length);
			ms.Write (originalContent, 0, 2); // Prolog (0x0001), verbatim.
			int pos = 2;
			bool changed = false;

			for (int i = 0; i < fixedArgCount; i++) {
				if (pos >= originalContent.Length) {
					throw new JniRewriteException ("Malformed custom attribute value blob: ran out of bytes while reading fixed arguments.");
				}

				int argStart = pos;
				string? value;
				if (originalContent [pos] == 0xFF) {
					// A "null string" SerString is encoded as a single 0xFF byte (ECMA-335 II.23.3).
					value = null;
					pos += 1;
				} else {
					int prefixWidth = MetadataEncoding.ReadCompressedInteger (originalContent, pos, out int strByteLength);
					pos += prefixWidth + strByteLength;
					if (pos > originalContent.Length) {
						throw new JniRewriteException ("Malformed custom attribute value blob: fixed string argument extends past the end of the blob.");
					}
					value = Encoding.UTF8.GetString (originalContent, argStart + prefixWidth, strByteLength);
				}

				string? newValue = value != null ? rewriteArg (i, value) : null;
				if (newValue != null && !string.Equals (newValue, value, StringComparison.Ordinal)) {
					changed = true;
					byte [] utf8 = Encoding.UTF8.GetBytes (newValue);
					byte [] prefix = MetadataEncoding.EncodeCompressedInteger (utf8.Length);
					ms.Write (prefix, 0, prefix.Length);
					ms.Write (utf8, 0, utf8.Length);
				} else {
					ms.Write (originalContent, argStart, pos - argStart);
				}
			}

			// Remaining fixed arguments and NumNamed/NamedArg tail, copied verbatim.
			ms.Write (originalContent, pos, originalContent.Length - pos);

			return changed ? ms.ToArray () : null;
		}

		/// <summary>
		/// Rewrites the elements of the first fixed argument when it is a <c>string[]</c>.
		/// All bytes following the array are copied verbatim.
		/// </summary>
		public static byte []? TryRewriteStringArray (byte [] originalContent, Func<string, string?> rewriteElement)
		{
			if (originalContent.Length < 6) {
				throw new JniRewriteException ("Malformed custom attribute value blob: missing string array length.");
			}

			using var ms = new MemoryStream (originalContent.Length);
			ms.Write (originalContent, 0, 6); // Prolog (uint16) and array length (int32), verbatim.
			int count = BitConverter.ToInt32 (originalContent, 2);
			if (count < -1) {
				throw new JniRewriteException ($"Malformed custom attribute value blob: invalid string array length {count}.");
			}
			if (count == -1) {
				return null;
			}

			int pos = 6;
			bool changed = false;
			for (int i = 0; i < count; i++) {
				if (pos >= originalContent.Length) {
					throw new JniRewriteException ("Malformed custom attribute value blob: ran out of bytes while reading string array.");
				}

				int elementStart = pos;
				if (originalContent [pos] == 0xFF) {
					pos++;
					ms.WriteByte (0xFF);
					continue;
				}

				int prefixWidth = MetadataEncoding.ReadCompressedInteger (originalContent, pos, out int strByteLength);
				pos += prefixWidth + strByteLength;
				if (pos > originalContent.Length) {
					throw new JniRewriteException ("Malformed custom attribute value blob: string array element extends past the end of the blob.");
				}

				string value = Encoding.UTF8.GetString (originalContent, elementStart + prefixWidth, strByteLength);
				string? newValue = rewriteElement (value);
				if (newValue != null && !string.Equals (newValue, value, StringComparison.Ordinal)) {
					changed = true;
					byte [] utf8 = Encoding.UTF8.GetBytes (newValue);
					byte [] prefix = MetadataEncoding.EncodeCompressedInteger (utf8.Length);
					ms.Write (prefix, 0, prefix.Length);
					ms.Write (utf8, 0, utf8.Length);
				} else {
					ms.Write (originalContent, elementStart, pos - elementStart);
				}
			}

			ms.Write (originalContent, pos, originalContent.Length - pos);
			return changed ? ms.ToArray () : null;
		}
	}
}
