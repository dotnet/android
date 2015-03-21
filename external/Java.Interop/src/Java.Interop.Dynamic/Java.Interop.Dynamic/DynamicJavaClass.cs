using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Java.Interop;

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

		internal StaticMethodAccess GetStaticMethodAccess (InvokeMemberBinder binder, DynamicMetaObject[] args)
		{
			return new StaticMethodAccess (this, binder, args);
		}

		public object CallStaticMethod (InvokeMemberBinder binder, DynamicMetaObject[] args, Type returnType)
		{
			Debug.WriteLine ("# DynamicJavaClass({0}).invoke({1}) with args({2}) as {3}",
					JniClassName, binder.Name, string.Join (", ", args.Select (a => a.Value)), returnType);
			var encoded = GetEncodedJniSignature (binder, args, returnType);
			var margs   = args.Select (arg => new JniArgumentMarshalInfo (arg.Value, arg.LimitType)).ToList ();
			var jvalues = margs.Select (a => a.JValue).ToArray ();
			var result  = members.StaticMethods.CallMethod (encoded, jvalues);
			for (int i = 0; i < margs.Count; ++i) {
				margs [i].Cleanup (args [i]);
			}
			return result;
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
			sb.Append (JniEnvironment.Current.JavaVM.GetJniTypeInfoForType (returnType).ToString ());

			return sb.ToString ();
		}

		public object GetStaticFieldValue (string fieldName, Type fieldType)
		{
			Debug.WriteLine ("# DynamicJavaClass({0}).field({1}) as {2}", JniClassName, fieldName, fieldType);
			var typeInfo    = JniEnvironment.Current.JavaVM.GetJniTypeInfoForType (fieldType);
			var encoded     = fieldName + "\u0000" + typeInfo.ToString ();
			return members.StaticFields.GetValue (encoded);
		}

		public void SetStaticFieldValue (string fieldName, Type fieldType, object value)
		{
			Debug.WriteLine ("# DynamicJavaClass({0}).field({1}) as {2} = {3}", JniClassName, fieldName, fieldType, value);
			var typeInfo    = JniEnvironment.Current.JavaVM.GetJniTypeInfoForType (fieldType);
			var encoded     = fieldName + "\u0000" + typeInfo.ToString ();
			members.StaticFields.SetValue (encoded,  value);
		}

		internal StaticFieldAccess GetStaticFieldAccess (string fieldName)
		{
			return new StaticFieldAccess (this, fieldName);
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
			: base (parameter, BindingRestrictions.Empty, value)
		{
		}
	}

	class MetaStaticMemberAccessObject : DynamicMetaObject<DynamicJavaClass>
	{
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
//			Console.WriteLine ("InvokeMember of method={0}; ReturnType={1}; args={{{2}}}; CallInfo={3}", binder.Name, binder.ReturnType,
//				string.Join (", ", args.Select (a => string.Format ("{0} [{1}]", a.Expression.ToCSharpCode (), a.LimitType))), binder.CallInfo);

			Func<InvokeMemberBinder, DynamicMetaObject[], StaticMethodAccess> gsma = Value.GetStaticMethodAccess;

			var expr =
				Expression.Call (
					ExpressionAsT,
					gsma.Method,
					Expression.Constant (binder),
					Expression.Constant (args));
			return new DynamicMetaObject (
				expr,
				BindingRestrictions.GetTypeRestriction (
					expr,
					typeof (StaticMethodAccess)));
		}

		public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
		{
//			Console.WriteLine ("SetMember: Expression={0} [{1}]; property={2}; value.LimitType={3}; value.RuntimeType={4}; value.Value={5}", Expression.ToCSharpCode (), Expression.Type, binder.Name, value.LimitType, value.RuntimeType, value.Value);
			var self        = Value;
			var fieldValue  = value.Expression;
			if (!value.HasValue) {
				fieldValue  = binder.Defer (value).Expression;
			}
			fieldValue      = Expression.Convert (fieldValue, typeof (object));

			Action<string, Type, object> sfv    = self.SetStaticFieldValue;
			var expr = Expression.Block (
					Expression.Call (
						ExpressionAsT,
						sfv.Method,
						Expression.Constant (binder.Name),
						Expression.Constant (value.LimitType),
						fieldValue),
					Expression);
			return new DynamicMetaObject (expr, BindingRestrictions.GetTypeRestriction (Expression, typeof (DynamicJavaClass)));
		}

		public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
		{
//			Console.WriteLine ("GetMember: Expression={0} [{1}]; property={2}", Expression.ToCSharpCode (), Expression.Type, binder.Name);
			var expr =
				Expression.Call (
					ExpressionAsT,
					typeof (DynamicJavaClass).GetMethod ("GetStaticFieldAccess", BindingFlags.Instance | BindingFlags.NonPublic),
					Expression.Constant (binder.Name));
			return new DynamicMetaObject (
				expr,
				BindingRestrictions.GetTypeRestriction (
					expr,
					typeof (StaticFieldAccess)));
		}
	}

	class StaticMethodAccess : IDynamicMetaObjectProvider {

		public  DynamicJavaClass        JavaClass           {get; private set;}
		public  InvokeMemberBinder      InvokeBinder        {get; private set;}
		public  DynamicMetaObject[]     MethodArguments     {get; private set;}

		public StaticMethodAccess (DynamicJavaClass klass, InvokeMemberBinder invokeBinder, DynamicMetaObject[] args)
		{
			JavaClass       = klass;
			InvokeBinder    = invokeBinder;
			MethodArguments = args;
		}

		public DynamicMetaObject GetMetaObject(Expression parameter)
		{
//			Console.WriteLine ("# StaticMethodAccess.GetMetaObject: parameter={0}", parameter);
			return new MetaStaticMethodAccessObject (parameter, this);
		}
	}

	class MetaStaticMethodAccessObject : DynamicMetaObject<StaticMethodAccess> {

		public MetaStaticMethodAccessObject (Expression e, StaticMethodAccess value)
			: base (e, value)
		{
//			Console.WriteLine ("MetaStaticMethodAccessObject: e={0}", e.ToCSharpCode ());
		}

		public override DynamicMetaObject BindConvert (ConvertBinder binder)
		{
//			Console.WriteLine ("MetaStaticMethodAccessObject.Convert: Expression={0} [{1}]", Expression.ToCSharpCode (), Expression.Type);

			var method      = ExpressionAsT;
			var instance    = Expression.Property (method, "JavaClass");
			var mbinder     = Expression.Property (method, "InvokeBinder");
			var args        = Expression.Property (method, "MethodArguments");
			var csm         = typeof (DynamicJavaClass).GetMethod ("CallStaticMethod");
			var returnType  = Expression.Constant (binder.Type);

			var expr        = Expression.Convert (
					Expression.Call (instance, csm, mbinder, args, returnType),
					binder.Type);
			return new DynamicMetaObject (
					expr,
					BindingRestrictions.GetInstanceRestriction (Expression, Value));
		}
	}

	class StaticFieldAccess : IDynamicMetaObjectProvider {

		public  string              FieldName   {get; private set;}
		public  DynamicJavaClass    JavaClass   {get; private set;}

		public StaticFieldAccess (DynamicJavaClass klass, string fieldName)
		{
			JavaClass = klass;
			FieldName = fieldName;
		}

		public DynamicMetaObject GetMetaObject(Expression parameter)
		{
//			Console.WriteLine ("# FieldAccessInfo.GetMetaObject: parameter={0}", parameter);
			return new MetaStaticFieldAccessObject(parameter, this);
		}
	}

	class MetaStaticFieldAccessObject : DynamicMetaObject<StaticFieldAccess> {

		public MetaStaticFieldAccessObject (Expression e, StaticFieldAccess value)
			: base (e, value)
		{
//			Console.WriteLine ("MyHelperObject: e={0}", e.ToCSharpCode ());
		}

		public override DynamicMetaObject BindConvert (ConvertBinder binder)
		{
//			Console.WriteLine ("MetaStaticFieldAccessObject.Convert: Expression={0} [{1}]", Expression.ToCSharpCode (), Expression.Type);

			var field       = ExpressionAsT;
			var instance    = Expression.Property (field, "JavaClass");
			var fieldName   = Expression.Property (field, "FieldName");
			var gsfv        = typeof(DynamicJavaClass).GetMethod ("GetStaticFieldValue");
			var expr        = Expression.Convert (
					Expression.Call (instance, gsfv, fieldName, Expression.Constant (binder.Type)),
                    binder.Type);
			return new DynamicMetaObject (
					expr,
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

