#nullable enable
using System;
using System.Runtime.InteropServices;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Minimal managed wrapper around the Zstandard compression functions exported by
	/// <c>libSystem.IO.Compression.Native</c>, which ships in the .NET runtime pack.
	/// We use it to compress assemblies that are placed in the assembly store; the native
	/// runtime decompresses them at load time using the same library.
	/// </summary>
	static class Zstd
	{
		// libSystem.IO.Compression.Native exports the raw zstd entry points (no prefix).
		const string ZstdLibrary = "System.IO.Compression.Native";

		// ZSTD_cParameter value for ZSTD_c_compressionLevel (see zstd.h).
		const int ZSTD_c_compressionLevel = 100;
		const int ZstdCompressionLevel = 3;

		[DllImport (ZstdLibrary, CallingConvention = CallingConvention.Cdecl)]
		static extern UIntPtr ZSTD_compressBound (UIntPtr srcSize);

		[DllImport (ZstdLibrary, CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr ZSTD_createCCtx ();

		[DllImport (ZstdLibrary, CallingConvention = CallingConvention.Cdecl)]
		static extern UIntPtr ZSTD_freeCCtx (IntPtr cctx);

		[DllImport (ZstdLibrary, CallingConvention = CallingConvention.Cdecl)]
		static extern UIntPtr ZSTD_CCtx_setParameter (IntPtr cctx, int param, int value);

		[DllImport (ZstdLibrary, CallingConvention = CallingConvention.Cdecl)]
		static extern UIntPtr ZSTD_compress2 (IntPtr cctx, byte[] dst, UIntPtr dstCapacity, byte[] src, UIntPtr srcSize);

		[DllImport (ZstdLibrary, CallingConvention = CallingConvention.Cdecl)]
		static extern uint ZSTD_isError (UIntPtr code);

		/// <summary>
		/// Returns the maximum size that compressed data of <paramref name="inputSize"/> bytes can occupy.
		/// </summary>
		public static int MaximumOutputSize (int inputSize)
		{
			if (inputSize < 0)
				return -1;

			try {
				UIntPtr result = ZSTD_compressBound ((UIntPtr) (uint) inputSize);
				if (ZSTD_isError (result) != 0)
					return -1;

				ulong maxOutputSize = (ulong) result;
				if (maxOutputSize > int.MaxValue)
					return -1;

				return (int) maxOutputSize;
			} catch (DllNotFoundException) {
				return -1;
			} catch (EntryPointNotFoundException) {
				return -1;
			}
		}

		/// <summary>
		/// Compresses <paramref name="inputLength"/> bytes from <paramref name="input"/> into
		/// <paramref name="output"/> using zstd's default compression level. Returns the number of
		/// bytes written to <paramref name="output"/>, or <c>-1</c> if compression failed.
		/// </summary>
		public static int Compress (byte[] input, int inputLength, byte[] output)
		{
			if (input == null)
				throw new ArgumentNullException (nameof (input));
			if (output == null)
				throw new ArgumentNullException (nameof (output));
			if (inputLength < 0 || inputLength > input.Length)
				throw new ArgumentOutOfRangeException (nameof (inputLength));

			IntPtr cctx;
			try {
				cctx = ZSTD_createCCtx ();
			} catch (DllNotFoundException) {
				return -1;
			} catch (EntryPointNotFoundException) {
				return -1;
			}

			if (cctx == IntPtr.Zero)
				return -1;

			try {
				UIntPtr setParameterResult = ZSTD_CCtx_setParameter (cctx, ZSTD_c_compressionLevel, ZstdCompressionLevel);
				if (ZSTD_isError (setParameterResult) != 0)
					return -1;

				UIntPtr result = ZSTD_compress2 (cctx, output, (UIntPtr) (uint) output.Length, input, (UIntPtr) (uint) inputLength);
				if (ZSTD_isError (result) != 0)
					return -1;

				ulong encodedLength = (ulong) result;
				if (encodedLength > int.MaxValue)
					return -1;

				return (int) encodedLength;
			} catch (DllNotFoundException) {
				return -1;
			} catch (EntryPointNotFoundException) {
				return -1;
			} finally {
				try {
					ZSTD_freeCCtx (cctx);
				} catch (DllNotFoundException) {
				} catch (EntryPointNotFoundException) {
				}
			}
		}
	}
}
