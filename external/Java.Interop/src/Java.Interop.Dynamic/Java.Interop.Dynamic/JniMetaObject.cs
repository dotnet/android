using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Text;

using Mono.Linq.Expressions;

using Java.Interop;

namespace Java.Interop.Dynamic {

	abstract class JniMetaObject : DynamicMetaObject
	{
		protected   delegate    bool    TryInvokeMember     (IJavaPeerable self, JavaMethodBase[] overloads, DynamicMetaObject[] args, out object value);

		JavaClassInfo   info;

		public JniMetaObject (Expression parameter, object value, JavaClassInfo info)
			: base (parameter, BindingRestrictions.GetInstanceRestriction (parameter, value), value)
		{
			this.info       = info;
		}

		protected   abstract    bool                        Disposed            {get;}
		protected   abstract    JniObjectReference          ConversionTarget    {get;}
		protected   abstract    bool                        HasSelf             {get;}

		protected   abstract    Expression      GetSelf ();

		public override IEnumerable<string> GetDynamicMemberNames ()
		{
			if (info == null || info.Disposed)
				return new string[0];
			return info.Fields.Keys.Concat (
				info.Methods.Keys
			);
		}

		public override DynamicMetaObject BindConvert (ConvertBinder binder)
		{
			var vm = JniEnvironment.Runtime;
			if (binder.Type == typeof (Type)) {
				var type    = vm.GetTypeForJniTypeRefererence (info.JniClassName);
				var typeE   = Expression.Convert (Expression.Constant (type), binder.Type);
				return new DynamicMetaObject (typeE, BindingRestrictions.GetTypeRestriction (typeE, binder.Type), type);
			}

			var marshalInfo = vm.GetJniMarshalInfoForType (binder.Type);
			if (marshalInfo.GetValueFromJni == null)
				return binder.FallbackConvert (this);

			var r       = ConversionTarget;
			var value   = marshalInfo.GetValueFromJni (ref r, JniObjectReferenceOptions.CreateNewReference, binder.Type);
			var valueE  = Expression.Convert (Expression.Constant (value), binder.Type);
			return new DynamicMetaObject (valueE, BindingRestrictions.GetTypeRestriction (valueE, binder.Type), value);
		}

		public override DynamicMetaObject BindGetMember (GetMemberBinder binder)
		{
			List<JavaFieldInfo> overloads = GetFields (binder.Name);
			if (overloads == null)
				return binder.FallbackGetMember (this);

			if (Disposed) {
				return new DynamicMetaObject (ThrowObjectDisposedException (typeof (object)), BindingRestrictions.GetInstanceRestriction (Expression, Value));
			}

			var field = overloads.FirstOrDefault (f => f.IsStatic == true);

			Func<IJavaPeerable, object>   getValue  = field.GetValue;

			var e = Expression.Call (Expression.Constant (field), getValue.Method, GetSelf ());
			return new DynamicMetaObject (e, BindingRestrictions.GetInstanceRestriction (Expression, Value));
		}

		protected static Expression ThrowObjectDisposedException (Type type = null)
		{
			return Expression.Throw (Expression.Constant (new ObjectDisposedException (nameof (DynamicJavaClass))), type);
		}

		List<JavaFieldInfo> GetFields (string name)
		{
			if (info == null || info.Fields == null)
				return null;

			List<JavaFieldInfo> overloads;
			if (info.Fields.TryGetValue (name, out overloads))
				return overloads;

			return null;
		}

		public override DynamicMetaObject BindInvokeMember (InvokeMemberBinder binder, DynamicMetaObject[] args)
		{
			var overloads   = GetMethods (binder.Name);
			if (overloads == null)
				return binder.FallbackInvokeMember (this, args);

			if (Disposed) {
				return new DynamicMetaObject (ThrowObjectDisposedException (typeof (object)), BindingRestrictions.GetInstanceRestriction (Expression, Value));
			}

			var applicable  = overloads.Where (o => (o.IsStatic == !HasSelf) && o.ArgumentTypes.Count == args.Length).ToArray ();
			if (applicable.Length == 0)
				return binder.FallbackInvokeMember (this, args);

			TryInvokeMember   invoke  = info.TryInvokeMember;
			var value       = Expression.Parameter (typeof (object), "value");
			var fallback    = binder.FallbackInvokeMember (this, args);
			var call        = Expression.Block (
					new[]{value},
					Expression.Condition (
						test:       Expression.Call (Expression.Constant (info), invoke.Method, GetSelf (), Expression.Constant (applicable), Expression.Constant (args), value),
						ifTrue:     value,
						ifFalse:    fallback.Expression)
			);
			return new DynamicMetaObject (call, BindingRestrictions.GetInstanceRestriction (Expression, Value));
		}

		protected List<JavaMethodInfo> GetMethods (string name)
		{
			if (info == null || info.Methods == null)
				return null;

			List<JavaMethodInfo>  overloads;
			if (info.Methods.TryGetValue (name, out overloads))
				return overloads;

			return null;
		}

		public override DynamicMetaObject BindSetMember (SetMemberBinder binder, DynamicMetaObject value)
		{
			List<JavaFieldInfo> overloads = GetFields (binder.Name);
			if (overloads == null)
				return binder.FallbackSetMember (this, value);

			if (Disposed) {
				return new DynamicMetaObject (ThrowObjectDisposedException (), BindingRestrictions.GetInstanceRestriction (Expression, Value));
			}

			var field   = overloads.FirstOrDefault (f => f.IsStatic == true);

			Action<IJavaPeerable, object>  setValue  = field.SetValue;
			var e = Expression.Block (
					Expression.Call (Expression.Constant (field), setValue.Method,
						GetSelf (), Expression.Convert (value.Expression, typeof (object))),
					Expression);
			return new DynamicMetaObject (e, BindingRestrictions.GetInstanceRestriction (Expression, Value));
		}
	}
}
