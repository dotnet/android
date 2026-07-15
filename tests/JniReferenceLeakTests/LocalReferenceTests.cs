using Java.Interop;

namespace JniReferenceLeakTests;

[TestFixture]
public sealed class LocalReferenceTests
{
	[Test]
	public void InvalidCreatedReferenceDoesNotChangeLocalReferenceCount ()
	{
		int before = JniEnvironment.LocalReferenceCount;
		var reference = new JniObjectReference ();
		JniEnvironment.References.CreatedReference (reference);
		Assert.AreEqual (before, JniEnvironment.LocalReferenceCount);
	}

	[Test]
	public void ArrayOperationsDoNotLeakLocalReferences ()
	{
		int before = JniEnvironment.LocalReferenceCount;
		for (int i = 0; i < 20; i++) {
			ExerciseArray (new JavaBooleanArray (new [] { true }), false);
			ExerciseArray (new JavaSByteArray (new sbyte [] { 1 }), (sbyte)2);
			ExerciseArray (new JavaCharArray (new [] { 'a' }), 'b');
			ExerciseArray (new JavaInt16Array (new short [] { 1 }), (short)2);
			ExerciseArray (new JavaInt32Array (new [] { 1 }), 2);
			ExerciseArray (new JavaInt64Array (new long [] { 1 }), 2L);
			ExerciseArray (new JavaSingleArray (new [] { 1f }), 2f);
			ExerciseArray (new JavaDoubleArray (new [] { 1d }), 2d);
			ExerciseArray (new JavaObjectArray<int> (new [] { 1 }), 2);
			ExerciseArray (new JavaObjectArray<int[]> (new [] { new [] { 1 } }), new [] { 2 });
			ExerciseArray (new JavaObjectArray<int[][]> (new [] { new [] { new [] { 1 } } }), new [] { new [] { 2 } });
			ExerciseArray (new JavaObjectArray<string> (new [] { "value" }), "replacement");
			ExerciseArray (new JavaObjectArray<object> (new object [] { new object () }), new object ());

			using var firstJavaArray = new JavaInt32Array (new [] { 1 });
			using var secondJavaArray = new JavaInt32Array (new [] { 2 });
			ExerciseArray (new JavaObjectArray<JavaInt32Array> (new [] { firstJavaArray }), secondJavaArray);

			using var firstJavaObject = new JavaObject ();
			using var secondJavaObject = new JavaObject ();
			ExerciseArray (new JavaObjectArray<JavaObject> (new [] { firstJavaObject }), secondJavaObject);
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

	static void ExerciseArray<T> (JavaArray<T> array, T replacement)
	{
		using (array) {
			_ = array [0];
			array [0] = replacement;
			Assert.AreEqual (1, array.ToArray ().Length);
		}
	}

}
