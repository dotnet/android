using Java.Interop;

namespace JniReferenceLeakTests;

[TestFixture]
public sealed class LocalReferenceTests
{
	// JNINativeInterface function indexes are fixed by the JNI specification.
	const int DeleteLocalReferenceFunctionIndex = 23;
	const int NewLocalReferenceFunctionIndex = 25;

	[Test]
	public void InvalidCreatedReferenceDoesNotChangeLocalReferenceCount ()
	{
		int before = JniEnvironment.LocalReferenceCount;
		var reference = new JniObjectReference ();
		JniEnvironment.References.CreatedReference (reference);
		Assert.AreEqual (before, JniEnvironment.LocalReferenceCount);
	}

	[Test]
	public void CreatedLocalReferenceUpdatesLocalReferenceCount ()
	{
		using var type = new JniType ("java/lang/Object");
		int before = JniEnvironment.LocalReferenceCount;
		IntPtr handle = NewReference (
			NewLocalReferenceFunctionIndex,
			JniEnvironment.EnvironmentPointer,
			type.PeerReference.Handle);
		var reference = new JniObjectReference (handle, JniObjectReferenceType.Local);
		bool registered = false;

		try {
			Assert.AreEqual (before, JniEnvironment.LocalReferenceCount);
			JniEnvironment.References.CreatedReference (reference);
			registered = true;
			Assert.AreEqual (before + 1, JniEnvironment.LocalReferenceCount);
		} finally {
			if (registered) {
				JniObjectReference.Dispose (ref reference);
			} else {
				DeleteReference (
					DeleteLocalReferenceFunctionIndex,
					JniEnvironment.EnvironmentPointer,
					reference.Handle);
			}
		}

		Assert.AreEqual (before, JniEnvironment.LocalReferenceCount);
	}

	[Test]
	public void CreatedGlobalReferenceIsRejected ()
	{
		using var type = new JniType ("java/lang/Object");
		var reference = new JniObjectReference (type.PeerReference.Handle, JniObjectReferenceType.Global);
		Assert.Throws<ArgumentException> (() => JniEnvironment.References.CreatedReference (reference));
	}

	[Test]
	public void ArrayOperationsDoNotLeakLocalReferences ()
	{
		int before = JniEnvironment.LocalReferenceCount;
		for (int i = 0; i < 20; i++) {
			ExerciseArrays ();
		}

		Assert.AreEqual (before, JniEnvironment.LocalReferenceCount);
	}

	[Test]
	public void JavaSideActivationDoesNotLeakLocalReferences ()
	{
		int before = JniEnvironment.LocalReferenceCount;
		for (int i = 0; i < 20; i++) {
			using var instance = ActivationProbe.CreateFromJava ();
			Assert.IsTrue (instance.DefaultConstructorInvoked);
		}

		Assert.AreEqual (before, JniEnvironment.LocalReferenceCount);
	}

	static void ExerciseArrays ()
	{
		ExerciseArray (values => new JavaBooleanArray (values), true, false);
		ExerciseArray (values => new JavaSByteArray (values), (sbyte)1, (sbyte)2);
		ExerciseArray (values => new JavaCharArray (values), 'a', 'b');
		ExerciseArray (values => new JavaInt16Array (values), (short)1, (short)2);
		ExerciseArray (values => new JavaInt32Array (values), 1, 2);
		ExerciseArray (values => new JavaInt64Array (values), 1L, 2L);
		ExerciseArray (values => new JavaSingleArray (values), 1f, 2f);
		ExerciseArray (values => new JavaDoubleArray (values), 1d, 2d);
		ExerciseArray (values => new JavaObjectArray<int> (values), 1, 2);
		ExerciseArray (values => new JavaObjectArray<int[]> (values), new [] { 1 }, new [] { 2 });
		ExerciseArray (values => new JavaObjectArray<int[][]> (values), new [] { new [] { 1 } }, new [] { new [] { 2 } });
		ExerciseArray (values => new JavaObjectArray<string> (values), "value", "replacement");
		ExerciseArray (values => new JavaObjectArray<object> (values), new object (), new object ());

		using var firstJavaArray = new JavaInt32Array (new [] { 1 });
		using var secondJavaArray = new JavaInt32Array (new [] { 2 });
		ExerciseArray (values => new JavaObjectArray<JavaInt32Array> (values), firstJavaArray, secondJavaArray);

		using var firstJavaObject = new JavaObject ();
		using var secondJavaObject = new JavaObject ();
		ExerciseArray (values => new JavaObjectArray<JavaObject> (values), firstJavaObject, secondJavaObject);
	}

	static void ExerciseArray<T> (Func<T [], JavaArray<T>> createArray, T first, T second)
	{
		using (var empty = createArray ([])) {
			var emptyCollection = (ICollection<T>)empty;
			var emptyList = (IList<T>)empty;
			Assert.AreEqual (0, emptyCollection.Count);
			Assert.IsFalse (emptyCollection.Contains (first));
			Assert.AreEqual (-1, emptyList.IndexOf (first));
			Assert.Throws<NotSupportedException> (() => emptyCollection.Add (first));
			Assert.Throws<NotSupportedException> (() => emptyCollection.Remove (first));
			Assert.Throws<NotSupportedException> (() => emptyList.Insert (-1, first));
			Assert.Throws<NotSupportedException> (() => emptyList.Insert (1, first));
			Assert.Throws<NotSupportedException> (() => emptyList.Insert (0, first));
			Assert.Throws<NotSupportedException> (() => emptyList.RemoveAt (-1));
			Assert.Throws<NotSupportedException> (() => emptyList.RemoveAt (0));
		}

		using (var array = createArray ([first, second])) {
			var collection = (ICollection<T>)array;
			var list = (IList<T>)array;
			Assert.AreEqual (2, collection.Count);
			Assert.IsTrue (collection.Contains (first));
			Assert.IsTrue (collection.Contains (second));
			Assert.AreEqual (0, list.IndexOf (first));
			Assert.AreEqual (1, list.IndexOf (second));

			_ = list [0];
			list [0] = second;
			Assert.Catch<ArgumentException> (() => _ = list [-1]);
			Assert.Catch<ArgumentException> (() => _ = list [list.Count]);
			Assert.Catch<ArgumentException> (() => list [-1] = first);
			Assert.Catch<ArgumentException> (() => list [list.Count] = first);

			var destination = new T [collection.Count + 2];
			collection.CopyTo (destination, 1);
			Assert.Throws<ArgumentOutOfRangeException> (() => collection.CopyTo (new T [collection.Count], -1));
			Assert.Throws<ArgumentException> (() => collection.CopyTo (new T [collection.Count], 1));
			Assert.Throws<ArgumentException> (() => collection.CopyTo ([], 0));
			Assert.AreEqual (collection.Count, array.ToArray ().Length);

			collection.Clear ();
			Assert.AreEqual (2, collection.Count);
		}
	}

	static unsafe IntPtr NewReference (int functionIndex, IntPtr environment, IntPtr reference)
	{
		var functions = *(IntPtr**)environment;
		var function = (delegate* unmanaged<IntPtr, IntPtr, IntPtr>)functions [functionIndex];
		return function (environment, reference);
	}

	static unsafe void DeleteReference (int functionIndex, IntPtr environment, IntPtr reference)
	{
		var functions = *(IntPtr**)environment;
		var function = (delegate* unmanaged<IntPtr, IntPtr, void>)functions [functionIndex];
		function (environment, reference);
	}
}
