using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Java.Interop;
using Java.Interop.Expressions;

using Mono.Linq.Expressions;

using NUnit.Framework;

namespace Java.InteropTests {

	public abstract class JniValueMarshalerContractTests<T> : JavaVMFixture {

		protected   abstract    T       Value           {get;}

		protected   virtual     bool    IsJniValueType  {get;}
		protected   virtual     bool    UsesProxy       {get;}

		protected   virtual     bool    Equals (T x, T y)
		{
			return EqualityComparer<T>.Default.Equals (x, y);
		}

		protected   virtual     void    Dispose (T value)
		{
		}

		protected   JniValueMarshaler<T>    marshaler;

		protected JniValueMarshalerContractTests (JniValueMarshaler<T> marshaler = null)
		{
			this.marshaler  = marshaler ?? JniRuntime.CurrentRuntime.ValueManager.GetValueMarshaler<T> ();
		}

		[Test]
		public void TestIsJniValueType ()
		{
			Assert.AreEqual (IsJniValueType, marshaler.IsJniValueType);
		}

		[Test]
		public void CreateArgumentState ()
		{
			if (IsJniValueType) {
				var s   = marshaler.CreateArgumentState (Value);

				// Note: Only valid if `Value` isn't 0, so don't do that!
				Assert.AreNotEqual (new JniArgumentValue (), s.JniArgumentValue);

				// JniValueMarshaler<T>.CreateArgumentState() for value types only
				// fills out the JniArgumentValue value, nothing else.
				Assert.IsFalse (s.ReferenceValue.IsValid);
				Assert.IsNull (s.PeerableValue);
				Assert.IsNull (s.Extra);

				marshaler.DestroyArgumentState (Value, ref s);
				Assert.AreEqual (new JniValueMarshalerState (), s);
			}
			else {
				var s   = marshaler.CreateArgumentState (Value);

				if (s.PeerableValue != null) {
					Assert.AreEqual (s.PeerableValue.PeerReference, s.ReferenceValue);
				}
				Assert.IsTrue (s.ReferenceValue.IsValid);
				Assert.AreEqual (new JniArgumentValue (s.ReferenceValue), s.JniArgumentValue);

				marshaler.DestroyArgumentState (Value, ref s);
				Assert.AreEqual (new JniValueMarshalerState (), s);
			}
		}

		[Test]
		public void CreateGenericArgumentState ()
		{
			if (IsJniValueType) {
				var s   = marshaler.CreateGenericArgumentState (Value);

				// Note: Only valid if `Value` isn't 0, so don't do that!
				Assert.AreNotEqual (new JniArgumentValue (), s.JniArgumentValue);

				// JniValueMarshaler<T>.CreateArgumentState() for value types only
				// fills out the JniArgumentValue value, nothing else.
				Assert.IsFalse (s.ReferenceValue.IsValid);
				Assert.IsNull (s.PeerableValue);
				Assert.IsNull (s.Extra);

				marshaler.DestroyGenericArgumentState (Value, ref s);
				Assert.AreEqual (new JniValueMarshalerState (), s);
			}
			else {
				var s   = marshaler.CreateGenericArgumentState (Value);

				if (s.PeerableValue != null) {
					Assert.AreEqual (s.PeerableValue.PeerReference, s.ReferenceValue);
				}
				Assert.IsTrue (s.ReferenceValue.IsValid);
				Assert.AreEqual (new JniArgumentValue (s.ReferenceValue), s.JniArgumentValue);

				marshaler.DestroyGenericArgumentState (Value, ref s);
				Assert.AreEqual (new JniValueMarshalerState (), s);
			}
		}

		[Test]
		public void CreateObjectReferenceArgumentState_DefaultValue ()
		{
			var s = marshaler.CreateObjectReferenceArgumentState (default (T));

			if (IsJniValueType) {
				Assert.IsTrue (s.ReferenceValue.IsValid);
				Assert.AreEqual (new JniArgumentValue (s.ReferenceValue), s.JniArgumentValue);
			} else {
				Assert.IsFalse (s.ReferenceValue.IsValid);
				Assert.AreEqual (new JniArgumentValue (), s.JniArgumentValue);
			}
			Assert.IsNull (s.PeerableValue);
			marshaler.DestroyArgumentState (default (T), ref s);
			Assert.AreEqual (new JniValueMarshalerState (), s);
		}

		[Test]
		public void CreateObjectReferenceArgumentState ()
		{
			var s   = marshaler.CreateObjectReferenceArgumentState (Value);
			if (s.PeerableValue != null) {
				Assert.AreEqual (s.PeerableValue.PeerReference, s.ReferenceValue);
			}
			Assert.IsTrue (s.ReferenceValue.IsValid);
			Assert.AreEqual (new JniArgumentValue (s.ReferenceValue), s.JniArgumentValue);

			marshaler.DestroyArgumentState (Value, ref s);
			Assert.AreEqual (new JniValueMarshalerState (), s);
		}

		[Test]
		public void CreateGenericObjectReferenceArgumentState_DefaultValue ()
		{
			var s = marshaler.CreateGenericObjectReferenceArgumentState (default (T));

			if (IsJniValueType) {
				Assert.IsTrue (s.ReferenceValue.IsValid);
				Assert.AreEqual (new JniArgumentValue (s.ReferenceValue), s.JniArgumentValue);
			} else {
				Assert.IsFalse (s.ReferenceValue.IsValid);
				Assert.AreEqual (new JniArgumentValue (), s.JniArgumentValue);
			}
			Assert.IsNull (s.PeerableValue);
			marshaler.DestroyGenericArgumentState (default (T), ref s);
			Assert.AreEqual (new JniValueMarshalerState (), s);
		}

		[Test]
		public void CreateGenericObjectReferenceArgumentState ()
		{
			var s   = marshaler.CreateGenericObjectReferenceArgumentState (Value);
			if (s.PeerableValue != null) {
				Assert.AreEqual (s.PeerableValue.PeerReference, s.ReferenceValue);
			}
			Assert.IsTrue (s.ReferenceValue.IsValid);
			Assert.AreEqual (new JniArgumentValue (s.ReferenceValue), s.JniArgumentValue);

			marshaler.DestroyGenericArgumentState (Value, ref s);
			Assert.AreEqual (new JniValueMarshalerState (), s);
		}

		[Test]
		public void CreateValue ()
		{
			var s   = marshaler.CreateObjectReferenceArgumentState (Value);
			var r   = s.ReferenceValue;
			var o   = marshaler.CreateValue (ref r, JniObjectReferenceOptions.Copy);
			Assert.IsTrue (Equals (Value, (T) o));
			if (!UsesProxy && !typeof(T).IsValueType)
				Assert.AreNotSame (Value, o);
			marshaler.DestroyArgumentState (Value, ref s);

			Dispose ((T) o);
		}

		[Test]
		public void CreateGenericValue ()
		{
			var s   = marshaler.CreateGenericObjectReferenceArgumentState (Value);
			var r   = s.ReferenceValue;
			var o   = marshaler.CreateGenericValue (ref r, JniObjectReferenceOptions.Copy);
			Assert.IsTrue (Equals (Value, o));
			if (!UsesProxy && !typeof(T).IsValueType)
				Assert.AreNotSame (Value, o);
			marshaler.DestroyGenericArgumentState (Value, ref s);

			Dispose (o);
		}

		[Test]
		public void DestroyArgumentState ()
		{
			var s   = new JniValueMarshalerState ();
			marshaler.DestroyArgumentState (null, ref s);
		}

		[Test]
		public void DestroyGenericArgumentState ()
		{
			var s   = new JniValueMarshalerState ();
			marshaler.DestroyGenericArgumentState (default (T), ref s);
		}
	}

	[TestFixture]
	public class JniValueMarshaler_String_ContractTests : JniValueMarshalerContractTests<string> {
		protected   override    string      Value       {get {return "value";}}
	}

	[TestFixture]
	public class JniValueMarshaler_Boolean_ContractTests : JniValueMarshalerContractTests<bool> {
		protected   override    bool    Value           {get {return true;}}
		protected   override    bool    IsJniValueType  {get {return true;}}
	}

	[TestFixture]
	public class JniValueMarshaler_SByte_ContractTests : JniValueMarshalerContractTests<sbyte> {
		protected   override    sbyte   Value           {get {return (sbyte) 2;}}
		protected   override    bool    IsJniValueType  {get {return true;}}
	}

	[TestFixture]
	public class JniValueMarshaler_Char_ContractTests : JniValueMarshalerContractTests<char> {
		protected   override    char    Value           {get {return '3';}}
		protected   override    bool    IsJniValueType  {get {return true;}}
	}

	[TestFixture]
	public class JniValueMarshaler_Int16_ContractTests : JniValueMarshalerContractTests<short> {
		protected   override    short   Value           {get {return (short) 4;}}
		protected   override    bool    IsJniValueType  {get {return true;}}
	}

	[TestFixture]
	public class JniValueMarshaler_Int32_ContractTests : JniValueMarshalerContractTests<int> {
		protected   override    int     Value           {get {return 5;}}
		protected   override    bool    IsJniValueType  {get {return true;}}
	}

	[TestFixture]
	public class JniValueMarshaler_Int64_ContractTests : JniValueMarshalerContractTests<long> {
		protected   override    long    Value           {get {return 6;}}
		protected   override    bool    IsJniValueType  {get {return true;}}
	}

	[TestFixture]
	public class JniValueMarshaler_Single_ContractTests : JniValueMarshalerContractTests<float> {
		protected   override    float   Value           {get {return 7F;}}
		protected   override    bool    IsJniValueType  {get {return true;}}
	}

	[TestFixture]
	public class JniValueMarshaler_Double_ContractTests : JniValueMarshalerContractTests<double> {
		protected   override    double  Value           {get {return 8D;}}
		protected   override    bool    IsJniValueType  {get {return true;}}
	}

	[TestFixture]
	public class JniValueMarshaler_NullableBoolean_ContractTests : JniValueMarshalerContractTests<bool?> {
		protected   override    bool?   Value           {get {return true;}}
	}

	[TestFixture]
	public class JniValueMarshaler_NullableSByte_ContractTests : JniValueMarshalerContractTests<sbyte?> {
		protected   override    sbyte?  Value           {get {return (sbyte) 2;}}
	}

	[TestFixture]
	public class JniValueMarshaler_NullableChar_ContractTests : JniValueMarshalerContractTests<char?> {
		protected   override    char?   Value           {get {return '3';}}
	}

	[TestFixture]
	public class JniValueMarshaler_NullableInt16_ContractTests : JniValueMarshalerContractTests<short?> {
		protected   override    short?  Value           {get {return (short) 4;}}
	}

	[TestFixture]
	public class JniValueMarshaler_NullableInt32_ContractTests : JniValueMarshalerContractTests<int?> {
		protected   override    int?    Value           {get {return 5;}}
	}

	[TestFixture]
	public class JniValueMarshaler_NullableInt64_ContractTests : JniValueMarshalerContractTests<long?> {
		protected   override    long?   Value           {get {return 6;}}
	}

	[TestFixture]
	public class JniValueMarshaler_NullableSingle_ContractTests : JniValueMarshalerContractTests<float?> {
		protected   override    float?  Value           {get {return 7F;}}
	}

	[TestFixture]
	public class JniValueMarshaler_NullableDouble_ContractTests : JniValueMarshalerContractTests<double?> {
		protected   override    double? Value           {get {return 8D;}}
	}

	public abstract class JniInt32ArrayValueMarshalerContractTests<T> : JniValueMarshalerContractTests<T>
		where T : IEnumerable<int>
	{
		protected   abstract    T       CreateArray (int[] values);

		protected   override    T       Value {
			get {return CreateArray (new[]{ 1, 2, 3 });}
		}

		protected   override    bool    Equals (T x, T y)
		{
			return x.SequenceEqual (y);
		}

		[Test]
		public unsafe void DestroyGenericArgumentState_UpdatesSource ()
		{
			var a   = CreateArray (new[]{ 1 });
			var s   = marshaler.CreateGenericObjectReferenceArgumentState (a);
			fixed (int *p = new[]{3})
				JniEnvironment.Arrays.SetIntArrayRegion (s.ReferenceValue, 0, 1, p);
			marshaler.DestroyGenericArgumentState (a, ref s);
			Assert.AreEqual (3, a.First ());
			Dispose (a);
		}

		protected override void Dispose (T value)
		{
			var d = value as IDisposable;
			if (d != null) {
				d.Dispose ();
			}
		}
	}

	[TestFixture]
	public class JniValueMarshaler_Int32Array_ContractTests : JniInt32ArrayValueMarshalerContractTests<int[]> {
		protected   override    int[]                   CreateArray (int[] values) {return values;}

		[Test]
		public unsafe void CreateGenericObjectReferenceArgumentState_OutParameterDoesNotCopy ()
		{
			var a   = new[]{ 1 };
			var s   = marshaler.CreateGenericObjectReferenceArgumentState (a, ParameterAttributes.Out);
			int v;
			JniEnvironment.Arrays.GetIntArrayRegion (s.ReferenceValue, 0, 1, &v);
			Assert.AreEqual (0, v);
			marshaler.DestroyGenericArgumentState (a, ref s);
		}

		[Test]
		public unsafe void DestroyGenericArgumentState_InParameterDoesNotUpdatesSource ()
		{
			var a   = CreateArray (new[]{ 1 });
			var s   = marshaler.CreateGenericObjectReferenceArgumentState (a);
			fixed (int *p = new[]{3})
				JniEnvironment.Arrays.SetIntArrayRegion (s.ReferenceValue, 0, 1, p);
			marshaler.DestroyGenericArgumentState (a, ref s, ParameterAttributes.In);
			Assert.AreEqual (1, a.First ());
			Dispose (a);
		}
	}

	[TestFixture]
	public class JniValueMarshaler_ListOfInt32_ContractTests : JniInt32ArrayValueMarshalerContractTests<IList<int>> {
		protected   override    IList<int>              CreateArray (int[] values) {return values;}
	}

	[TestFixture]
	public class JniValueMarshaler_JavaArray_Int32_ContractTests : JniInt32ArrayValueMarshalerContractTests<JavaArray<int>> {
		protected   override    JavaArray<int>          CreateArray (int[] values) {return new JavaInt32Array (values);}
	}

	[TestFixture]
	public class JniValueMarshaler_JavaPrimitiveArray_Int32_ContractTests : JniInt32ArrayValueMarshalerContractTests<JavaPrimitiveArray<int>> {
		protected   override    JavaPrimitiveArray<int> CreateArray (int[] values) {return new JavaInt32Array (values);}
	}

	[TestFixture]
	public class JniValueMarshaler_JavaInt32Array_ContractTests : JniInt32ArrayValueMarshalerContractTests<JavaInt32Array> {
		protected   override    JavaInt32Array          CreateArray (int[] values) {return new JavaInt32Array (values);}
	}

	[TestFixture]
	public class JniValueMarshaler_object_ContractTests : JniValueMarshalerContractTests<object> {

		readonly    object      value   = new object ();

		protected   override    object                  Value               {get {return value;}}
		protected   override    bool                    UsesProxy           {get {return true;}}

		[Test]
		public void SpecificTypesAreUsed ()
		{
			// As a "GREF optimization", the JniValueMarshaler<object> implementation
			// will use a "nested" JniValueMarshaler for the runtime type,
			// e.g. a boxed int will use JniValueMarshaler<int>
			var s   = marshaler.CreateGenericObjectReferenceArgumentState (42);
			Assert.AreEqual ("java/lang/Integer",   JniEnvironment.Types.GetJniTypeNameFromInstance (s.ReferenceValue));
			marshaler.DestroyGenericArgumentState (42, ref s);

			// Compare to the default proxy behavior...
			s       = marshaler.CreateGenericObjectReferenceArgumentState (value);
			Assert.AreEqual ("com/xamarin/android/internal/JavaProxyObject",    JniEnvironment.Types.GetJniTypeNameFromInstance (s.ReferenceValue));
			marshaler.DestroyGenericArgumentState (value, ref s);
		}
	}

	[TestFixture]
	public class JniValueMarshaler_IJavaPeerable_ContractTests : JniValueMarshalerContractTests<IJavaPeerable> {
		readonly    IJavaPeerable      value   = new JavaObject ();

		protected   override    IJavaPeerable       Value   {get {return value;}}

		[Test]
		public void CreateReturnValueFromManagedExpression ()
		{
			var runtime = Expression.Variable (typeof (JniRuntime), "__jvm");
			var value   = Expression.Variable (typeof (IJavaPeerable), "__value");
			var context = new JniValueMarshalerContext (runtime) {
				LocalVariables  = {
					runtime,
					value,
				},
			};
			marshaler.CreateReturnValueFromManagedExpression (context, value);
			var expected = @"{
	JniRuntime __jvm;
	IJavaPeerable __value;
	JniObjectReference __value_ref;
	IntPtr __value_handle;
	IntPtr __value_rtn;

	if (null == __value)
	{
		return __value_ref = new JniObjectReference();
	}
	else
	{
		return __value_ref = __value.PeerReference;
	}
	__value_handle = __value_ref.Handle;
	__value_rtn = References.NewReturnToJniRef(__value_ref);
	JniObjectReference.Dispose(__value_ref);
}";
			CheckExpression (context, expected);
		}

		static void CheckExpression (JniValueMarshalerContext context, string expected)
		{
			var block   = Expression.Block (context.LocalVariables, context.CreationStatements.Concat (context.CleanupStatements));
			Assert.AreEqual (expected, block.ToCSharpCode ());
		}
	}
}

