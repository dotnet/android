using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Android.Runtime;

using Xunit;

using ExternalEntry = Microsoft.Android.Sdk.TrimmableTypeMap.PrecompiledTypeMapBlobWriter.ExternalEntry;
using ProxyEntry = Microsoft.Android.Sdk.TrimmableTypeMap.PrecompiledTypeMapBlobWriter.ProxyEntry;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class PrecompiledTypeMapBlobTests
{
	static byte[] Write (IEnumerable<ExternalEntry>? external = null, IEnumerable<ProxyEntry>? proxy = null) =>
		PrecompiledTypeMapBlobWriter.Write (
			(external ?? Enumerable.Empty<ExternalEntry> ()).ToList (),
			(proxy ?? Enumerable.Empty<ProxyEntry> ()).ToList ());

	static int[] ReadExternalTokens (byte[] blob, string jniName)
	{
		if (!PrecompiledTypeMapBlobFormat.TryGetExternalTokens (blob, jniName, out int count, out int offset)) {
			return Array.Empty<int> ();
		}
		var tokens = new int [count];
		for (int i = 0; i < count; i++) {
			tokens [i] = PrecompiledTypeMapBlobFormat.ReadTokenAt (blob, offset, i);
		}
		return tokens;
	}

	[Fact]
	public void EmptyBlob_IsValid_AndFindsNothing ()
	{
		byte[] blob = Write ();

		Assert.True (PrecompiledTypeMapBlobFormat.IsValid (blob));
		Assert.False (PrecompiledTypeMapBlobFormat.TryGetExternalTokens (blob, "android/app/Activity", out _, out _));
		Assert.False (PrecompiledTypeMapBlobFormat.TryGetProxyToken (blob, "Android.App.Activity, Mono.Android", out _));
	}

	[Fact]
	public void SingleExternalAndProxy_RoundTrips ()
	{
		byte[] blob = Write (
			external: new [] { new ExternalEntry ("android/app/Activity", new [] { 0x01000005 }) },
			proxy: new [] { new ProxyEntry ("Android.App.Activity, Mono.Android", 0x01000005) });

		Assert.Equal (new [] { 0x01000005 }, ReadExternalTokens (blob, "android/app/Activity"));

		Assert.True (PrecompiledTypeMapBlobFormat.TryGetProxyToken (blob, "Android.App.Activity, Mono.Android", out int token));
		Assert.Equal (0x01000005, token);
	}

	[Fact]
	public void UnknownKeys_ReturnFalse ()
	{
		byte[] blob = Write (
			external: new [] { new ExternalEntry ("android/app/Activity", new [] { 0x01000005 }) },
			proxy: new [] { new ProxyEntry ("Android.App.Activity, Mono.Android", 0x01000005) });

		Assert.Empty (ReadExternalTokens (blob, "android/widget/Button"));
		Assert.False (PrecompiledTypeMapBlobFormat.TryGetProxyToken (blob, "Android.Widget.Button, Mono.Android", out _));
	}

	[Fact]
	public void MultipleProxiesPerJniName_RoundTrip_Aliases ()
	{
		byte[] blob = Write (
			external: new [] {
				new ExternalEntry ("java/util/Collection", new [] { 0x01000010, 0x01000011 }),
				new ExternalEntry ("android/app/Activity", new [] { 0x01000005 }),
			});

		Assert.Equal (new [] { 0x01000010, 0x01000011 }, ReadExternalTokens (blob, "java/util/Collection"));
		Assert.Equal (new [] { 0x01000005 }, ReadExternalTokens (blob, "android/app/Activity"));
	}

	[Fact]
	public void ManyEntries_AllRoundTrip ()
	{
		var external = new List<ExternalEntry> ();
		var proxy = new List<ProxyEntry> ();
		for (int i = 0; i < 500; i++) {
			external.Add (new ExternalEntry ($"java/pkg/Type{i}", new [] { 0x01000000 + i }));
			proxy.Add (new ProxyEntry ($"Java.Pkg.Type{i}, MyAssembly{i % 7}", 0x02000000 + i));
		}

		byte[] blob = Write (external, proxy);

		for (int i = 0; i < 500; i++) {
			Assert.Equal (new [] { 0x01000000 + i }, ReadExternalTokens (blob, $"java/pkg/Type{i}"));
			Assert.True (PrecompiledTypeMapBlobFormat.TryGetProxyToken (blob, $"Java.Pkg.Type{i}, MyAssembly{i % 7}", out int token));
			Assert.Equal (0x02000000 + i, token);
		}

		// A key that was never inserted must not match.
		Assert.Empty (ReadExternalTokens (blob, "java/pkg/Type500"));
	}

	[Fact]
	public void DuplicateStringKeys_AreInterned_ProducingSmallerBlob ()
	{
		// The same managed key appears as both an external value target string and proxy key here to
		// exercise string interning across the two maps; identical strings must be stored once.
		byte[] shared = Write (
			external: new [] { new ExternalEntry ("android/app/Activity", new [] { 1 }) },
			proxy: new [] { new ProxyEntry ("android/app/Activity", 1) });

		byte[] distinct = Write (
			external: new [] { new ExternalEntry ("android/app/Activity", new [] { 1 }) },
			proxy: new [] { new ProxyEntry ("android/widget/Button", 1) });

		Assert.True (shared.Length < distinct.Length);
	}

	[Fact]
	public void HashCollision_IsResolvedByKeyVerification ()
	{
		// Two distinct keys whose XxHash3 collides would otherwise be ambiguous; the reader must scan
		// the equal-hash run and verify keys. We can't easily force a real collision, so instead assert
		// that keys sharing a hash prefix (common OrderBy path) all resolve to their own token.
		var external = Enumerable.Range (0, 50)
			.Select (i => new ExternalEntry ($"k{i}", new [] { 1000 + i }))
			.ToList ();
		byte[] blob = Write (external, Array.Empty<ProxyEntry> ());

		foreach (var e in external) {
			Assert.Equal (new [] { e.ProxyTokens [0] }, ReadExternalTokens (blob, e.JniName));
		}
	}

	[Fact]
	public void NegativeTokens_RoundTrip ()
	{
		// Metadata tokens are stored as raw int32; ensure the high bit survives.
		byte[] blob = Write (
			proxy: new [] { new ProxyEntry ("X, Y", unchecked ((int) 0xFF000001)) });

		Assert.True (PrecompiledTypeMapBlobFormat.TryGetProxyToken (blob, "X, Y", out int token));
		Assert.Equal (unchecked ((int) 0xFF000001), token);
	}

	[Fact]
	public void Utf8SpanOverload_MatchesStringOverload_External ()
	{
		byte[] blob = Write (
			external: new [] {
				new ExternalEntry ("android/app/Activity", new [] { 0x0100000A }),
				new ExternalEntry ("java/util/Collection", new [] { 0x0100000B, 0x0100000C }),
			});

		foreach (var jni in new [] { "android/app/Activity", "java/util/Collection", "not/present" }) {
			bool byString = PrecompiledTypeMapBlobFormat.TryGetExternalTokens (blob, jni, out int c1, out int o1);
			bool byUtf8 = PrecompiledTypeMapBlobFormat.TryGetExternalTokens (blob, (ReadOnlySpan<byte>) System.Text.Encoding.UTF8.GetBytes (jni), out int c2, out int o2);
			Assert.Equal (byString, byUtf8);
			Assert.Equal (c1, c2);
			Assert.Equal (o1, o2);
		}
	}

	[Fact]
	public void Utf8SpanOverload_MatchesStringOverload_Proxy ()
	{
		byte[] blob = Write (
			proxy: new [] {
				new ProxyEntry ("Android.App.Activity, Mono.Android", 0x02000001),
				new ProxyEntry ("Android.Widget.Button, Mono.Android", 0x02000002),
			});

		foreach (var key in new [] { "Android.App.Activity, Mono.Android", "Android.Widget.Button, Mono.Android", "Nope, Nope" }) {
			bool byString = PrecompiledTypeMapBlobFormat.TryGetProxyToken (blob, key, out int t1);
			bool byUtf8 = PrecompiledTypeMapBlobFormat.TryGetProxyToken (blob, (ReadOnlySpan<byte>) System.Text.Encoding.UTF8.GetBytes (key), out int t2);
			bool byChars = PrecompiledTypeMapBlobFormat.TryGetProxyToken (blob, key.AsSpan (), out int t3);
			Assert.Equal (byString, byUtf8);
			Assert.Equal (byString, byChars);
			Assert.Equal (t1, t2);
			Assert.Equal (t1, t3);
		}
	}

	[Fact]
	public void CharSpanOverload_HandlesSlicedKey_WithoutSubstring ()
	{
		// TryGetProxyType slices the simplified AQN out of a full AQN; verify the sliced ReadOnlySpan<char>
		// (not a standalone string) resolves identically.
		byte[] blob = Write (
			proxy: new [] { new ProxyEntry ("Android.App.Activity, Mono.Android", 0x02000009) });

		string fullAqn = "Android.App.Activity, Mono.Android, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
		int secondComma = fullAqn.IndexOf (',', fullAqn.IndexOf (',') + 1);
		ReadOnlySpan<char> sliced = fullAqn.AsSpan (0, secondComma);

		Assert.True (PrecompiledTypeMapBlobFormat.TryGetProxyToken (blob, sliced, out int token));
		Assert.Equal (0x02000009, token);
	}

	[Fact]
	public void LongKey_ExceedingStackBuffer_StillRoundTrips ()
	{
		// A key whose UTF-8 form exceeds the stack buffer exercises the heap-fallback encode path.
		string longKey = new string ('k', 4096);
		byte[] blob = Write (proxy: new [] { new ProxyEntry (longKey, 0x02000123) });

		Assert.True (PrecompiledTypeMapBlobFormat.TryGetProxyToken (blob, longKey, out int token));
		Assert.Equal (0x02000123, token);
		Assert.False (PrecompiledTypeMapBlobFormat.TryGetProxyToken (blob, new string ('k', 4095), out _));
	}
}
