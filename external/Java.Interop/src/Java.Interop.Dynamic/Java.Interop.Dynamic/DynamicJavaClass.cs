using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Java.Interop;

using Mono.Linq.Expressions;

namespace Java.Interop.Dynamic {

	public class DynamicJavaClass : IDynamicMetaObjectProvider
	{
		readonly    static  Func<string, JniPeerMembers>    CreatePeerMembers;

		static DynamicJavaClass ()
		{
			CreatePeerMembers = (Func<string, JniPeerMembers>)
				Delegate.CreateDelegate (
					typeof(Func<string, JniPeerMembers>),
					typeof(JniPeerMembers).GetMethod ("CreatePeerMembers", BindingFlags.NonPublic | BindingFlags.Static));
			if (CreatePeerMembers == null)
				throw new NotSupportedException ("Could not find JniPeerMembers.CreatePeerMembers!");
		}

		public  string          JniClassName            {get; private set;}

		JniPeerMembers          members;

		public DynamicJavaClass (string jniClassName)
		{
			if (jniClassName == null)
				throw new ArgumentNullException ("jniClassName");

			JniClassName    = jniClassName;
			members         = CreatePeerMembers (jniClassName);
		}

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject (Expression parameter)
		{
			return new MetaStaticMemberAccessObject (parameter, this);
		}

		internal bool TryInvokeStaticMember (InvokeMemberBinder binder, DynamicMetaObject[] args, Type returnType, out object value)
		{
			Debug.WriteLine ("# DynamicJavaClass({0}).invoke({1}) with args({2}) as {3}",
					JniClassName, binder.Name, string.Join (", ", args.Select (a => a.Value)), returnType);
			value       = null;
			var margs   = (List<JniArgumentMarshalInfo>) null;
			try {
				var encoded = GetEncodedJniSignature (binder, args, returnType);
				margs       = args.Select (arg => new JniArgumentMarshalInfo (arg.Value, arg.LimitType)).ToList ();
				var jvalues = margs.Select (a => a.JValue).ToArray ();
				value       = members.StaticMethods.CallMethod (encoded, jvalues);
				return true;
			}
			catch (JavaException e) {
				e.Dispose ();
			}
			finally {
				for (int i = 0; margs != null && i < margs.Count; ++i) {
					margs [i].Cleanup (args [i]);
				}
			}
			return false;
		}

		static string GetEncodedJniSignature (InvokeMemberBinder binder, DynamicMetaObject[] args, Type returnType)
		{
			var sb = new StringBuilder ();

			sb.Append (binder.Name);
			sb.Append ("\u0000");
			sb.Append ("(");
			foreach (var arg in args) {
				var argType     = arg.LimitType;
				var typeInfo    = JniEnvironment.Current.JavaVM.GetJniTypeInfoForType (argType);
				sb.Append (typeInfo.ToString ());
			}
			sb.Append (")");
			sb.Append (JniEnvironment.Current.JavaVM.GetJniTypeInfoForType (returnType).JniTypeReference);

			return sb.ToString ();
		}

		internal bool TryGetStaticMemberValue (string fieldName, Type fieldType, out object value)
		{
			Debug.WriteLine ("# DynamicJavaClass({0}).field({1}) as {2}", JniClassName, fieldName, fieldType);
			var typeInfo    = JniEnvironment.Current.JavaVM.GetJniTypeInfoForType (fieldType);
			var encoded     = fieldName + "\u0000" + typeInfo.JniTypeReference;
			try {
				value       =  members.StaticFields.GetValue (encoded);
				return true;
			}
			catch (JavaException e) {
				value       = null;
				e.Dispose ();
				return false;
			}
		}

		internal bool TrySetStaticMemberValue (string fieldName, Type fieldType, object value)
		{
			Debug.WriteLine ("# DynamicJavaClass({0}).field({1}) as {2} = {3}", JniClassName, fieldName, fieldType, value);
			var typeInfo    = JniEnvironment.Current.JavaVM.GetJniTypeInfoForType (fieldType);
			var encoded     = fieldName + "\u0000" + typeInfo.JniTypeReference;
			try {
				members.StaticFields.SetValue (encoded,  value);
				return true;
			}
			catch (JavaException e) {
				e.Dispose ();
			}
			return false;
		}

#if false
		Type CreateManagedPeerType ()
		{
			var className   = JniClassName.Replace ('/', '-');
			var aname       = new AssemblyName ("Java.Interop.Dynamic-" + className);

			var assembly    = AppDomain.CurrentDomain.DefineDynamicAssembly (aname, AssemblyBuilderAccess.ReflectionOnly);
			var module      = assembly.DefineDynamicModule (className);
			var type        = module.DefineType (JniClassName, TypeAttributes.Sealed, typeof (JavaObject));
			type.SetCustomAttribute (
					new CustomAttributeBuilder (
						typeof (JniTypeInfoAttribute).GetConstructor (new[]{ typeof(string) }),
						new []{JniClassName}));

			return type.CreateType ();
		}
#endif
	}

	class DynamicMetaObject<T> : DynamicMetaObject {

		public new T Value {
			get {return (T) base.Value;}
		}

		public Expression ExpressionAsT {
			get {return Expression.Convert (Expression, typeof (T));}
		}

		public DynamicMetaObject (Expression parameter, T value)
			: base (parameter, BindingRestrictions.GetInstanceRestriction (parameter, value), value)
		{
		}
	}

	class MetaStaticMemberAccessObject : DynamicMetaObject<DynamicJavaClass>
	{
		delegate bool TryGetStaticMemberValue (string fieldName, Type fieldType, out object value);
		delegate bool TryInvokeStaticMember (InvokeMemberBinder binder, DynamicMetaObject[] args, Type returnType, out object value);

		public MetaStaticMemberAccessObject (Expression parameter, DynamicJavaClass value)
			: base (parameter, value)
		{
//			Console.WriteLine ("# MyMetaObject..ctor: paramter={0} {1} {2}", parameter.ToCSharpCode (), parameter.GetType (), parameter.Type);
			Debug.WriteLine ("# MyMetaObject..ctor: value={0} {1}", value, value.GetType ());
		}

		public override DynamicMetaObject BindConvert(ConvertBinder binder)
		{
//			Console.WriteLine ("Convert: Expression={0} [{1}]", Expression.ToCSharpCode (), Expression.Type);
			throw new NotSupportedException ("How is this being invoked?!");
		}

		public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
		{
			Debug.WriteLine ("## MetaStaticMemberAccessObject.BindInvokeMember of method={0}; ReturnType={1}; args={{{2}}}; CallInfo={3}", binder.Name, binder.ReturnType,
				string.Join (", ", args.Select (a => string.Format ("{0} [{1}]", a.Expression.ToCSharpCode (), a.LimitType))), binder.CallInfo);

			TryInvokeStaticMember m = Value.TryInvokeStaticMember;
			var call    = new DeferredConvert<DynamicJavaClass> {
				Arguments           = new[]{Expression.Constant (binder), Expression.Constant (args)},
				Instance            = Value,
				FallbackCreator     = v => binder.FallbackInvokeMember (v, args),
				Method              = m.Method,
			};
			var callE   = Expression.Constant (call);
			return new DynamicMetaObject (callE, BindingRestrictions.GetInstanceRestriction (callE, call));
		}

		public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
		{
			// Debug.WriteLine ("SetMember: Expression={0} [{1}]; property={2}; value.LimitType={3}; value.RuntimeType={4}; value.Value={5}", Expression.ToCSharpCode (), Expression.Type, binder.Name, value.LimitType, value.RuntimeType, value.Value);
			var self        = Value;
			var fieldValue  = value.Expression;
			fieldValue      = Expression.Convert (fieldValue, typeof (object));

			Func<string, Type, object, bool>    sfv = self.TrySetStaticMemberValue;
			var call = Expression.Condition (
					test:       Expression.Call (ExpressionAsT, sfv.Method, Expression.Constant (binder.Name), Expression.Constant (value.LimitType), fieldValue),
					ifTrue:     Expression,
					ifFalse:    Expression.Block (new[] {
						binder.FallbackSetMember (this, value).Expression,
						Expression
			}));
			return new DynamicMetaObject (call, BindingRestrictions.GetInstanceRestriction (Expression, Value));
		}

		public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
		{
//			Console.WriteLine ("GetMember: Expression={0} [{1}]; property={2}", Expression.ToCSharpCode (), Expression.Type, binder.Name);
			TryGetStaticMemberValue m = Value.TryGetStaticMemberValue;
			var access  = new DeferredConvert<DynamicJavaClass> {
				Arguments           = new[]{Expression.Constant (binder.Name)},
				Instance            = Value,
				FallbackCreator     = binder.FallbackGetMember,
				Method              = m.Method,
			};
			var accessE = Expression.Constant (access);
			return new DynamicMetaObject (accessE, BindingRestrictions.GetInstanceRestriction (accessE, access));
		}
	}

	class DeferredConvert<T> : IDynamicMetaObjectProvider {

		public  T                                               Instance;
		public  MethodInfo                                      Method;
		public  Expression[]                                    Arguments;
		public  Func<DynamicMetaObject, DynamicMetaObject>      FallbackCreator;

		public DynamicMetaObject GetMetaObject (Expression parameter)
		{
			return new DeferredConvertMetaObject<T> (parameter, this);
		}
	}

	class DeferredConvertMetaObject<T> : DynamicMetaObject<DeferredConvert<T>> {

		public DeferredConvertMetaObject (Expression e, DeferredConvert<T> value)
			: base (e, value)
		{
			Debug.WriteLine ("DeferredConvertMetaObject<{0}>: e={1}", typeof (T).Name, e.ToCSharpCode ());
		}

		public override DynamicMetaObject BindConvert (ConvertBinder binder)
		{
			Debug.WriteLine ("DeferredConvertMetaObject<{0}>.BindConvert: Expression='{1}'; Expression.Type={2}", typeof (T).Name, Expression.ToCSharpCode (), Expression.Type);

			var instance    = Expression.Constant (Value.Instance);
			var instanceMO  = new DynamicMetaObject (instance, BindingRestrictions.GetInstanceRestriction (instance, Value.Instance));
			var value       = Expression.Parameter (typeof (object), "value");
			Debug.WriteLine ("DeferredConvertMetaObject<{0}>.BindConvert: Fallback={1}", typeof (T).Name, Value.FallbackCreator (instanceMO).Expression.ToCSharpCode ());
			var call = Expression.Block (
					new[]{value},
					Expression.Condition (
						test:       Expression.Call (instance, Value.Method, Value.Arguments.Concat (new Expression[]{Expression.Constant (binder.Type), value})),
						ifTrue:     Expression.Convert (value, binder.Type),
						ifFalse:    Expression.Convert (Value.FallbackCreator (instanceMO).Expression, binder.Type))
			);
			Debug.WriteLine ("DeferredConvertMetaObject<{0}>.BindConvert: call={1}", typeof (T).Name, call.ToCSharpCode ());
			return new DynamicMetaObject (
					call,
					BindingRestrictions.GetInstanceRestriction (Expression, Value));
		}
	}

	struct JniArgumentMarshalInfo {
		JValue                          jvalue;
		JniLocalReference               lref;
		IJavaObject                     obj;
		Action<IJavaObject, object>     cleanup;

		internal JniArgumentMarshalInfo (object value, Type valueType)
		{
			this        = new JniArgumentMarshalInfo ();
			var jvm     = JniEnvironment.Current.JavaVM;
			var info    = jvm.GetJniMarshalInfoForType (valueType);
			if (info.CreateJValue != null)
				jvalue = info.CreateJValue (value);
			else if (info.CreateMarshalCollection != null) {
				obj     = info.CreateMarshalCollection (value);
				jvalue  = new JValue (obj);
			} else if (info.CreateLocalRef != null) {
				lref    = info.CreateLocalRef (value);
				jvalue  = new JValue (lref);
			} else
				throw new NotSupportedException ("Don't know how to get a JValue for: " + valueType.FullName);
			cleanup     = info.CleanupMarshalCollection;
		}

		public JValue JValue {
			get {return jvalue;}
		}

		public  void    Cleanup (object value)
		{
			if (cleanup != null && obj != null)
				cleanup (obj, value);
			if (lref != null)
				lref.Dispose ();
		}
	}
}

