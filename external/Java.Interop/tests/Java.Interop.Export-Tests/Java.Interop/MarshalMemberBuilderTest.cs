using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Java.Interop;

using Mono.Linq.Expressions;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	class MarshalMemberBuilderTest : JavaVMFixture
	{
		[Test]
		public void AddExportMethods ()
		{
			using (var t = CreateExportTestType ()) {
				var methods = CreateBuilder ()
					.GetExportedMemberRegistrations (typeof (ExportTest))
					.ToList ();
				Assert.AreEqual (10, methods.Count);

				Assert.AreEqual ("action",  methods [0].Name);
				Assert.AreEqual ("()V",     methods [0].Signature);
				Assert.IsTrue (methods [0].Marshaler is Action<IntPtr, IntPtr>);

				Assert.AreEqual ("staticAction",    methods [1].Name);
				Assert.AreEqual ("()V",             methods [1].Signature);
				Assert.IsTrue (methods [1].Marshaler is Action<IntPtr, IntPtr>);

				var m = t.GetStaticMethod ("testStaticMethods", "()V");
				JniEnvironment.StaticMethods.CallStaticVoidMethod (t.PeerReference, m);
				Assert.IsTrue (ExportTest.StaticHelloCalled);
				Assert.IsTrue (ExportTest.StaticActionInt32StringCalled);

				using (var o = CreateExportTest (t)) {
					var n = t.GetInstanceMethod ("testMethods", "()V");
					JniEnvironment.InstanceMethods.CallVoidMethod (o.PeerReference, n);
					Assert.IsTrue (o.HelloCalled);
					o.Dispose ();
				}
			}
		}

		static MarshalMemberBuilder CreateBuilder ()
		{
			return new MarshalMemberBuilder (JniRuntime.CurrentRuntime);
		}

		static JniType CreateExportTestType ()
		{
			return new JniType ("com/xamarin/interop/export/ExportType");
		}

		static unsafe ExportTest CreateExportTest (JniType type)
		{
			var c = type.GetConstructor ("()V");
			var p = type.NewObject (c, null);
			return new ExportTest (ref p, JniObjectReferenceOptions.CopyAndDispose);
		}

		[Test]
		public void GetExportedMemberRegistrations_NullChecks ()
		{
			var builder = CreateBuilder ();
			Assert.Throws<ArgumentNullException> (() => builder.GetExportedMemberRegistrations (null));
		}

		[Test]
		public void GetJniMethodSignature_NullChecks ()
		{
			var builder = CreateBuilder ();
			Action a    = () => {};
			Assert.Throws<ArgumentNullException> (() => builder.GetJniMethodSignature (null, a.Method));
			Assert.Throws<ArgumentNullException> (() => builder.GetJniMethodSignature (new JavaCallableAttribute (), null));
		}

		[Test]
		public void GetJniMethodSignature ()
		{
			var builder = CreateBuilder ();
			Action a    = () => {};
			var export  = new JavaCallableAttribute () {
				Signature = "(I)V",
			};
			// Note: no validation between actual MethodInfo & existing signature
			// Validation would be done by CreateMarshalFromJniMethodRegistration().
			Assert.AreEqual ("(I)V", builder.GetJniMethodSignature (export, a.Method));
			Assert.AreEqual ("(I)V", export.Signature);

			export = new JavaCallableAttribute () {
				Signature = null,
			};
			Assert.AreEqual ("()V", builder.GetJniMethodSignature (export, a.Method));
			// Note: export.Signature updated
			Assert.AreEqual ("()V", export.Signature);

			Action<string> s    = v => {};
			Assert.AreEqual ("(Ljava/lang/String;)V", builder.GetJniMethodSignature (new JavaCallableAttribute (), s.Method));

			Action<IntPtr, IntPtr, string> ds   = (e, c, v) => {};
			Assert.AreEqual ("(Ljava/lang/String;)V", builder.GetJniMethodSignature (new JavaCallableAttribute (), ds.Method));

			Func<string> fs     = () => null;
			Assert.AreEqual ("()Ljava/lang/String;", builder.GetJniMethodSignature (new JavaCallableAttribute (), fs.Method));

			Func<IntPtr, IntPtr, string> dfs     = (e, c) => null;
			Assert.AreEqual ("()Ljava/lang/String;", builder.GetJniMethodSignature (new JavaCallableAttribute (), dfs.Method));

			// Note: AppDomain currently has no builtin marshaling defaults
			// TODO: but should it? We could default wrap to JavaProxyObject...?
			Action<AppDomain> aad    = v => {};
			Assert.Throws<NotSupportedException> (() => builder.GetJniMethodSignature (new JavaCallableAttribute (), aad.Method));

			Func<AppDomain> fad    = () => null;
			Assert.Throws<NotSupportedException> (() => builder.GetJniMethodSignature (new JavaCallableAttribute (), fad.Method));
		}

		[Test]
		public void CreateMarshalToManagedExpression_NullChecks ()
		{
			Action a    = ExportTest.StaticAction;
			var builder = CreateBuilder ();
			Assert.Throws<ArgumentNullException> (() => builder.CreateMarshalToManagedExpression (null));
			builder.CreateMarshalToManagedExpression (a.Method, null);
			builder.CreateMarshalToManagedExpression (a.Method, null, null);
		}

		[Test]
		public void CreateMarshalToManagedExpression_SignatureMismatch ()
		{
			Action<int, string> a   = ExportTest.StaticActionInt32String;
			var builder             = CreateBuilder ();

			// Parameter count mismatch: 0 != 2
			Assert.Throws<ArgumentException>(() => builder.CreateMarshalToManagedExpression (
					a.Method, new JavaCallableAttribute () { Signature = "()V" }, a.Method.DeclaringType));
			// Parameter count mismatch: 1 != 2
			Assert.Throws<ArgumentException>(() => builder.CreateMarshalToManagedExpression (
					a.Method, new JavaCallableAttribute () { Signature = "(I)V" }, a.Method.DeclaringType));
			// Parameter type mismatch: (int, int) != (int, string)
			Assert.Throws<ArgumentException>(() => builder.CreateMarshalToManagedExpression (
					a.Method, new JavaCallableAttribute () { Signature = "(II)V" }, a.Method.DeclaringType));
			// return type mismatch: int != void
			Assert.Throws<ArgumentException>(() => builder.CreateMarshalToManagedExpression (
					a.Method, new JavaCallableAttribute () { Signature = "(ILjava/lang/String;)I" }, a.Method.DeclaringType));
			// invalid JNI signatures
			Assert.Throws<ArgumentException>(() => builder.CreateMarshalToManagedExpression (
					a.Method, new JavaCallableAttribute () { Signature = "(IL)I" }, a.Method.DeclaringType));
			Assert.Throws<ArgumentException>(() => builder.CreateMarshalToManagedExpression (
					a.Method, new JavaCallableAttribute () { Signature = "(I[)I" }, a.Method.DeclaringType));
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_InstanceAction ()
		{
			var t = typeof (ExportTest);
			var m = t.GetMethod ("InstanceAction");
			CheckCreateMarshalToManagedExpression (null, t, m, typeof (Action<IntPtr, IntPtr>),
					@"void (IntPtr __jnienv, IntPtr __this)
{
	JniTransition __envp;
	JniRuntime __jvm;
	JniValueManager __vm;
	ExportTest __this_val;

	__envp = new JniTransition(__jnienv);
	try
	{
		__jvm = JniEnvironment.Runtime;
		__vm = __jvm.ValueManager;
		__vm.WaitForGCBridgeProcessing();
		__this_val = __vm.GetValue<ExportTest>(__this);
		__this_val.InstanceAction();
	}
	catch (Exception __e) if (__jvm.ExceptionShouldTransitionToJni(__e))
	{
		__envp.SetPendingException(__e);
	}
	finally
	{
		__envp.Dispose();
	}
}");
		}

		static void CheckCreateMarshalToManagedExpression (JavaCallableAttribute export, Type type, MethodInfo method, Type expectedDelegateType, string expectedBody)
		{
			export = export ?? new JavaCallableAttribute ();
			var b = CreateBuilder ();
			var l = b.CreateMarshalToManagedExpression (method, export, type);
			CheckExpression (l, method.Name, expectedDelegateType, expectedBody);
		}

		static void CheckExpression (LambdaExpression expression, string memberName, Type expressionType, string expectedBody)
		{
			Console.WriteLine ("## member: {0}", memberName);
			Console.WriteLine (expression.ToCSharpCode ());
			var da = AppDomain.CurrentDomain.DefineDynamicAssembly(
				new AssemblyName("dyn"), // call it whatever you want
				System.Reflection.Emit.AssemblyBuilderAccess.Save,
				Path.GetDirectoryName (typeof (MarshalMemberBuilderTest).Assembly.Location));

			var _name = "dyn-" + memberName + ".dll";
			var dm = da.DefineDynamicModule("dyn_mod", _name);
			var dt = dm.DefineType ("dyn_type", TypeAttributes.Public);
			var mb = dt.DefineMethod(
				memberName,
				MethodAttributes.Public | MethodAttributes.Static);

			expression.CompileToMethod (mb);
			dt.CreateType();
			Assert.AreEqual (expressionType,    expression.Type);
			Assert.AreEqual (expectedBody,      expression.ToCSharpCode ());
#if !__ANDROID__
			da.Save (_name);
#endif  // !__ANDROID__
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_StaticAction ()
		{
			var t       = typeof (ExportTest);
			Action a    = ExportTest.StaticAction;
			CheckCreateMarshalToManagedExpression (null, t, a.Method, typeof(Action<IntPtr, IntPtr>),
					@"void (IntPtr __jnienv, IntPtr __class)
{
	JniTransition __envp;
	JniRuntime __jvm;

	__envp = new JniTransition(__jnienv);
	try
	{
		__jvm = JniEnvironment.Runtime;
		__jvm.ValueManager.WaitForGCBridgeProcessing();
		ExportTest.StaticAction();
	}
	catch (Exception __e) if (__jvm.ExceptionShouldTransitionToJni(__e))
	{
		__envp.SetPendingException(__e);
	}
	finally
	{
		__envp.Dispose();
	}
}");
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_StaticActionIJavaLangObject ()
		{
			var t                 = typeof (ExportTest);
			var m = t.GetMethod ("StaticActionIJavaObject");
			CheckCreateMarshalToManagedExpression (null, t, m, typeof(Action<IntPtr, IntPtr, IntPtr>),
					@"void (IntPtr __jnienv, IntPtr __class, IntPtr test)
{
	JniTransition __envp;
	JniRuntime __jvm;
	JniValueManager __vm;
	JavaObject test_val;

	__envp = new JniTransition(__jnienv);
	try
	{
		__jvm = JniEnvironment.Runtime;
		__vm = __jvm.ValueManager;
		__vm.WaitForGCBridgeProcessing();
		test_val = __vm.GetValue<JavaObject>(test);
		ExportTest.StaticActionIJavaObject(test_val);
	}
	catch (Exception __e) if (__jvm.ExceptionShouldTransitionToJni(__e))
	{
		__envp.SetPendingException(__e);
	}
	finally
	{
		__envp.Dispose();
	}
}");
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_InstanceActionIJavaLangObject ()
		{
			var t                 = typeof (ExportTest);
			var m = t.GetMethod ("InstanceActionIJavaObject");
			CheckCreateMarshalToManagedExpression (null, t, m, typeof(Action<IntPtr, IntPtr, IntPtr>),
					@"void (IntPtr __jnienv, IntPtr __this, IntPtr test)
{
	JniTransition __envp;
	JniRuntime __jvm;
	JniValueManager __vm;
	ExportTest __this_val;
	JavaObject test_val;

	__envp = new JniTransition(__jnienv);
	try
	{
		__jvm = JniEnvironment.Runtime;
		__vm = __jvm.ValueManager;
		__vm.WaitForGCBridgeProcessing();
		__this_val = __vm.GetValue<ExportTest>(__this);
		test_val = __vm.GetValue<JavaObject>(test);
		__this_val.InstanceActionIJavaObject(test_val);
	}
	catch (Exception __e) if (__jvm.ExceptionShouldTransitionToJni(__e))
	{
		__envp.SetPendingException(__e);
	}
	finally
	{
		__envp.Dispose();
	}
}");
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_StaticActionInt32String ()
		{
			var t = typeof (ExportTest);
			var m = ((Action<int, string>) ExportTest.StaticActionInt32String);
			var e = new JavaCallableAttribute () {
				Signature = "(ILjava/lang/String;)V",
			};
			CheckCreateMarshalToManagedExpression (e, t, m.Method, typeof (Action<IntPtr, IntPtr, int, IntPtr>),
					@"void (IntPtr __jnienv, IntPtr __class, int i, IntPtr v)
{
	JniTransition __envp;
	JniRuntime __jvm;
	string v_val;

	__envp = new JniTransition(__jnienv);
	try
	{
		__jvm = JniEnvironment.Runtime;
		__jvm.ValueManager.WaitForGCBridgeProcessing();
		v_val = Strings.ToString(v);
		ExportTest.StaticActionInt32String(i, v_val);
	}
	catch (Exception __e) if (__jvm.ExceptionShouldTransitionToJni(__e))
	{
		__envp.SetPendingException(__e);
	}
	finally
	{
		__envp.Dispose();
	}
}");
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_StaticFuncMyLegacyColorMyColor_MyColor ()
		{
			var t = typeof (ExportTest);
			var m = ((Func<MyLegacyColor, MyColor, MyColor>) ExportTest.StaticFuncMyLegacyColorMyColor_MyColor);
			var e = new JavaCallableAttribute () {
				Signature = "(II)I",
			};
			CheckCreateMarshalToManagedExpression (e, t, m.Method, typeof (Func<IntPtr, IntPtr, int, int, int>),
					@"int (IntPtr __jnienv, IntPtr __class, int color1, int color2)
{
	JniTransition __envp;
	JniRuntime __jvm;
	MyColor __mret;
	MyLegacyColor color1_val;
	MyColor color2_val;
	int __mret_p;

	__envp = new JniTransition(__jnienv);
	try
	{
		__jvm = JniEnvironment.Runtime;
		__jvm.ValueManager.WaitForGCBridgeProcessing();
		color1_val = new MyLegacyColor(color1);
		color2_val = new MyColor(color2);
		__mret = ExportTest.StaticFuncMyLegacyColorMyColor_MyColor(color1_val, color2_val);
		__mret_p = __mret.Value;
		return __mret_p;
	}
	catch (Exception __e) if (__jvm.ExceptionShouldTransitionToJni(__e))
	{
		__envp.SetPendingException(__e);
		return default(int);
	}
	finally
	{
		__envp.Dispose();
	}
}");
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_FuncInt64 ()
		{
			var t = typeof (ExportTest);
			var m = t.GetMethod ("FuncInt64");
			var e = new JavaCallableAttribute () {
				Signature = "()J",
			};
			CheckCreateMarshalToManagedExpression (e, t, m, typeof (Func<IntPtr, IntPtr, long>),
					@"long (IntPtr __jnienv, IntPtr __this)
{
	JniTransition __envp;
	JniRuntime __jvm;
	JniValueManager __vm;
	long __mret;
	ExportTest __this_val;

	__envp = new JniTransition(__jnienv);
	try
	{
		__jvm = JniEnvironment.Runtime;
		__vm = __jvm.ValueManager;
		__vm.WaitForGCBridgeProcessing();
		__this_val = __vm.GetValue<ExportTest>(__this);
		__mret = __this_val.FuncInt64();
		return __mret;
	}
	catch (Exception __e) if (__jvm.ExceptionShouldTransitionToJni(__e))
	{
		__envp.SetPendingException(__e);
		return default(long);
	}
	finally
	{
		__envp.Dispose();
	}
}");
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_FuncIJavaObject ()
		{
			var t = typeof (ExportTest);
			var m = t.GetMethod ("FuncIJavaObject");
			var e = new JavaCallableAttribute () {
				Signature = "()Ljava/lang/Object;",
			};
			CheckCreateMarshalToManagedExpression (e, t, m, typeof (Func<IntPtr, IntPtr, IntPtr>),
					@"IntPtr (IntPtr __jnienv, IntPtr __this)
{
	JniTransition __envp;
	JniRuntime __jvm;
	JniValueManager __vm;
	JavaObject __mret;
	ExportTest __this_val;
	JniObjectReference __mret_ref;
	IntPtr __mret_rtn;

	__envp = new JniTransition(__jnienv);
	try
	{
		__jvm = JniEnvironment.Runtime;
		__vm = __jvm.ValueManager;
		__vm.WaitForGCBridgeProcessing();
		__this_val = __vm.GetValue<ExportTest>(__this);
		__mret = __this_val.FuncIJavaObject();
		if (null == __mret)
		{
			return __mret_ref = new JniObjectReference();
		}
		else
		{
			return __mret_ref = (IJavaPeerable)__mret.PeerReference;
		}
		__mret_rtn = References.NewReturnToJniRef(__mret_ref);
		return __mret_rtn;
	}
	catch (Exception __e) if (__jvm.ExceptionShouldTransitionToJni(__e))
	{
		__envp.SetPendingException(__e);
		return default(IntPtr);
	}
	finally
	{
		__envp.Dispose();
	}
}");
		}

		static void DirectInvocation (IntPtr jnienv, IntPtr context)
		{
		}

		[Test]
		public void CreateMarshalToManagedExpression_DirectMethod ()
		{
			Action<IntPtr, IntPtr> a = DirectInvocation;
			var e = new JavaCallableAttribute () {
				Signature = "()V",
			};
			CheckCreateMarshalToManagedExpression (e, a.Method.DeclaringType, a.Method, typeof (Action<IntPtr, IntPtr>),
				@"void (IntPtr jnienv, IntPtr context)
{
	JniTransition __envp;
	JniRuntime __jvm;

	__envp = new JniTransition(jnienv);
	try
	{
		__jvm = JniEnvironment.Runtime;
		__jvm.ValueManager.WaitForGCBridgeProcessing();
		MarshalMemberBuilderTest.DirectInvocation(jnienv, context);
	}
	catch (Exception __e) if (__jvm.ExceptionShouldTransitionToJni(__e))
	{
		__envp.SetPendingException(__e);
	}
	finally
	{
		__envp.Dispose();
	}
}");
		}

		[Test]
		public void CreateConstructActivationPeerExpression_Exceptions ()
		{
			var b   = CreateBuilder ();
			Assert.Throws<ArgumentNullException> (() => b.CreateConstructActivationPeerExpression (null));
		}

		[Test]
		public void CreateConstructActivationPeerExpression ()
		{
			var b   = CreateBuilder ();
			var c   = typeof (MarshalMemberBuilderTest).GetConstructor (new Type [0]);
			var e   = b.CreateConstructActivationPeerExpression (c);
			CheckExpression (e,
					"ExportedMemberBuilderTest_ctor",
					typeof(Func<ConstructorInfo, JniObjectReference, object[], object>),
					@"object (ConstructorInfo constructor, JniObjectReference reference, object[] parameters)
{
	Type type;
	object self;

	type = constructor.DeclaringType;
	self = FormatterServices.GetUninitializedObject(type);
	(IJavaPeerable)self.SetPeerReference(reference);
	constructor.Invoke(self, parameters);
	return self;
}");
		}
	}
}
