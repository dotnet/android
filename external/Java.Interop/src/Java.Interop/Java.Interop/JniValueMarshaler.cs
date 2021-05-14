#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

using Java.Interop.Expressions;

namespace Java.Interop.Expressions {

	sealed class VariableCollection : KeyedCollection<string, ParameterExpression> {

		protected override string GetKeyForItem (ParameterExpression item)
		{
			return item.Name!;
		}
	}

	public sealed class JniValueMarshalerContext {
		public  Expression                                      Runtime             {get;}
		public  Expression?                                     ValueManager        {get;}

		public  KeyedCollection<string, ParameterExpression>    LocalVariables      {get;}  = new VariableCollection ();
		public  Collection<Expression>                          CreationStatements  {get;}  = new Collection<Expression> ();
		public  Collection<Expression>                          CleanupStatements   {get;}  = new Collection<Expression> ();

		public JniValueMarshalerContext (Expression runtime)
			: this (runtime, null)
		{
		}

		public JniValueMarshalerContext (Expression runtime, Expression? vm)
		{
			Runtime        = runtime ?? throw new ArgumentNullException (nameof (runtime));
			ValueManager   = vm;
		}
	}
}

namespace Java.Interop {

	public struct JniValueMarshalerState : IEquatable<JniValueMarshalerState> {

		public  JniArgumentValue        JniArgumentValue    {get; private set;}
		public  JniObjectReference      ReferenceValue      {get; private set;}
		public  IJavaPeerable?          PeerableValue       {get; private set;}
		public  object?                 Extra               {get; private set;}

		public JniValueMarshalerState (JniArgumentValue jniArgumentValue, object? extra = null)
		{
			JniArgumentValue    = jniArgumentValue;
			ReferenceValue      = default (JniObjectReference);
			PeerableValue       = null;
			Extra               = extra;
		}

		public JniValueMarshalerState (JniObjectReference referenceValue, object? extra = null)
		{
			JniArgumentValue    = new JniArgumentValue (referenceValue);
			ReferenceValue      = referenceValue;
			PeerableValue       = null;
			Extra               = extra;
		}

		public JniValueMarshalerState (IJavaPeerable? peerableValue, object? extra = null)
		{
			PeerableValue       = peerableValue;
			ReferenceValue      = peerableValue == null ? default (JniObjectReference) : peerableValue.PeerReference;
			JniArgumentValue    = new JniArgumentValue (ReferenceValue);
			Extra               = extra;
		}

		internal JniValueMarshalerState (JniValueMarshalerState copy, object? extra = null)
		{
			JniArgumentValue    = copy.JniArgumentValue;
			ReferenceValue      = copy.ReferenceValue;
			PeerableValue       = copy.PeerableValue;
			Extra               = extra ?? copy.Extra;
		}

		public override int GetHashCode ()
		{
			return JniArgumentValue.GetHashCode ();
		}

		public override bool Equals (object? obj)
		{
			var o = obj as JniValueMarshalerState?;
			if (!o.HasValue)
				return false;
			return Equals (o.Value);
		}

		public bool Equals (JniValueMarshalerState other)
		{
			return JniArgumentValue.Equals (other.JniArgumentValue) &&
				ReferenceValue.Equals (other.ReferenceValue) &&
				object.ReferenceEquals (PeerableValue, other.PeerableValue) &&
				object.ReferenceEquals (Extra, other.Extra);
		}

		public static bool operator == (JniValueMarshalerState a, JniValueMarshalerState b) => a.Equals (b);
		public static bool operator != (JniValueMarshalerState a, JniValueMarshalerState b) => !a.Equals (b);

		public override string ToString ()
		{
			return string.Format ("JniValueMarshalerState({0}, ReferenceValue={1}, PeerableValue=0x{2}, Extra={3})",
					JniArgumentValue.ToString (),
					ReferenceValue.ToString (),
					RuntimeHelpers.GetHashCode (PeerableValue!).ToString ("x"),
					Extra);
		}
	}

	public abstract class JniValueMarshaler {

		public  virtual     bool                    IsJniValueType {
			get {return false;}
		}

		static  readonly    Type                    IntPtr_type     = typeof(IntPtr);
		public  virtual     Type                    MarshalType {
			get {return IntPtr_type;}
		}

		public  abstract    object?                 CreateValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type? targetType = null);

		public  virtual     JniValueMarshalerState  CreateArgumentState (object? value, ParameterAttributes synchronize = 0)
		{
			return CreateObjectReferenceArgumentState (value, synchronize);
		}

		public  abstract    JniValueMarshalerState  CreateObjectReferenceArgumentState (object? value, ParameterAttributes synchronize = 0);
		public  abstract    void                    DestroyArgumentState (object? value, ref JniValueMarshalerState state, ParameterAttributes synchronize = 0);

		internal object? CreateValue (IntPtr handle, Type? targetType)
		{
			var r = new JniObjectReference (handle);
			return CreateValue (ref r, JniObjectReferenceOptions.Copy, targetType);
		}

		public  virtual     Expression              CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize = 0, Type? targetType = null)
		{
			Func<IntPtr, Type, object?> m   = CreateValue;

			var self    = CreateSelf (context, sourceValue);

			var call    = Expression.Call (self, m.GetMethodInfo (), sourceValue, Expression.Constant (targetType, typeof (Type)));
			return targetType == null
				? (Expression) call
				: Expression.Convert (call, targetType);
		}

		Expression CreateSelf (JniValueMarshalerContext context, ParameterExpression sourceValue)
		{
			var self = Expression.Variable (GetType (), sourceValue.Name + "_marshaler");
			context.LocalVariables.Add (self);
			context.CreationStatements.Add (Expression.Assign (self, Expression.New (GetType ())));
			return self;
		}

		public  virtual     Expression              CreateReturnValueFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue)
		{
			CreateParameterFromManagedExpression (context, sourceValue, 0);
			var s   = context.LocalVariables [sourceValue.Name + "_state"];
			return ReturnObjectReferenceToJni (context, sourceValue.Name, Expression.Property (s, "ReferenceValue"));
		}

		protected Expression ReturnObjectReferenceToJni (JniValueMarshalerContext context, string? namePrefix, Expression sourceValue)
		{
			Func<JniObjectReference, IntPtr>    m = JniEnvironment.References.NewReturnToJniRef;
			var r   = Expression.Variable (MarshalType, namePrefix + "_rtn");
			if (context == null)
				throw new ArgumentNullException (nameof (context));
			context.LocalVariables.Add (r);
			context.CreationStatements.Add (
				Expression.Assign (r,
					Expression.Call (m.GetMethodInfo (), sourceValue)));
			return r;
		}

		delegate void DestroyArgumentStateCb (object value, ref JniValueMarshalerState state, ParameterAttributes synchronize);
		public  virtual     Expression              CreateParameterFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize)
		{
			Func<object, ParameterAttributes, JniValueMarshalerState>   c   = CreateArgumentState;
			DestroyArgumentStateCb                                      d   = DestroyArgumentState;

			var self    = CreateSelf (context, sourceValue);
			var state   = Expression.Variable (typeof(JniValueMarshalerState), sourceValue.Name + "_state");
			var ret     = Expression.Variable (MarshalType, sourceValue.Name + "_val");

			context.LocalVariables.Add (state);
			context.LocalVariables.Add (ret);
			context.CreationStatements.Add (Expression.Assign (state, Expression.Call (self, c.GetMethodInfo (), Expression.Convert (sourceValue, typeof (object)), Expression.Constant (synchronize, typeof (ParameterAttributes)))));
			context.CreationStatements.Add (
					Expression.Assign (ret,
						Expression.Property (
							Expression.Property (state, "ReferenceValue"),
							"Handle")));
			context.CleanupStatements.Add (Expression.Call (self, d.GetMethodInfo (), Expression.Convert (sourceValue, typeof (object)), state, Expression.Constant (synchronize)));

			return ret;
		}

		delegate void DisposeObjRef (ref JniObjectReference r);
		protected static Expression DisposeObjectReference (Expression sourceValue)
		{
			DisposeObjRef   m   = JniObjectReference.Dispose;
			return Expression.Call (m.GetMethodInfo (), sourceValue);
		}
	}

	public abstract class JniValueMarshaler<T> : JniValueMarshaler {

		[return: MaybeNull]
		public  abstract    T                       CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type? targetType = null);

		public  virtual     JniValueMarshalerState  CreateGenericArgumentState ([MaybeNull] T value, ParameterAttributes synchronize = 0)
		{
			return CreateGenericObjectReferenceArgumentState (value, synchronize);
		}

		public  abstract    JniValueMarshalerState  CreateGenericObjectReferenceArgumentState ([MaybeNull] T value, ParameterAttributes synchronize = 0);
		public  abstract    void                    DestroyGenericArgumentState ([AllowNull] T value, ref JniValueMarshalerState state, ParameterAttributes synchronize = 0);

		public override object? CreateValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type? targetType = null)
		{
			return CreateGenericValue (ref reference, options, targetType ?? typeof (T));
		}

		public override JniValueMarshalerState CreateArgumentState (object? value, ParameterAttributes synchronize = 0)
		{
			return CreateGenericArgumentState ((T) value!, synchronize);
		}

		public override JniValueMarshalerState CreateObjectReferenceArgumentState (object? value, ParameterAttributes synchronize = 0)
		{
			return CreateGenericObjectReferenceArgumentState ((T) value!, synchronize);
		}

		public override void DestroyArgumentState (object? value, ref JniValueMarshalerState state, ParameterAttributes synchronize = 0)
		{
			DestroyGenericArgumentState ((T) value!, ref state, synchronize);
		}
	}
}
