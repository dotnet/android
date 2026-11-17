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

using System;
using System.IO.Hashing;

namespace Java.Interop.Tools.JavaCallableWrappers
{
	/// <summary>
	///  CRC64 variant: crc-64-jones 64-bit
	///    * Poly: 0xad93d23594c935a9
	///
	///  Changes beyond initial implementation:
	///    * Starting Value: ulong.MaxValue
	///    * XOR length in HashFinal()
	///    * System.IO.Hashing implementation with custom Jones parameters
	/// </summary>
	internal sealed class Crc64Helper
	{
		static readonly Crc64ParameterSet parameters = Crc64ParameterSet.Create (
			polynomial: 0xad93d23594c935a9UL,
			initialValue: ulong.MaxValue,
			finalXorValue: 0,
			reflectValues: true);

		readonly System.IO.Hashing.Crc64 crc = new System.IO.Hashing.Crc64 (parameters);
		ulong length;

		internal void Initialize ()
		{
			crc.Reset ();
			length = 0;
		}

		internal void HashCoreJones (byte [] array, int ibStart, int cbSize)
		{
			if (array == null)
				throw new ArgumentNullException (nameof (array));
			if (ibStart < 0 || ibStart > array.Length)
				throw new ArgumentOutOfRangeException (nameof (ibStart));
			if (cbSize < 0 || cbSize > array.Length - ibStart)
				throw new ArgumentOutOfRangeException (nameof (cbSize));

			crc.Append (array.AsSpan (ibStart, cbSize));
			length += (ulong) cbSize;
		}

		internal byte [] GetCurrentHash () => BitConverter.GetBytes (crc.GetCurrentHashAsUInt64 () ^ length);

		internal static byte [] ComputeJones (byte [] array)
		{
			if (array == null)
				throw new ArgumentNullException (nameof (array));
			return BitConverter.GetBytes (HashToUInt64Jones (array));
		}

		internal static ulong HashToUInt64Jones (ReadOnlySpan<byte> input) =>
			System.IO.Hashing.Crc64.HashToUInt64 (parameters, input) ^ (ulong) input.Length;
	}
}
