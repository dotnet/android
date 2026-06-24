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

		[DllImport (ZstdLibrary)]
		static extern UIntPtr ZSTD_compressBound (UIntPtr srcSize);

		[DllImport (ZstdLibrary)]
		static extern IntPtr ZSTD_createCCtx ();

		[DllImport (ZstdLibrary)]
		static extern UIntPtr ZSTD_freeCCtx (IntPtr cctx);

		[DllImport (ZstdLibrary)]
		static extern UIntPtr ZSTD_CCtx_setParameter (IntPtr cctx, int param, int value);

		[DllImport (ZstdLibrary)]
		static extern UIntPtr ZSTD_compress2 (IntPtr cctx, byte[] dst, UIntPtr dstCapacity, byte[] src, UIntPtr srcSize);

		[DllImport (ZstdLibrary)]
		static extern uint ZSTD_isError (UIntPtr code);

		[DllImport (ZstdLibrary)]
		static extern int ZSTD_maxCLevel ();

		/// <summary>
		/// Returns the maximum size that compressed data of <paramref name="inputSize"/> bytes can occupy.
		/// </summary>
		public static int MaximumOutputSize (int inputSize)
		{
			return checked ((int) (ulong) ZSTD_compressBound ((UIntPtr) (uint) inputSize));
		}

		/// <summary>
		/// Compresses <paramref name="inputLength"/> bytes from <paramref name="input"/> into
		/// <paramref name="output"/> using the maximum compression level. Returns the number of
		/// bytes written to <paramref name="output"/>, or <c>-1</c> if compression failed.
		/// </summary>
		public static int Compress (byte[] input, int inputLength, byte[] output)
		{
			IntPtr cctx = ZSTD_createCCtx ();
			if (cctx == IntPtr.Zero)
				return -1;

			try {
				ZSTD_CCtx_setParameter (cctx, ZSTD_c_compressionLevel, ZSTD_maxCLevel ());

				UIntPtr result = ZSTD_compress2 (cctx, output, (UIntPtr) (uint) output.Length, input, (UIntPtr) (uint) inputLength);
				if (ZSTD_isError (result) != 0)
					return -1;

				return checked ((int) (ulong) result);
			} finally {
				ZSTD_freeCCtx (cctx);
			}
		}
	}
}
