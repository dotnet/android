/*
 * Originally ported from: https://github.com/gityf/crc/blob/8045f50ba6e4193d4ee5d2539025fef26e613c9f/crc/crc64.c
 *
 * Copyright (c) 2012, Salvatore Sanfilippo <antirez at gmail dot com>
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *
 *   * Redistributions of source code must retain the above copyright notice,
 *     this list of conditions and the following disclaimer.
 *   * Redistributions in binary form must reproduce the above copyright
 *     notice, this list of conditions and the following disclaimer in the
 *     documentation and/or other materials provided with the distribution.
 *   * Neither the name of Redis nor the names of its contributors may be used
 *     to endorse or promote products derived from this software without
 *     specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE. */

// https://github.com/xamarin/java.interop/blob/7d197f17a0f9d73854522d6e1d68deafbcdbcaf6/src/Java.Interop.Tools.JavaCallableWrappers/Java.Interop.Tools.JavaCallableWrappers/Crc64Helper.cs

using System;
using System.Security.Cryptography;

namespace Microsoft.Android.Build.Tasks
{
	/// <summary>
	///  CRC64 variant: crc-64-jones 64-bit
	///    * Poly: 0xad93d23594c935a9
	///
	///  Changes beyond initial implementation:
	///    * Starting Value: ulong.MaxValue
	///    * XOR length in HashFinal()
	///    * Using spliced table for faster processing
	/// </summary>
	internal static partial class Crc64Helper
	{

		internal static byte [] Compute (byte [] array)
		{
			ulong crc = ulong.MaxValue;
			ulong length = 0;

			HashCore (array, 0, array.Length, ref crc, ref length);

			return BitConverter.GetBytes (crc ^ length);
		}

		internal static unsafe void HashCore (byte [] array, int ibStart, int cbSize, ref ulong crc, ref ulong length)
		{
			int len = cbSize;
			int idx = ibStart;

			fixed (ulong* tptr = table) {
				fixed (byte* aptr = array) {
					while (len >= 8) {
						crc ^= *((ulong*) (aptr + idx));
						crc =
							tptr [7 * 256 + (crc & 0xff)] ^
							tptr [6 * 256 + ((crc >> 8) & 0xff)] ^
							tptr [5 * 256 + ((crc >> 16) & 0xff)] ^
							tptr [4 * 256 + ((crc >> 24) & 0xff)] ^
							tptr [3 * 256 + ((crc >> 32) & 0xff)] ^
							tptr [2 * 256 + ((crc >> 40) & 0xff)] ^
							tptr [1 * 256 + ((crc >> 48) & 0xff)] ^
							tptr [0 * 256 + (crc >> 56)];
						idx += 8;
						len -= 8;
					}

					while (len > 0) {
						crc = tptr [0 * 256 + ((crc ^ aptr [idx]) & 0xff)] ^ (crc >> 8);
						idx++;
						len--;
					}
				}
			}

			length += (ulong) cbSize;
		}
	}
}
