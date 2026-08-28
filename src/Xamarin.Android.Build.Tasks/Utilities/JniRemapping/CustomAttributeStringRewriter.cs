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
	/// Java.Interop.JniMethodSignatureAttribute, and Java.Interop.JniConstructorSignatureAttribute
	/// - whose constructors take only <c>string</c> fixed arguments.
	/// </summary>
	static class CustomAttributeStringRewriter
	{
		/// <summary>
		/// Rewrites the fixed string arguments of a CustomAttribute value blob whose constructor
		/// takes exactly <paramref name="fixedArgCount"/> string arguments. <paramref name="rewriteArg"/>
		/// is invoked with the (0-based) argument index and its original value, and should return
		/// the replacement value, or null if that argument should be left unchanged.
		///
		/// Returns null if no argument was rewritten (i.e. the blob does not need to change).
		/// </summary>
		public static byte []? TryRewrite (byte [] originalContent, int fixedArgCount, Func<int, string?, string?> rewriteArg)
		{
			if (originalContent.Length < 2) {
				throw new JniRewriteException ("Malformed custom attribute value blob: missing 2-byte prolog.");
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

			// Tail: NumNamed (uint16, not compressed) + NamedArg*, copied verbatim.
			ms.Write (originalContent, pos, originalContent.Length - pos);

			return changed ? ms.ToArray () : null;
		}
	}
}
