// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
public class AdbClientTests
{
	[Test]
	public async Task ReadLengthPrefixedStringFromStreamAsync_ValidPayload ()
	{
		var payload = "emulator-5554\tdevice\n";
		var hex = payload.Length.ToString ("x4");
		var data = Encoding.ASCII.GetBytes (hex + payload);
		using var stream = new MemoryStream (data);

		var result = await AdbClient.ReadLengthPrefixedStringFromStreamAsync (stream, CancellationToken.None);
		Assert.AreEqual (payload, result);
	}

	[Test]
	public async Task ReadLengthPrefixedStringFromStreamAsync_EmptyPayload ()
	{
		var data = Encoding.ASCII.GetBytes ("0000");
		using var stream = new MemoryStream (data);

		var result = await AdbClient.ReadLengthPrefixedStringFromStreamAsync (stream, CancellationToken.None);
		Assert.AreEqual (string.Empty, result);
	}

	[Test]
	public async Task ReadLengthPrefixedStringFromStreamAsync_EndOfStream_ReturnsNull ()
	{
		using var stream = new MemoryStream (Array.Empty<byte> ());

		var result = await AdbClient.ReadLengthPrefixedStringFromStreamAsync (stream, CancellationToken.None);
		Assert.IsNull (result);
	}

	[Test]
	public async Task ReadLengthPrefixedStringFromStreamAsync_MultipleDevices ()
	{
		var payload =
			"0A041FDD400327\tdevice product:redfin model:Pixel_5 device:redfin transport_id:2\n" +
			"emulator-5554\tdevice product:sdk_gphone64_x86_64 model:sdk_gphone64_x86_64 device:emu64xa transport_id:1\n";
		var hex = payload.Length.ToString ("x4");
		var data = Encoding.ASCII.GetBytes (hex + payload);
		using var stream = new MemoryStream (data);

		var result = await AdbClient.ReadLengthPrefixedStringFromStreamAsync (stream, CancellationToken.None);
		Assert.IsNotNull (result);

		var devices = AdbRunner.ParseAdbDevicesOutput (result!.Split ('\n'));
		Assert.AreEqual (2, devices.Count);
		Assert.AreEqual ("0A041FDD400327", devices [0].Serial);
		Assert.AreEqual ("emulator-5554", devices [1].Serial);
	}

	[Test]
	public void ReadLengthPrefixedStringFromStreamAsync_InvalidHex_ThrowsFormatException ()
	{
		var data = Encoding.ASCII.GetBytes ("ZZZZ");
		using var stream = new MemoryStream (data);

		Assert.ThrowsAsync<FormatException> (
			() => AdbClient.ReadLengthPrefixedStringFromStreamAsync (stream, CancellationToken.None));
	}

	[Test]
	public void ReadLengthPrefixedStringFromStreamAsync_TruncatedPayload_ThrowsIOException ()
	{
		// Header says 100 bytes but only 5 are present
		var data = Encoding.ASCII.GetBytes ("0064hello");
		using var stream = new MemoryStream (data);

		Assert.ThrowsAsync<IOException> (
			() => AdbClient.ReadLengthPrefixedStringFromStreamAsync (stream, CancellationToken.None));
	}
}
