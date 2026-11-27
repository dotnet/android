// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;

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
	[Obsolete ("Use System.IO.Hashing.Crc64. Preserving legacy hashes requires CRC-64-Jones parameters and XORing the total input length into the result.")]
	public partial class Crc64 : HashAlgorithm
	{
		readonly Crc64Helper crc = new Crc64Helper ();

		public override void Initialize () => crc.Initialize ();

		protected override void HashCore (byte [] array, int ibStart, int cbSize) => crc.HashCoreJones (array, ibStart, cbSize);

		protected override byte [] HashFinal () => crc.GetCurrentHash ();
	}
}
