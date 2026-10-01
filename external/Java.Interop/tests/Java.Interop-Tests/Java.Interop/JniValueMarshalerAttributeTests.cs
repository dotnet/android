using System;
using System.Reflection;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	public class JniValueMarshalerAttributeTests {

		[Test]
		public void Constructor ()
		{
			Assert.Throws<ArgumentNullException> (() => new JniValueMarshalerAttribute (null));
			Assert.Throws<ArgumentException> (() => new JniValueMarshalerAttribute (typeof(int)));

			var a   = new JniValueMarshalerAttribute (typeof (DemoValueTypeValueMarshaler));
			Assert.AreEqual (a.MarshalerType, typeof (DemoValueTypeValueMarshaler));
		}
	}

	[JniValueMarshaler (typeof (DemoValueTypeValueMarshaler))]
	struct DemoValueType {
		public int Value { get; }

		public DemoValueType (int value)
		{
			Value = value;
		}
	}

	class DemoValueTypeValueMarshaler : JniValueMarshaler<DemoValueType> {

		JniValueMarshaler<int> Int32Marshaler;

		public override bool IsJniValueType => Int32Marshaler.IsJniValueType;

		public DemoValueTypeValueMarshaler ()
		{
			Int32Marshaler = JniRuntime.CurrentRuntime.ValueManager.GetValueMarshaler<int> ();
		}

		public override DemoValueType CreateGenericValue (
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			Type targetType)
		{
			var value = Int32Marshaler.CreateGenericValue (ref reference, options, typeof (int));
			return new DemoValueType (value);
		}

		public override JniValueMarshalerState CreateGenericArgumentState (DemoValueType value, ParameterAttributes synchronize)
		{
			return Int32Marshaler.CreateGenericArgumentState (value.Value, synchronize);
		}

		public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState (DemoValueType value, ParameterAttributes synchronize)
		{
			return Int32Marshaler.CreateGenericObjectReferenceArgumentState (value.Value, synchronize);
		}

		public override void DestroyArgumentState (object value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			Int32Marshaler.DestroyArgumentState ((value as DemoValueType?)?.Value, ref state, synchronize);
		}

		public override void DestroyGenericArgumentState (DemoValueType value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			Int32Marshaler.DestroyGenericArgumentState (value.Value, ref state, synchronize);
		}
	}
}
