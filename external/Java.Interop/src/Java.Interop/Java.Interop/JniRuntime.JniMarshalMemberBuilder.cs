#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Java.Interop {

	partial class JniRuntime {

		partial class CreationOptions {
			[Obsolete ("JniMarshalMemberBuilder is no longer supported. This property will be removed in a future release.")]
			public  bool                       UseMarshalMemberBuilder     {get; set;}
			[Obsolete ("JniMarshalMemberBuilder is no longer supported. This property will be removed in a future release.")]
			public  JniMarshalMemberBuilder?   MarshalMemberBuilder        {get; set;}
		}

		[Obsolete ("JniMarshalMemberBuilder is no longer supported. This property will be removed in a future release.")]
		public  JniMarshalMemberBuilder        MarshalMemberBuilder        {
			get => throw new NotSupportedException ("JniMarshalMemberBuilder is no longer supported.");
		}

		[Obsolete ("JniMarshalMemberBuilder is no longer supported. This class will be removed in a future release.")]
		public abstract class JniMarshalMemberBuilder : IDisposable, ISetRuntime
		{
			JniRuntime?             runtime;
			bool                    disposed;

			public JniRuntime  Runtime     {
				get => runtime ?? throw new NotSupportedException ();
			}

			protected JniMarshalMemberBuilder ()
			{
			}

			public virtual void OnSetRuntime (JniRuntime runtime)
			{
				if (disposed)
					throw new ObjectDisposedException (GetType ().Name);

				this.runtime = runtime;
			}

			public void Dispose ()
			{
				Dispose (false);
			}

			protected virtual void Dispose (bool disposing)
			{
				disposed = true;
			}

			public  Delegate                                                CreateMarshalToManagedDelegate (Delegate value)
			{
				throw new NotSupportedException ("JniMarshalMemberBuilder is no longer supported.");
			}

			public  abstract    LambdaExpression                            CreateMarshalToManagedExpression (MethodInfo method);
			public  abstract    IEnumerable<JniNativeMethodRegistration>    GetExportedMemberRegistrations (Type declaringType);

			public  abstract    Expression<Func<ConstructorInfo, JniObjectReference, object?[]?, object>>   CreateConstructActivationPeerExpression (ConstructorInfo constructor);

			public  Func<ConstructorInfo, JniObjectReference, object?[]?, object>                           CreateConstructActivationPeerFunc (ConstructorInfo constructor)
			{
				throw new NotSupportedException ("JniMarshalMemberBuilder is no longer supported.");
			}

			public string GetJniMethodSignature (MethodBase member)
			{
				throw new NotSupportedException ("JniMarshalMemberBuilder is no longer supported.");
			}

			public JniValueMarshaler GetParameterMarshaler (ParameterInfo parameter)
			{
				throw new NotSupportedException ("JniMarshalMemberBuilder is no longer supported.");
			}

			// Heuristic: if first two parameters are IntPtr, this is a "direct" wrapper.
			public bool IsDirectMethod (ParameterInfo[] methodParameters)
			{
				return methodParameters?.Length >= 2 &&
					methodParameters [0].ParameterType == typeof (IntPtr) &&
					methodParameters [1].ParameterType == typeof (IntPtr);
			}
		}
	}
}

