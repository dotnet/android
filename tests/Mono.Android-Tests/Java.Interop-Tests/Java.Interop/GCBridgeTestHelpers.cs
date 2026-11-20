#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Android.Runtime;
using Java.Interop;

namespace Java.InteropTests
{
	static class GCBridgeTestHelpers
	{
		/// <summary>
		/// Waits until a GC bridge round that started after this call has completed.
		/// </summary>
		public static async Task WaitForBridgeRoundsAsync (CancellationToken cancellationToken)
		{
			// Bridge rounds run one at a time on a background thread. A round frees its handles,
			// then increments BridgeProcessingGeneration, then writes its Stop event. When the
			// wait starts, one round may still be running and the next may already be queued
			// with objects gathered by an earlier GC. The third round to complete therefore
			// started from a GC after the wait started, and the second round's events have
			// all been written.
			const int RoundsAfterCurrentState = 3;
			int targetGeneration = JNIEnv.BridgeProcessingGeneration + RoundsAfterCurrentState;
			while (JNIEnv.BridgeProcessingGeneration < targetGeneration) {
				// A GC starts a bridge round only when it finds an unreachable peer.
				CreateUnreachablePeer ();
				GC.Collect (generation: 2, mode: GCCollectionMode.Forced, blocking: true);
				await Task.Delay (10, cancellationToken);
			}
		}

		/// <summary>
		/// Waits for a bridge round to process peers that are already unreachable, then
		/// collects them and runs their finalizers.
		/// </summary>
		public static async Task CollectUnreachablePeersAsync (CancellationToken cancellationToken)
		{
			await WaitForBridgeRoundsAsync (cancellationToken);
			GC.Collect (generation: 2, mode: GCCollectionMode.Forced, blocking: true);
			GC.WaitForPendingFinalizers ();
			// Free finalized peers so long weak references observe their collection.
			GC.Collect (generation: 2, mode: GCCollectionMode.Forced, blocking: true);
			JniEnvironment.Runtime.ValueManager.CollectPeers ();
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static void CreateUnreachablePeer ()
		{
			_ = new Java.Interop.JavaObject ();
		}
	}
}
