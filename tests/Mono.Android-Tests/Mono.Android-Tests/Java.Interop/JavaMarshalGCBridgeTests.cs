using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Java;
using System.Threading;
using Android.Runtime;
using Java.Interop;
using Microsoft.Android.Runtime;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests;

[TestFixture]
public class JavaMarshalGCBridgeTests
{
	// A static class cannot be used as the first parameter's type.
	[UnsafeAccessor (UnsafeAccessorKind.StaticMethod, Name = nameof (JavaMarshalGCBridge.RequiresTemporaryPeer))]
	static extern bool RequiresTemporaryPeer (
		[UnsafeAccessorType ("Microsoft.Android.Runtime.JavaMarshalGCBridge, Mono.Android")] object target,
		nuint componentCount);

	[UnsafeAccessor (UnsafeAccessorKind.StaticMethod, Name = "PrepareComponentsAndCrossReferences")]
	static extern unsafe void PrepareComponentsAndCrossReferences (
		[UnsafeAccessorType ("Microsoft.Android.Runtime.JavaMarshalGCBridge, Mono.Android")] object target,
		MarkCrossReferencesArgs* args);

	[UnsafeAccessor (UnsafeAccessorKind.StaticMethod, Name = "get_BridgeJni")]
	static extern JavaMarshalGCBridgeJni GetBridgeJni (
		[UnsafeAccessorType ("Microsoft.Android.Runtime.JavaMarshalGCBridge, Mono.Android")] object target);

	[TestCase ("net/dot/jni/test/GetThis")]
	[TestCase ("net/dot/jni/test/CallNonvirtualBase")]
	[TestCase ("net/dot/jni/test/CallNonvirtualDerived")]
	public unsafe void HandwrittenPeersUseAndroidGCReferenceContract (string jniTypeName)
	{
		using var peerType = new JniType (jniTypeName);
		using var listType = new JniType ("java/util/ArrayList");
		using var destination = new Java.Interop.JavaObject ();
		var peer = peerType.NewObject (peerType.GetConstructor ("()V"), null);
		JniObjectReference references = default;
		JniObjectReference retained = default;
		try {
			Assert.IsTrue (Java.Interop.Runtime.IsGCUserPeer (peer.Handle),
				$"{jniTypeName} should be recognized as an Android GC peer.");
			var field = peerType.GetInstanceField ("managedReferences", "Ljava/util/ArrayList;");
			references = JniEnvironment.InstanceFields.GetObjectField (peer, field);
			var size = listType.GetInstanceMethod ("size", "()I");
			Assert.AreEqual (0, JniEnvironment.InstanceMethods.CallIntMethod (references, size));

			var bridge = GetBridgeJni (null);
			Assert.IsTrue (bridge.AddReference (peer, destination.PeerReference, knownGCUserPeer: false));
			Assert.AreEqual (1, JniEnvironment.InstanceMethods.CallIntMethod (references, size));
			JniArgumentValue* arguments = stackalloc JniArgumentValue [1];
			arguments [0] = new JniArgumentValue (0);
			retained = JniEnvironment.InstanceMethods.CallObjectMethod (
				references, listType.GetInstanceMethod ("get", "(I)Ljava/lang/Object;"), arguments);
			Assert.IsTrue (JniEnvironment.Types.IsSameObject (destination.PeerReference, retained));

			bridge.ClearReferences (peer);
			Assert.AreEqual (0, JniEnvironment.InstanceMethods.CallIntMethod (references, size));
		} finally {
			JniObjectReference.Dispose (ref retained);
			JniObjectReference.Dispose (ref references);
			JniObjectReference.Dispose (ref peer);
		}
	}

	[Test]
	public void OrdinaryJavaObjectsAreNotGCUserPeers ()
	{
		using var value = new Java.Interop.JavaObject ();
		Assert.IsFalse (Java.Interop.Runtime.IsGCUserPeer (value.PeerReference.Handle));
		Assert.IsFalse (Java.Interop.Runtime.IsGCUserPeer (IntPtr.Zero));
	}

	[Test]
	public void NativeAndManagedBridgeLayoutsMatch ()
	{
		Assert.AreEqual (IntPtr.Size * 4, Marshal.SizeOf<MarkCrossReferencesArgs> ());
		Assert.AreEqual (IntPtr.Size * 2, Marshal.SizeOf<StronglyConnectedComponent> ());
		Assert.AreEqual (IntPtr.Size * 2, Marshal.SizeOf<ComponentCrossReference> ());

		Type handleContext = typeof (HandleContext);
		Assert.AreEqual (0, Marshal.OffsetOf (handleContext, "identityHashCode").ToInt32 ());
		Assert.AreEqual (IntPtr.Size, Marshal.OffsetOf (handleContext, "controlBlock").ToInt32 ());
		Assert.AreEqual (IntPtr.Size * 2, Marshal.SizeOf<HandleContext> ());

		Type controlBlock = typeof (HandleContext.JniObjectReferenceControlBlock);
		Assert.AreEqual (0, Marshal.OffsetOf (controlBlock, "handle").ToInt32 ());
		Assert.AreEqual (IntPtr.Size, Marshal.OffsetOf (controlBlock, "handle_type").ToInt32 ());
		Assert.AreEqual (IntPtr.Size + sizeof (int), Marshal.OffsetOf (controlBlock, "refs_added").ToInt32 ());
		Assert.AreEqual (IntPtr.Size + (2 * sizeof (int)), Marshal.SizeOf<HandleContext.JniObjectReferenceControlBlock> ());
	}

	[TestCase (0ul, true)]
	[TestCase (1ul, false)]
	[TestCase (2ul, false)]
	public void EmptyComponentsRequireTemporaryPeers (ulong count, bool expected)
	{
		Assert.AreEqual (expected, RequiresTemporaryPeer (null, new UIntPtr (count)));
	}

	[Test]
	public unsafe void EmptyComponentsCanAddCrossReferenceWithoutLeakingLocalRefs ()
	{
		StronglyConnectedComponent* components = stackalloc StronglyConnectedComponent [2];
		components [0] = default;
		components [1] = default;
		ComponentCrossReference crossReference = new () { SourceGroupIndex = 0, DestinationGroupIndex = 1 };
		MarkCrossReferencesArgs args = new () {
			ComponentCount = 2,
			Components = components,
			CrossReferenceCount = 1,
			CrossReferences = &crossReference,
		};

		int localReferences = Java.Interop.Runtime.LocalReferenceCount;
		PrepareComponentsAndCrossReferences (null, &args);

		Assert.IsTrue (components [0].Count == 0,
			$"After adding a temporary-peer cross-reference, source SCC Count should be 0, but was {components [0].Count}.");
		Assert.IsTrue (components [1].Count == 0,
			$"After adding a temporary-peer cross-reference, destination SCC Count should be 0, but was {components [1].Count}.");
		int remainingLocalReferences = Java.Interop.Runtime.LocalReferenceCount;
		Assert.IsTrue (remainingLocalReferences == localReferences,
			$"Temporary peers should be released after the cross-reference is added: expected {localReferences} local references, but found {remainingLocalReferences}.");
	}

	[Test]
	[Category ("GCBridge")]
	[Category ("ReferenceTracing")]
	public void ManagedPeerCycleSurvivesJavaRootAndIsCollectedAfterRelease ()
	{
		if (!Microsoft.Android.Runtime.RuntimeFeature.IsCoreClrRuntime && !Microsoft.Android.Runtime.RuntimeFeature.IsNativeAotRuntime)
			Assert.Ignore ("This test exercises the managed GC bridge.");

		WeakReference<BridgeCyclePeer> first;
		WeakReference<BridgeCyclePeer> second;
		using var listener = new ManagedObjectReferenceManagerTests.ReferenceListener (RuntimeEventSource.GlobalReferenceKeyword);
		ulong firstHandle = 0, secondHandle = 0;
		var roots = new JavaObjectArray<Java.Lang.Object> (1);
		try {
			CreatePeerCycle (roots, out first, out second, out firstHandle, out secondHandle);
			WaitForBridgeRound (() => first.TryGetTarget (out _) && second.TryGetTarget (out _),
				"Both peers in the managed cycle should survive while Java roots one peer.");
			var weakHandles = listener.GetEvents ()
				.Where (e => e.Id == 13 && Equals (e.Payload [7], (int)GCBridgeReferenceOperation.GlobalToWeak) &&
					(Equals (e.Payload [0], firstHandle) || Equals (e.Payload [0], secondHandle)))
				.Select (e => e.Payload [1])
				.ToArray ();
			Assert.IsNotEmpty (weakHandles, "The Java-rooted cycle must participate in global-to-weak bridge processing.");
			var survivingHandles = listener.GetEvents ()
				.Where (e => e.Id == 11 && Equals (e.Payload [7], (int)GCBridgeReferenceOperation.WeakToGlobal) &&
					weakHandles.Contains (e.Payload [0]))
				.Select (e => e.Payload [1] is ulong handle ? handle : throw new InvalidOperationException ("A global reference handle must be an unsigned integer."))
				.ToArray ();
			Assert.IsNotEmpty (survivingHandles, "Java-rooted peers must be promoted back to global references.");
		} finally {
			roots.Dispose ();
		}

		WaitForBridgeRound (() => !first.TryGetTarget (out _) && !second.TryGetTarget (out _),
			"Both peers should be collected after releasing the Java root.");
	}

	[MethodImpl (MethodImplOptions.NoInlining)]
	static void CreatePeerCycle (JavaObjectArray<Java.Lang.Object> roots,
		out WeakReference<BridgeCyclePeer> first, out WeakReference<BridgeCyclePeer> second, out ulong firstHandle, out ulong secondHandle)
	{
		var a = new BridgeCyclePeer ();
		var b = new BridgeCyclePeer ();
		a.Next = b;
		b.Next = a;
		roots [0] = a;
		first = new WeakReference<BridgeCyclePeer> (a, trackResurrection: true);
		second = new WeakReference<BridgeCyclePeer> (b, trackResurrection: true);
		firstHandle = unchecked((ulong)(nuint)a.PeerReference.Handle);
		secondHandle = unchecked((ulong)(nuint)b.PeerReference.Handle);
	}

	static void WaitForBridgeRound (Func<bool> predicate, string message)
	{
		int generation = JNIEnv.BridgeProcessingGeneration;
		var timeout = TimeSpan.FromSeconds (10);
		var stopwatch = Stopwatch.StartNew ();
		do {
			GC.Collect (generation: 2, mode: GCCollectionMode.Forced, blocking: true);
			GC.WaitForPendingFinalizers ();
			JniEnvironment.Runtime.ValueManager.CollectPeers ();
			if (JNIEnv.BridgeProcessingGeneration != generation && predicate ())
				return;
			Thread.Sleep (10);
		} while (stopwatch.Elapsed < timeout);

		Assert.Fail ($"{message} No bridge round completed with the expected outcome within {timeout}.");
	}

	sealed class BridgeCyclePeer : Java.Lang.Object
	{
		public BridgeCyclePeer Next { get; set; }
	}

	[Test]
	public void BridgeCallbackIsUnmanagedEntryPoint ()
	{
		Type bridge = typeof (JavaMarshalGCBridge);
		MethodInfo method = bridge.GetMethod ("ProcessBridge", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException ("ProcessBridge was not found.");
		Assert.IsNotNull (method.GetCustomAttribute<UnmanagedCallersOnlyAttribute> ());
		Assert.AreEqual (typeof (void), method.ReturnType);

		ParameterInfo[] parameters = method.GetParameters ();
		Assert.AreEqual (1, parameters.Length);
		Assert.AreEqual (typeof (MarkCrossReferencesArgs).MakePointerType (), parameters [0].ParameterType);
	}
}
