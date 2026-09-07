using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;

namespace Xamarin.Android.Tools.Benchmarks;

[MemoryDiagnoser]
[InProcess]
[WarmupCount (3)]
[IterationCount (3)]
public class RegisteredPeersAddBenchmarks
{
	Peer[] peers = [];

	[Params (1_000)]
	public int PeerCount { get; set; }

	[GlobalSetup]
	public void Setup ()
	{
		peers = Peer.CreateUnique (PeerCount);
	}

	[Benchmark (Baseline = true)]
	public int AlwaysList ()
	{
		var registered = new AlwaysListRegisteredPeers (PeerCount);
		for (int i = 0; i < peers.Length; i++) {
			registered.Add (peers [i]);
		}
		return registered.Count;
	}

	[Benchmark]
	public int FirstRest ()
	{
		var registered = new FirstRestRegisteredPeers (PeerCount);
		for (int i = 0; i < peers.Length; i++) {
			registered.Add (peers [i]);
		}
		return registered.Count;
	}

	[Benchmark]
	public int ObjectOrList ()
	{
		var registered = new ObjectOrListRegisteredPeers (PeerCount);
		for (int i = 0; i < peers.Length; i++) {
			registered.Add (peers [i]);
		}
		return registered.Count;
	}

	[Benchmark]
	public int InlineFirstRest ()
	{
		var registered = new InlineFirstRestRegisteredPeers (PeerCount);
		for (int i = 0; i < peers.Length; i++) {
			registered.Add (peers [i]);
		}
		return registered.Count;
	}
}

[MemoryDiagnoser]
[InProcess]
[WarmupCount (3)]
[IterationCount (3)]
public class RegisteredPeersPeekBenchmarks
{
	const int OperationsPerInvoke = 1_000;

	AlwaysListRegisteredPeers alwaysList = new (0);
	FirstRestRegisteredPeers firstRest = new (0);
	ObjectOrListRegisteredPeers objectOrList = new (0);
	InlineFirstRestRegisteredPeers inlineFirstRest = new (0);
	Peer[] expected = [];

	[Params (1, 4)]
	public int PeersPerHash { get; set; }

	[GlobalSetup]
	public void Setup ()
	{
		alwaysList = new AlwaysListRegisteredPeers (OperationsPerInvoke);
		firstRest = new FirstRestRegisteredPeers (OperationsPerInvoke);
		objectOrList = new ObjectOrListRegisteredPeers (OperationsPerInvoke);
		inlineFirstRest = new InlineFirstRestRegisteredPeers (OperationsPerInvoke);
		expected = new Peer [OperationsPerInvoke];

		for (int hash = 0; hash < OperationsPerInvoke; hash++) {
			for (int id = 0; id < PeersPerHash; id++) {
				var peer = new Peer (hash, id);
				alwaysList.Add (peer);
				firstRest.Add (peer);
				objectOrList.Add (peer);
				inlineFirstRest.Add (peer);
				expected [hash] = peer;
			}
		}
	}

	[Benchmark (Baseline = true, OperationsPerInvoke = OperationsPerInvoke)]
	public int AlwaysList ()
	{
		int checksum = 0;
		for (int i = 0; i < expected.Length; i++) {
			checksum += alwaysList.Peek (expected [i]).Id;
		}
		return checksum;
	}

	[Benchmark (OperationsPerInvoke = OperationsPerInvoke)]
	public int FirstRest ()
	{
		int checksum = 0;
		for (int i = 0; i < expected.Length; i++) {
			checksum += firstRest.Peek (expected [i]).Id;
		}
		return checksum;
	}

	[Benchmark (OperationsPerInvoke = OperationsPerInvoke)]
	public int ObjectOrList ()
	{
		int checksum = 0;
		for (int i = 0; i < expected.Length; i++) {
			checksum += objectOrList.Peek (expected [i]).Id;
		}
		return checksum;
	}

	[Benchmark (OperationsPerInvoke = OperationsPerInvoke)]
	public int InlineFirstRest ()
	{
		int checksum = 0;
		for (int i = 0; i < expected.Length; i++) {
			checksum += inlineFirstRest.Peek (expected [i]).Id;
		}
		return checksum;
	}
}

[MemoryDiagnoser]
[InProcess]
[WarmupCount (3)]
[IterationCount (3)]
public class RegisteredPeersChurnBenchmarks
{
	const int HashCount = 1_000;
	const int PeersPerHash = 4;
	const int OperationsPerInvoke = HashCount;

	readonly Peer[] peers = Peer.CreateCollisions (HashCount, PeersPerHash);
	readonly Peer[] removePeers = new Peer [HashCount];
	AlwaysListRegisteredPeers alwaysList = new (0);
	FirstRestRegisteredPeers firstRest = new (0);
	ObjectOrListRegisteredPeers objectOrList = new (0);
	InlineFirstRestRegisteredPeers inlineFirstRest = new (0);

	[GlobalSetup]
	public void Setup ()
	{
		alwaysList = new AlwaysListRegisteredPeers (HashCount);
		firstRest = new FirstRestRegisteredPeers (HashCount);
		objectOrList = new ObjectOrListRegisteredPeers (HashCount);
		inlineFirstRest = new InlineFirstRestRegisteredPeers (HashCount);
		AddPeers (alwaysList);
		AddPeers (firstRest);
		AddPeers (objectOrList);
		AddPeers (inlineFirstRest);
		for (int hash = 0; hash < removePeers.Length; hash++) {
			removePeers [hash] = peers [(hash * PeersPerHash) + PeersPerHash - 1];
		}
	}

	[Benchmark (Baseline = true, OperationsPerInvoke = OperationsPerInvoke)]
	public int AlwaysList ()
	{
		for (int i = 0; i < removePeers.Length; i++) {
			alwaysList.Remove (removePeers [i]);
			alwaysList.Add (removePeers [i]);
		}
		return alwaysList.Count;
	}

	[Benchmark (OperationsPerInvoke = OperationsPerInvoke)]
	public int FirstRest ()
	{
		for (int i = 0; i < removePeers.Length; i++) {
			firstRest.Remove (removePeers [i]);
			firstRest.Add (removePeers [i]);
		}
		return firstRest.Count;
	}

	[Benchmark (OperationsPerInvoke = OperationsPerInvoke)]
	public int ObjectOrList ()
	{
		for (int i = 0; i < removePeers.Length; i++) {
			objectOrList.Remove (removePeers [i]);
			objectOrList.Add (removePeers [i]);
		}
		return objectOrList.Count;
	}

	[Benchmark (OperationsPerInvoke = OperationsPerInvoke)]
	public int InlineFirstRest ()
	{
		for (int i = 0; i < removePeers.Length; i++) {
			inlineFirstRest.Remove (removePeers [i]);
			inlineFirstRest.Add (removePeers [i]);
		}
		return inlineFirstRest.Count;
	}

	void AddPeers (IRegisteredPeers registered)
	{
		for (int i = 0; i < peers.Length; i++) {
			registered.Add (peers [i]);
		}
	}
}

interface IRegisteredPeers
{
	int Count { get; }

	void Add (Peer peer);

	Peer Peek (Peer expected);

	void Remove (Peer peer);
}

sealed class AlwaysListRegisteredPeers : IRegisteredPeers
{
	readonly Dictionary<int, List<ReferenceTrackingHandle>> peers;

	public AlwaysListRegisteredPeers (int capacity)
	{
		peers = new Dictionary<int, List<ReferenceTrackingHandle>> (capacity);
	}

	public int Count => peers.Count;

	public void Add (Peer peer)
	{
		if (!peers.TryGetValue (peer.Hash, out List<ReferenceTrackingHandle> values)) {
			values = [new ReferenceTrackingHandle (peer)];
			peers.Add (peer.Hash, values);
			return;
		}

		values.Add (new ReferenceTrackingHandle (peer));
	}

	public Peer Peek (Peer expected)
	{
		if (!peers.TryGetValue (expected.Hash, out List<ReferenceTrackingHandle> values))
			return null;

		for (int i = values.Count - 1; i >= 0; i--) {
			Peer candidate = values [i].Target;
			if (candidate.Id == expected.Id)
				return candidate;
		}
		return null;
	}

	public void Remove (Peer peer)
	{
		if (!peers.TryGetValue (peer.Hash, out List<ReferenceTrackingHandle> values))
			return;

		for (int i = values.Count - 1; i >= 0; i--) {
			if (ReferenceEquals (values [i].Target, peer)) {
				values.RemoveAt (i);
			}
		}
		if (values.Count == 0)
			peers.Remove (peer.Hash);
	}
}

sealed class FirstRestRegisteredPeers : IRegisteredPeers
{
	readonly Dictionary<int, FirstRestBucket> peers;

	public FirstRestRegisteredPeers (int capacity)
	{
		peers = new Dictionary<int, FirstRestBucket> (capacity);
	}

	public int Count => peers.Count;

	public void Add (Peer peer)
	{
		if (!peers.TryGetValue (peer.Hash, out FirstRestBucket values)) {
			peers.Add (peer.Hash, new FirstRestBucket (peer));
			return;
		}

		values.Add (peer);
	}

	public Peer Peek (Peer expected)
	{
		if (!peers.TryGetValue (expected.Hash, out FirstRestBucket values))
			return null;

		for (int i = values.Count - 1; i >= 0; i--) {
			Peer candidate = values [i].Target;
			if (candidate.Id == expected.Id)
				return candidate;
		}
		return null;
	}

	public void Remove (Peer peer)
	{
		if (!peers.TryGetValue (peer.Hash, out FirstRestBucket values))
			return;

		for (int i = values.Count - 1; i >= 0; i--) {
			if (ReferenceEquals (values [i].Target, peer)) {
				values.RemoveAt (i);
			}
		}
		if (values.Count == 0)
			peers.Remove (peer.Hash);
	}
}

sealed class FirstRestBucket
{
	ReferenceTrackingHandle first;
	List<ReferenceTrackingHandle> rest;
	bool hasFirst;

	public FirstRestBucket (Peer peer)
	{
		first = new ReferenceTrackingHandle (peer);
		hasFirst = true;
	}

	public int Count => (hasFirst ? 1 : 0) + (rest?.Count ?? 0);

	public ReferenceTrackingHandle this [int index] {
		get => index == 0 ? first : rest [index - 1];
	}

	public void Add (Peer peer)
	{
		if (!hasFirst) {
			first = new ReferenceTrackingHandle (peer);
			hasFirst = true;
			return;
		}

		rest ??= [];
		rest.Add (new ReferenceTrackingHandle (peer));
	}

	public void RemoveAt (int index)
	{
		if (index == 0) {
			hasFirst = false;
			if (rest?.Count > 0) {
				first = rest [0];
				hasFirst = true;
				rest.RemoveAt (0);
			}
		} else {
			rest?.RemoveAt (index - 1);
		}

		if (rest?.Count == 0)
			rest = null;
	}
}

sealed class ObjectOrListRegisteredPeers : IRegisteredPeers
{
	readonly Dictionary<int, object> peers;

	public ObjectOrListRegisteredPeers (int capacity)
	{
		peers = new Dictionary<int, object> (capacity);
	}

	public int Count => peers.Count;

	public void Add (Peer peer)
	{
		var handle = new ReferenceTrackingHandle (peer);
		if (!peers.TryGetValue (peer.Hash, out object values)) {
			peers.Add (peer.Hash, handle);
			return;
		}

		if (values is ReferenceTrackingHandle first) {
			peers [peer.Hash] = new List<ReferenceTrackingHandle> { first, handle };
			return;
		}

		((List<ReferenceTrackingHandle>) values).Add (handle);
	}

	public Peer Peek (Peer expected)
	{
		if (!peers.TryGetValue (expected.Hash, out object values))
			return null;

		if (values is ReferenceTrackingHandle single)
			return single.Target.Id == expected.Id ? single.Target : null;

		var list = (List<ReferenceTrackingHandle>) values;
		for (int i = list.Count - 1; i >= 0; i--) {
			Peer candidate = list [i].Target;
			if (candidate.Id == expected.Id)
				return candidate;
		}
		return null;
	}

	public void Remove (Peer peer)
	{
		if (!peers.TryGetValue (peer.Hash, out object values))
			return;

		if (values is ReferenceTrackingHandle single) {
			if (ReferenceEquals (single.Target, peer))
				peers.Remove (peer.Hash);
			return;
		}

		var list = (List<ReferenceTrackingHandle>) values;
		for (int i = list.Count - 1; i >= 0; i--) {
			if (ReferenceEquals (list [i].Target, peer)) {
				list.RemoveAt (i);
			}
		}
		if (list.Count == 0) {
			peers.Remove (peer.Hash);
		}
	}
}

sealed class InlineFirstRestRegisteredPeers : IRegisteredPeers
{
	readonly Dictionary<int, InlineFirstRestBucket> peers;

	public InlineFirstRestRegisteredPeers (int capacity)
	{
		peers = new Dictionary<int, InlineFirstRestBucket> (capacity);
	}

	public int Count => peers.Count;

	public void Add (Peer peer)
	{
		if (!peers.TryGetValue (peer.Hash, out InlineFirstRestBucket values)) {
			peers.Add (peer.Hash, new InlineFirstRestBucket (peer));
			return;
		}

		values.Add (peer);
		peers [peer.Hash] = values;
	}

	public Peer Peek (Peer expected)
	{
		if (!peers.TryGetValue (expected.Hash, out InlineFirstRestBucket values))
			return null;

		for (int i = values.Count - 1; i >= 0; i--) {
			Peer candidate = values [i].Target;
			if (candidate.Id == expected.Id)
				return candidate;
		}
		return null;
	}

	public void Remove (Peer peer)
	{
		if (!peers.TryGetValue (peer.Hash, out InlineFirstRestBucket values))
			return;

		for (int i = values.Count - 1; i >= 0; i--) {
			if (ReferenceEquals (values [i].Target, peer)) {
				values.RemoveAt (i);
			}
		}
		if (values.Count == 0)
			peers.Remove (peer.Hash);
		else
			peers [peer.Hash] = values;
	}
}

struct InlineFirstRestBucket
{
	ReferenceTrackingHandle first;
	List<ReferenceTrackingHandle> rest;

	public InlineFirstRestBucket (Peer peer)
	{
		first = new ReferenceTrackingHandle (peer);
		rest = null;
	}

	public int Count => (first.IsValid ? 1 : 0) + (rest?.Count ?? 0);

	public ReferenceTrackingHandle this [int index] {
		get => index == 0 ? first : rest [index - 1];
	}

	public void Add (Peer peer)
	{
		if (!first.IsValid) {
			first = new ReferenceTrackingHandle (peer);
			return;
		}

		rest ??= [];
		rest.Add (new ReferenceTrackingHandle (peer));
	}

	public void RemoveAt (int index)
	{
		if (index == 0) {
			first = default;
			if (rest?.Count > 0) {
				first = rest [0];
				rest.RemoveAt (0);
			}
		} else {
			rest?.RemoveAt (index - 1);
		}

		if (rest?.Count == 0)
			rest = null;
	}
}

// Matches the two managed pointer-sized fields in the runtime's ReferenceTrackingHandle.
readonly struct ReferenceTrackingHandle
{
	public ReferenceTrackingHandle (Peer target)
	{
		Target = target;
		Context = (nint) target.Id;
	}

	public Peer Target { get; }

	public nint Context { get; }

	public bool IsValid => Target != null;
}

sealed class Peer
{
	public Peer (int hash, int id)
	{
		Hash = hash;
		Id = id;
	}

	public int Hash { get; }

	public int Id { get; }

	public static Peer[] CreateUnique (int count)
	{
		var peers = new Peer [count];
		for (int i = 0; i < peers.Length; i++) {
			peers [i] = new Peer (i, i);
		}
		return peers;
	}

	public static Peer[] CreateCollisions (int hashCount, int peersPerHash)
	{
		var peers = new Peer [hashCount * peersPerHash];
		for (int hash = 0; hash < hashCount; hash++) {
			for (int id = 0; id < peersPerHash; id++) {
				peers [(hash * peersPerHash) + id] = new Peer (hash, id);
			}
		}
		return peers;
	}
}
