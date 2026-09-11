#if !__ANDROID__
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Java.Interop;
using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	[NonParallelizable]
	public class JniSubclassConstructorCacheTests : JavaVMFixture
	{
		[Test]
		public void ConcurrentCreationDisposesUnpublishedConstructors ()
		{
			RunWithReferenceTracking ((members, references, runtime) => {
				const int count = 4;
				using var barrier = new Barrier (count);
				references.OnCreate = () => Assert.IsTrue (barrier.SignalAndWait (TimeSpan.FromSeconds (30)), "Constructor creation did not overlap.");

				var calls = Enumerable.Range (0, count)
					.Select (_ => Task.Factory.StartNew (
						() => {
							runtime.AttachCurrentThread ();
							Assert.AreSame (runtime, JniEnvironment.Runtime);
							return members.InstanceMethods.GetConstructorsForType (typeof (MyString));
						},
						CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
					.ToArray ();
				var constructors = Task.WhenAll (calls).GetAwaiter ().GetResult ();
				references.OnCreate = null;

				foreach (var constructor in constructors)
					Assert.AreSame (constructors [0], constructor);
				AssertWinnerAndCleanup (members, references, runtime, constructors [0], count);
			});
		}

		[Test]
		public void RecursiveCreationDisposesOuterConstructor ()
		{
			RunWithReferenceTracking ((members, references, runtime) => {
				JniPeerMembers.JniInstanceMethods recursive = null;
				references.OnCreate = () => {
					references.OnCreate = null;
					recursive = members.InstanceMethods.GetConstructorsForType (typeof (MyString));
				};

				var constructor = members.InstanceMethods.GetConstructorsForType (typeof (MyString));

				Assert.AreSame (recursive, constructor, "The recursive lookup should publish the winner.");
				AssertWinnerAndCleanup (members, references, runtime, constructor, 2);
			});
		}

		[Test]
		public void PublicationFailureDisposesConstructor ()
		{
			RunWithReferenceTracking ((members, references, runtime) => {
				var comparer = new ThrowingTypeComparer ();
				var cache = new ConcurrentDictionary<Type, JniPeerMembers.JniInstanceMethods> (comparer);
				var field = typeof (JniPeerMembers.JniInstanceMethods).GetField ("subclassConstructors", BindingFlags.NonPublic | BindingFlags.Instance);
				Assert.IsNotNull (field);
				field.SetValue (members.InstanceMethods, cache);
				references.OnCreate = () => comparer.ThrowOnHash = true;

				Assert.Throws<InvalidOperationException> (() => members.InstanceMethods.GetConstructorsForType (typeof (MyString)));
				references.OnCreate = null;
				comparer.ThrowOnHash = false;

				Assert.AreEqual (1, references.Created.Count);
				Assert.IsEmpty (cache);
				AssertReleased (references, runtime);

				// Failure must leave the cache usable for a subsequent lookup.
				var constructor = members.InstanceMethods.GetConstructorsForType (typeof (MyString));
				AssertWinnerAndCleanup (members, references, runtime, constructor, 2);
			});
		}

		static unsafe void AssertWinnerAndCleanup (JniPeerMembers members, TrackingReferenceManager references, JniRuntime runtime, JniPeerMembers.JniInstanceMethods winner, int created)
		{
			var type = winner.JniPeerType;
			var handle = type.PeerReference.Handle;
			Assert.AreSame (winner, members.InstanceMethods.GetConstructorsForType (typeof (MyString)));
			Assert.IsTrue (winner.GetConstructor ("()V").IsValid);

			var instance = members.InstanceMethods.NewObject ("()V", typeof (MyString), null);
			try {
				Assert.IsTrue (type.IsInstanceOfType (instance), "The winning constructor must remain usable.");
			} finally {
				JniObjectReference.Dispose (ref instance);
			}

			Assert.Multiple (() => {
				Assert.AreEqual (created, references.Created.Count, "Cache hits must not create more class globals.");
				CollectionAssert.AreEquivalent (new [] { handle }, references.Created.Where (references.IsLive).Distinct ());
				var tracked = GetTrackedInstances (runtime);
				lock (tracked) {
					CollectionAssert.AreEquivalent (new [] { handle }, references.Created.Where (tracked.ContainsKey).Distinct ());
					Assert.AreSame (type, tracked [handle], "The winner must stay registered until disposal.");
				}

				JniPeerMembers.Dispose (members);
				Assert.IsFalse (type.PeerReference.IsValid);
				AssertReleased (references, runtime);
			});
		}

		static void AssertReleased (TrackingReferenceManager references, JniRuntime runtime)
		{
			Assert.IsFalse (references.Created.Any (references.IsLive), "Unpublished constructor class globals must be deleted.");
			var tracked = GetTrackedInstances (runtime);
			lock (tracked)
				Assert.IsFalse (references.Created.Any (tracked.ContainsKey), "Constructor classes must not be retained until runtime shutdown.");
		}

		static Dictionary<IntPtr, IDisposable> GetTrackedInstances (JniRuntime runtime)
		{
			var field = typeof (JniRuntime).GetField ("TrackedInstances", BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.IsNotNull (field);
			return (Dictionary<IntPtr, IDisposable>) field.GetValue (runtime);
		}

		static void RunWithReferenceTracking (Action<JniPeerMembers, TrackingReferenceManager, JniRuntime> test)
		{
			var runtime = JniEnvironment.Runtime;
			var original = runtime.ObjectReferenceManager;
			var references = new TrackingReferenceManager (original);
			references.OnSetRuntime (runtime);
			var property = typeof (JniRuntime).GetProperty (nameof (JniRuntime.ObjectReferenceManager));
			Assert.IsNotNull (property);
			var members = new JniPeerMembers ("java/lang/Object", typeof (JavaObject));
			try {
				// Exclude class initialization and the parent's separately-owned class global.
				Assert.IsTrue (members.JniPeerType.PeerReference.IsValid);
				using (var type = new JniType (MyString.JniTypeName)) {
					var instance = type.AllocObject ();
					JniObjectReference.Dispose (ref instance);
				}
				// Decorate the fixture's manager without creating another runtime or changing JNI environments.
				property.SetValue (runtime, references);
				references.TrackCreation = true;
				test (members, references, runtime);
			} finally {
				references.TrackCreation = false;
				references.OnCreate = null;
				try {
					JniPeerMembers.Dispose (members);
					// Also clean up leaked candidates when running this regression against broken code.
					var tracked = GetTrackedInstances (runtime);
					List<IDisposable> remaining;
					lock (tracked)
						remaining = references.LiveHandles.Where (tracked.ContainsKey).Select (handle => tracked [handle]).ToList ();
					foreach (var value in remaining)
						value.Dispose ();
				} finally {
					property.SetValue (runtime, original);
				}
			}
		}

		sealed class TrackingReferenceManager : JniRuntime.JniObjectReferenceManager
		{
			readonly JniRuntime.JniObjectReferenceManager inner;
			readonly ConcurrentDictionary<IntPtr, byte> live = new ConcurrentDictionary<IntPtr, byte> ();
			readonly AsyncLocal<bool> trackCreation = new AsyncLocal<bool> ();

			public readonly ConcurrentBag<IntPtr> Created = new ConcurrentBag<IntPtr> ();
			public Action OnCreate;

			public TrackingReferenceManager (JniRuntime.JniObjectReferenceManager inner)
			{
				this.inner = inner;
			}

			public bool TrackCreation {
				get => trackCreation.Value;
				set => trackCreation.Value = value;
			}

			public ICollection<IntPtr> LiveHandles => live.Keys;
			public override int GlobalReferenceCount => inner.GlobalReferenceCount;
			public override int WeakGlobalReferenceCount => inner.WeakGlobalReferenceCount;
			public override bool LogLocalReferenceMessages => inner.LogLocalReferenceMessages;
			public override bool LogGlobalReferenceMessages => inner.LogGlobalReferenceMessages;

			public bool IsLive (IntPtr handle) => live.ContainsKey (handle);

			public override JniObjectReference CreateGlobalReference (JniObjectReference reference)
			{
				var result = inner.CreateGlobalReference (reference);
				if (TrackCreation) {
					live.TryAdd (result.Handle, 0);
					Created.Add (result.Handle);
					try {
						OnCreate?.Invoke ();
					} catch {
						DeleteGlobalReference (ref result);
						throw;
					}
				}
				return result;
			}

			public override void DeleteGlobalReference (ref JniObjectReference reference)
			{
				var handle = reference.Handle;
				inner.DeleteGlobalReference (ref reference);
				live.TryRemove (handle, out _);
			}

			public override JniObjectReference CreateLocalReference (JniObjectReference reference, ref int localReferenceCount) =>
				inner.CreateLocalReference (reference, ref localReferenceCount);

			public override void DeleteLocalReference (ref JniObjectReference reference, ref int localReferenceCount) =>
				inner.DeleteLocalReference (ref reference, ref localReferenceCount);

			public override void CreatedLocalReference (JniObjectReference reference, ref int localReferenceCount) =>
				inner.CreatedLocalReference (reference, ref localReferenceCount);

			public override IntPtr ReleaseLocalReference (ref JniObjectReference reference, ref int localReferenceCount) =>
				inner.ReleaseLocalReference (ref reference, ref localReferenceCount);

			public override JniObjectReference CreateWeakGlobalReference (JniObjectReference reference) =>
				inner.CreateWeakGlobalReference (reference);

			public override void DeleteWeakGlobalReference (ref JniObjectReference reference) =>
				inner.DeleteWeakGlobalReference (ref reference);

			public override void WriteLocalReferenceLine (string format, params object [] args) =>
				inner.WriteLocalReferenceLine (format, args);

			public override void WriteGlobalReferenceLine (string format, params object [] args) =>
				inner.WriteGlobalReferenceLine (format, args);
		}

		sealed class ThrowingTypeComparer : IEqualityComparer<Type>
		{
			public bool ThrowOnHash;

			public bool Equals (Type x, Type y) => x == y;

			public int GetHashCode (Type type)
			{
				if (ThrowOnHash)
					throw new InvalidOperationException ("Constructor publication failed.");
				return type.GetHashCode ();
			}
		}
	}
}
#endif
