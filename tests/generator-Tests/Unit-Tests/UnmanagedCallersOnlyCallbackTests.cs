using System;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MonoDroid.Generation;

namespace generatortests
{
	/// <summary>
	/// Tests for the experimental direct-<c>[UnmanagedCallersOnly]</c> binding callback shape
	/// enabled by <c>--lang-features=unmanaged-callers-only-callbacks</c>.
	/// </summary>
	[TestClassAttribute]
	class UnmanagedCallersOnlyCallbackTests : CodeGeneratorTestBase
	{
		protected override CodeGenerationOptions CreateOptions () => new CodeGenerationOptions {
			SupportNullableReferenceTypes = true,
			UseUnmanagedCallersOnlyCallbacks = true,
			UseGlobal = true,
		};

		const string Api = """
			<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.example' jni-name='com/example'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' final='false' name='Peer' static='false' visibility='public' jni-signature='Lcom/example/Peer;' />
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' final='false' name='Widget' static='false' visibility='public' jni-signature='Lcom/example/Widget;'>
			      <method abstract='false' deprecated='not deprecated' final='false' name='onLayout' jni-signature='(ZIIII)V' return='void' static='false' visibility='public'>
			        <parameter name='changed' type='boolean' jni-type='Z' />
			        <parameter name='left' type='int' jni-type='I' />
			        <parameter name='top' type='int' jni-type='I' />
			        <parameter name='right' type='int' jni-type='I' />
			        <parameter name='bottom' type='int' jni-type='I' />
			      </method>
			      <method abstract='false' deprecated='not deprecated' final='false' name='attach' jni-signature='(Lcom/example/Peer;I)Lcom/example/Peer;' return='com.example.Peer' static='false' visibility='public'>
			        <parameter name='peer' type='com.example.Peer' jni-type='Lcom/example/Peer;' />
			        <parameter name='flags' type='int' jni-type='I' />
			      </method>
			      <method abstract='false' deprecated='not deprecated' final='false' name='describe' jni-signature='(Ljava/lang/String;)Ljava/lang/String;' return='java.lang.String' static='false' visibility='public'>
			        <parameter name='label' type='java.lang.String' jni-type='Ljava/lang/String;' />
			      </method>
			      <method abstract='false' deprecated='not deprecated' final='false' name='count' jni-signature='()I' return='int' static='false' visibility='public' />
			    </class>
			  </package>
			</api>
			""";

		string GenerateWidget ()
		{
			var gens = ParseApiDefinition (Api);
			return GetGeneratedTypeOutput (gens.Single (g => g.Name == "Widget"));
		}

		[TestMethodAttribute]
		public void CallbacksAreUnmanagedCallersOnlyAndDropConnectors ()
		{
			var source = GenerateWidget ();

			Assert.IsTrue (source.Contains ("[global::System.Runtime.InteropServices.UnmanagedCallersOnly]"), source);

			// No `cb_*` delegate cache fields and no `Get*Handler ()` connector methods.
			Assert.IsFalse (source.Contains ("cb_onLayout"), source);
			Assert.IsFalse (source.Contains ("static Delegate GetOnLayout_ZIIIIHandler ()"), source);
			Assert.IsFalse (source.Contains ("static Delegate Get"), source);

			// There is no connector method left to name, so the connector stores the callback name.
			Assert.IsTrue (source.Contains ("\"onLayout\", \"(ZIIII)V\", \"n_OnLayout\""), source);
		}

		[TestMethodAttribute]
		public void AllScalarCallbackUsesTypedHelper ()
		{
			var source = GenerateWidget ();

			// boolean marshals as a blittable `sbyte`, so the shape is 5 scalars returning void.
			Assert.IsTrue (source.Contains (
				"global::Java.Interop.JniMarshalTyped.Invoke_SSSSSX<global::Com.Example.Widget, sbyte, int, int, int, int> (jnienv, native__this, native_changed, left, top, right, bottom, &global::Com.Example.Widget.m3);"),
				source);

			// The function-pointer target contains only the projection of the JNI scalars and the
			// managed member invocation -- no JNI transition, no peer lookup, no exception handling.
			Assert.IsTrue (source.Contains (
				"private static void m3 (global::Com.Example.Widget __this, sbyte native_changed, int left, int top, int right, int bottom)"),
				source);
			Assert.IsTrue (source.Contains ("var changed = native_changed != 0;"), source);
			Assert.IsTrue (source.Contains ("__this.OnLayout (changed, left, top, right, bottom);"), source);
		}

		[TestMethodAttribute]
		public void PeerArgumentAndPeerReturnUseTypedHelper ()
		{
			var source = GenerateWidget ();

			// The peer return is not a type argument: bound methods frequently have covariant
			// returns, so the target is typed as IJavaObject. That also keeps the MethodSpec smaller.
			Assert.IsTrue (source.Contains (
				"global::Java.Interop.JniMarshalTyped.Invoke_OSO<global::Com.Example.Widget, global::Com.Example.Peer, int> (jnienv, native__this, native_peer, flags, &global::Com.Example.Widget.m0);"),
				source);

			// The helper owns both the argument peer lookup and the JNI conversion of the result,
			// so the target neither calls GetObject<T> () nor ToLocalJniHandle ().
			Assert.IsTrue (source.Contains (
				"private static global::Android.Runtime.IJavaObject? m0 (global::Com.Example.Widget __this, global::Com.Example.Peer? peer, int flags)"),
				source);
			Assert.IsTrue (source.Contains ("return __this.Attach (peer, flags);"), source);
			Assert.IsFalse (source.Contains ("m0 (IntPtr jnienv"), source);
		}

		[TestMethodAttribute]
		public void StringShapesFallBackToRawSafeInvoke ()
		{
			var source = GenerateWidget ();

			// Strings need JNI string marshaling the shared typed helper cannot own, so the callback
			// stays [UnmanagedCallersOnly] but forwards to the raw SafeInvoke helper.
			Assert.IsTrue (source.Contains (
				"global::Java.Interop.JniMarshal.SafeInvokeFunc (jnienv, native__this, native_label, &global::Com.Example.Widget.m2);"),
				source);
			Assert.IsFalse (source.Contains ("WaitForBridgeProcessing"), source);
			Assert.IsTrue (source.Contains (
				"private static unsafe IntPtr m2 (IntPtr jnienv, IntPtr native__this, IntPtr native_label)"),
				source);
			Assert.IsTrue (source.Contains ("global::Java.Lang.Object.GetObject<global::Com.Example.Widget> (jnienv, native__this, JniHandleOwnership.DoNotTransfer)"), source);
		}

		[TestMethodAttribute]
		public void ScalarReturnUsesTypedHelper ()
		{
			var source = GenerateWidget ();

			Assert.IsTrue (source.Contains (
				"global::Java.Interop.JniMarshalTyped.Invoke_S<global::Com.Example.Widget, int> (jnienv, native__this, &global::Com.Example.Widget.m1);"),
				source);
			Assert.IsTrue (source.Contains ("private static int m1 (global::Com.Example.Widget __this)"), source);
		}

		[TestMethodAttribute]
		public void DefaultsAreUnchangedWhenFlagIsOff ()
		{
			options = new CodeGenerationOptions {
				SupportNullableReferenceTypes = true,
				UseGlobal = true,
			};
			generator = new JavaInteropCodeGenerator (writer, options);

			var source = GenerateWidget ();

			Assert.IsFalse (source.Contains ("UnmanagedCallersOnly"), source);
			Assert.IsFalse (source.Contains ("JniMarshalTyped"), source);
			Assert.IsTrue (source.Contains ("cb_onLayout"), source);
			Assert.IsTrue (source.Contains ("static Delegate GetOnLayout_ZIIIIHandler ()"), source);
		}
		/// <summary>
		/// A class with several overloads of the same Java method, plus an interface whose members
		/// collide with a class member name, to exercise the disambiguation rules.
		/// </summary>
		const string OverloadApi = """
			<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.example' jni-name='com/example'>
			    <interface abstract='true' deprecated='not deprecated' final='false' name='Listener' static='false' visibility='public' jni-signature='Lcom/example/Listener;'>
			      <method abstract='true' deprecated='not deprecated' final='false' name='remove' jni-signature='(I)V' return='void' static='false' visibility='public'>
			        <parameter name='index' type='int' jni-type='I' />
			      </method>
			      <method abstract='true' deprecated='not deprecated' final='false' name='remove' jni-signature='(J)V' return='void' static='false' visibility='public'>
			        <parameter name='id' type='long' jni-type='J' />
			      </method>
			    </interface>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' final='false' name='Registry' static='false' visibility='public' jni-signature='Lcom/example/Registry;'>
			      <method abstract='false' deprecated='not deprecated' final='false' name='remove' jni-signature='(I)V' return='void' static='false' visibility='public'>
			        <parameter name='index' type='int' jni-type='I' />
			      </method>
			      <method abstract='false' deprecated='not deprecated' final='false' name='remove' jni-signature='(J)V' return='void' static='false' visibility='public'>
			        <parameter name='id' type='long' jni-type='J' />
			      </method>
			      <method abstract='false' deprecated='not deprecated' final='false' name='remove' jni-signature='(Z)V' return='void' static='false' visibility='public'>
			        <parameter name='flag' type='boolean' jni-type='Z' />
			      </method>
			      <method abstract='false' deprecated='not deprecated' final='false' name='clear' jni-signature='()V' return='void' static='false' visibility='public' />
			    </class>
			  </package>
			</api>
			""";

		string GenerateOverloads (string type = "Registry")
		{
			var gens = ParseApiDefinition (OverloadApi);
			return GetGeneratedTypeOutput (gens.Single (g => g.Name == type));
		}

		/// <summary>
		/// Generates <paramref name="type" /> from a completely fresh generator, so that a second
		/// call shares no state at all with the first.
		/// </summary>
		string GenerateFromScratch (string type)
		{
			builder = new System.Text.StringBuilder ();
			writer = new System.IO.StringWriter (builder);
			options = CreateOptions ();
			generator = new JavaInteropCodeGenerator (writer, options);

			return GenerateOverloads (type);
		}

		[TestMethodAttribute]
		public void CallbackNamesDoNotContainJavaSignatures ()
		{
			var source = GenerateWidget ();

			foreach (var name in new [] { "n_OnLayout", "n_Attach", "n_Describe", "n_Count" })
				Assert.IsTrue (source.Contains ($"static {(name == "n_Count" ? "int" : "void")} {name} (IntPtr jnienv") || source.Contains ($" {name} (IntPtr jnienv"), $"missing {name}\n{source}");

			// None of the escaped Java signatures the legacy format appends survive.
			Assert.IsFalse (source.Contains ("n_OnLayout_ZIIII"), source);
			Assert.IsFalse (source.Contains ("_Lcom_example_Peer_"), source);
			Assert.IsFalse (source.Contains ("_Ljava_lang_String_"), source);
		}

		[TestMethodAttribute]
		public void DuplicateManagedNamesAreNumberedDeterministically ()
		{
			var source = GenerateOverloads ();

			// Ordering is by the member's own signature, not by its position in the API
			// description, so (I)V < (J)V < (Z)V.
			Assert.IsTrue (source.Contains ("\"remove\", \"(I)V\", \"n_Remove\")"), source);
			Assert.IsTrue (source.Contains ("\"remove\", \"(J)V\", \"n_Remove_1\")"), source);
			Assert.IsTrue (source.Contains ("\"remove\", \"(Z)V\", \"n_Remove_2\")"), source);

			// A name which is unique needs no suffix, and the numbering of an unrelated group does
			// not shift it.
			Assert.IsTrue (source.Contains ("\"clear\", \"()V\", \"n_Clear\")"), source);

			foreach (var name in new [] { "n_Remove", "n_Remove_1", "n_Remove_2", "n_Clear" })
				Assert.IsTrue (source.Contains ($"static unsafe void {name} (IntPtr jnienv"), $"missing {name}\n{source}");
		}

		[TestMethodAttribute]
		public void FunctionPointerTargetsAreOpaquePerTypeOrdinals ()
		{
			var source = GenerateOverloads ();

			// Ordinals are assigned in the same deterministic order as the callbacks: Clear first
			// (group names are ordered), then the three Remove overloads.
			Assert.IsTrue (source.Contains ("private static void m0 (global::Com.Example.Registry __this)"), source);
			Assert.IsTrue (source.Contains ("&global::Com.Example.Registry.m0);"), source);

			foreach (var target in new [] { "m1", "m2", "m3" })
				Assert.IsTrue (source.Contains ($"&global::Com.Example.Registry.{target});"), $"missing &{target}\n{source}");

			Assert.IsFalse (source.Contains ("__n_"), source);
		}

		[TestMethodAttribute]
		public void FunctionPointerTargetsDoNotCollideWithBoundMembers ()
		{
			const string api = """
				<api>
				  <package name='java.lang'><class name='Object' visibility='public' /></package>
				  <package name='com.example'>
				    <class name='Widget' extends='java.lang.Object' visibility='public' abstract='false'>
				      <method name='reserved' managedName='m0' return='void' visibility='public' abstract='false' final='false' static='false' />
				      <method name='reservedCallback' managedName='n_Work' return='void' visibility='public' abstract='false' final='false' static='false' />
				      <method name='work' return='void' visibility='public' abstract='false' final='false' static='false' />
				    </class>
				  </package>
				</api>
				""";
			var source = GetGeneratedTypeOutput (ParseApiDefinition (api).Single (g => g.Name == "Widget"));

			CompileCallbacks (source);
			Assert.IsTrue (source.Contains ("public virtual void m0 ()"), source);
			Assert.IsTrue (source.Contains ("&global::Com.Example.Widget.m0_1);"), source);
			Assert.IsTrue (source.Contains ("private static void m0_1 (global::Com.Example.Widget __this)"), source);
			Assert.IsTrue (source.Contains ("[Register (\"work\", \"()V\", \"n_Work_1\")]"), source);
			Assert.IsTrue (source.Contains ("static unsafe void n_Work_1 (IntPtr jnienv"), source);
		}

		[TestMethodAttribute]
		public void GenerationIsRepeatable ()
		{
			// A second generator run over the same API description must produce byte-identical
			// output: nothing in the allocation may depend on a counter that survives a run or on
			// the order in which writers were constructed.
			Assert.AreEqual (GenerateFromScratch ("Registry"), GenerateFromScratch ("Registry"));
			Assert.AreEqual (GenerateFromScratch ("IListener"), GenerateFromScratch ("IListener"));
		}

		[TestMethodAttribute]
		public void InterfaceConnectorsKeepTheirOwnerQualifier ()
		{
			var source = GenerateOverloads ("IListener");

			// The compact name replaces only the Get*Handler segment; the invoker qualifier which
			// tells a reader which type declares the callback is preserved verbatim.
			Assert.IsTrue (source.Contains ("\"remove\", \"(I)V\", \"n_Remove:Com.Example.IListenerInvoker"), source);
			Assert.IsTrue (source.Contains ("\"remove\", \"(J)V\", \"n_Remove_1:Com.Example.IListenerInvoker"), source);

			// ... and the invoker really does declare callbacks under those names.
			Assert.IsTrue (source.Contains ("static unsafe void n_Remove (IntPtr jnienv"), source);
			Assert.IsTrue (source.Contains ("static unsafe void n_Remove_1 (IntPtr jnienv"), source);
		}

		[TestMethodAttribute]
		public void JavaNamesAndSignaturesAreUnaffected ()
		{
			var source = GenerateOverloads ();

			// Renaming managed callback infrastructure must not disturb the Java method name or
			// JNI signature a JCW declares and RegisterNatives binds.
			Assert.IsTrue (source.Contains ("[Register (\"remove\", \"(I)V\""), source);
			Assert.IsTrue (source.Contains ("[Register (\"remove\", \"(J)V\""), source);
			Assert.IsTrue (source.Contains ("[Register (\"remove\", \"(Z)V\""), source);
			Assert.IsTrue (source.Contains ("[Register (\"clear\", \"()V\""), source);
		}

		[TestMethodAttribute]
		public void LegacyOutputIsUnchangedWhenFlagIsOff ()
		{
			options = new CodeGenerationOptions {
				SupportNullableReferenceTypes = true,
				UseGlobal = true,
			};
			generator = new JavaInteropCodeGenerator (writer, options);

			var source = GenerateOverloads ();

			// Legacy names still carry the escaped Java signature, and the connector still names a
			// connector method rather than the callback.
			Assert.IsTrue (source.Contains ("\"remove\", \"(I)V\", \"GetRemove_IHandler\")"), source);
			Assert.IsTrue (source.Contains ("static unsafe void n_Remove_I (IntPtr jnienv"), source);
			Assert.IsTrue (source.Contains ("static Delegate GetRemove_IHandler ()"), source);
			Assert.IsFalse (source.Contains ("n_Remove_1"), source);
			Assert.IsFalse (source.Contains ("&m0)"), source);
		}

		const string CompilationSupport = """
			using System;
			delegate long _JniMarshal_PP_J (IntPtr env, IntPtr self);
			delegate long _JniMarshal_PPI_J (IntPtr env, IntPtr self, int value);
			delegate void _JniMarshal_PPJ_V (IntPtr env, IntPtr self, long value);
			delegate void _JniMarshal_PPI_V (IntPtr env, IntPtr self, int value);
			namespace Java.Interop {
				public static unsafe class JniMarshalTyped {
					public static void Invoke_X<TSelf> (IntPtr env, IntPtr self, delegate*<TSelf, void> target)
						where TSelf : class, Android.Runtime.IJavaObject { }
					public static void Invoke_SX<TSelf, T> (IntPtr env, IntPtr self, T value, delegate*<TSelf, T, void> target)
						where TSelf : class, Android.Runtime.IJavaObject where T : unmanaged { }
					public static T Invoke_S<TSelf, T> (IntPtr env, IntPtr self, delegate*<TSelf, T> target)
						where TSelf : class, Android.Runtime.IJavaObject where T : unmanaged => default;
					public static TResult Invoke_SS<TSelf, T, TResult> (IntPtr env, IntPtr self, T value, delegate*<TSelf, T, TResult> target)
						where TSelf : class, Android.Runtime.IJavaObject where T : unmanaged where TResult : unmanaged => default;
				}
				public static unsafe class JniMarshal {
					public static void SafeInvokeAction<T1, T2> (IntPtr env, IntPtr self, T1 a, T2 b, delegate*<IntPtr, IntPtr, T1, T2, void> target) { }
					public static void SafeInvokeAction<T> (IntPtr env, IntPtr self, T value, delegate*<IntPtr, IntPtr, T, void> target) { }
					public static T SafeInvokeFunc<T> (IntPtr env, IntPtr self, delegate*<IntPtr, IntPtr, T> target) => default;
					public static TResult SafeInvokeFunc<T, TResult> (IntPtr env, IntPtr self, T value, delegate*<IntPtr, IntPtr, T, TResult> target) => default;
				}
			}
			""";

		static Assembly CompileCallbacks (string source)
			=> CompileCallbacks (source, out _);

		static Assembly CompileCallbacks (string source, out byte [] assemblyImage)
		{
			var assembly = Compiler.CompileSources (new [] {
				"using System; using Android.Runtime; using Java.Interop; namespace Com.Example {\n" + source + "\n}",
				CompilationSupport,
			}, out var hasErrors, out var output, out assemblyImage);
			Assert.IsFalse (hasErrors, output + "\n" + source);
			return assembly;
		}

		[TestMethodAttribute]
		[DataRow (false, false)]
		[DataRow (false, true)]
		[DataRow (true, false)]
		[DataRow (true, true)]
		public void PrimitiveParametersDoNotShadowCallbackTargets (bool isInterface, bool raw)
		{
			var element = isInterface ? "interface" : "class";
			var api = $$"""
				<api>
				  <package name='java.lang'>
				    <class name='Object' visibility='public' />
				  </package>
				  <package name='com.example'>
				    <{{element}} name='Widget' extends='java.lang.Object' visibility='public' abstract='{{isInterface.ToString ().ToLowerInvariant ()}}'>
				      <method name='work' return='void' visibility='public' abstract='{{isInterface.ToString ().ToLowerInvariant ()}}' static='false' final='false'>
				        <parameter name='m0' type='int' />
				        {{(raw ? "<parameter name='text' type='java.lang.String' />" : "")}}
				      </method>
				    </{{element}}>
				  </package>
				</api>
				""";
			var gens = ParseApiDefinition (api);
			var name = isInterface ? "IWidget" : "Widget";
			var source = GetGeneratedTypeOutput (gens.Single (g => g.Name == name));
			var owner = name + (isInterface ? "Invoker" : "");

			var assembly = CompileCallbacks (source);
			Assert.IsTrue (source.Contains ($"&global::Com.Example.{owner}.m0)"), source);
			Assert.IsTrue (source.Contains (raw ? "JniMarshal.SafeInvokeAction" : "JniMarshalTyped.Invoke_SX"), source);
			var callback = assembly.GetType ("Com.Example." + owner)?.GetMethod ("n_Work", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull (callback);
		}

		[TestMethodAttribute]
		public void InterfacePropertyTargetsUseInvokerOwner ()
		{
			var api = """
				<api>
				  <package name='java.lang'><class name='Object' visibility='public' /></package>
				  <package name='com.example'>
				    <interface name='Widget' visibility='public' abstract='true'>
				      <method name='getValue' return='long' visibility='public' abstract='true' final='false' static='false' />
				      <method name='setValue' return='void' visibility='public' abstract='true' final='false' static='false'>
				        <parameter name='m1' type='long' />
				      </method>
				    </interface>
				  </package>
				</api>
				""";
			var gens = ParseApiDefinition (api);
			var source = GetGeneratedTypeOutput (gens.Single (g => g.Name == "IWidget"));
			CompileCallbacks (source);
			Assert.IsTrue (source.Contains ("&global::Com.Example.IWidgetInvoker.m0)"), source);
			Assert.IsTrue (source.Contains ("&global::Com.Example.IWidgetInvoker.m1)"), source);
		}

		[TestMethodAttribute]
		[DataRow (false)]
		[DataRow (true)]
		public void InheritedInterfaceTargetsUseEmittedInvoker (bool raw)
		{
			var api = $$"""
				<api>
				  <package name='java.lang'><class name='Object' visibility='public' /></package>
				  <package name='com.example'>
				    <interface name='Parent' visibility='public' abstract='true'>
				      <method name='work' return='void' visibility='public' abstract='true' final='false'>
				        <parameter name='m0' type='int' />
				        {{(raw ? "<parameter name='text' type='java.lang.String' />" : "")}}
				      </method>
				    </interface>
				    <interface name='Child' visibility='public' abstract='true'>
				      <implements name='com.example.Parent' name-generic-aware='com.example.Parent' />
				    </interface>
				  </package>
				</api>
				""";
			foreach (var gen in ParseApiDefinition (api).Where (g => g.Namespace == "Com.Example"))
				GetGeneratedTypeOutput (gen);
			var source = writer.ToString ();
			CompileCallbacks (source);
			Assert.IsTrue (source.Contains ("&global::Com.Example.IChildInvoker.m0)"), source);
			Assert.IsTrue (source.Contains ("&global::Com.Example.IParentInvoker.m0)"), source);
		}

		[TestMethodAttribute]
		[DataRow (false)]
		[DataRow (true)]
		public void DefaultInterfaceTargetsUseInterfaceOwner (bool raw)
		{
			options.SupportDefaultInterfaceMethods = true;
			var api = $$"""
				<api>
				  <package name='java.lang'><class name='Object' visibility='public' /></package>
				  <package name='com.example'>
				    <interface name='Widget' visibility='public' abstract='true'>
				      <method name='work' return='void' visibility='public' abstract='false' final='false' static='false'>
				        <parameter name='m0' type='int' />
				        {{(raw ? "<parameter name='text' type='java.lang.String' />" : "")}}
				      </method>
				    </interface>
				  </package>
				</api>
				""";
			var gens = ParseApiDefinition (api);
			var source = GetGeneratedTypeOutput (gens.Single (g => g.Name == "IWidget"));
			CompileCallbacks (source);
			Assert.IsTrue (source.Contains ("&global::Com.Example.IWidget.m0)"), source);
		}

		[TestMethodAttribute]
		[DataRow (true, false, false)]
		[DataRow (true, true, false)]
		[DataRow (false, false, false)]
		[DataRow (true, false, true)]
		public void AbstractPropertyOverridesReuseEmittingDeclaration (bool abstractBase, bool redeclareMiddle, bool genericBase)
		{
			const string property = """
				<method name='getValue' return='long' visibility='public' abstract='true' final='false' static='false' />
				<method name='setValue' return='void' visibility='public' abstract='true' final='false' static='false'>
				  <parameter name='value' type='long' />
				</method>
				""";
			var api = $$"""
				<api>
				  <package name='java.lang'><class name='Object' visibility='public' /></package>
				  <package name='com.example'>
				    <class name='Base' extends='java.lang.Object' visibility='public' abstract='true'>
				      {{(genericBase ? "<typeParameters><typeParameter name='T' classBound='java.lang.Object' /></typeParameters>" : "")}}
				      {{(abstractBase ? property : property.Replace ("abstract='true'", "abstract='false'"))}}
				      <method name='getValue' return='long' visibility='public' abstract='false' final='false'>
				        <parameter name='index' type='int' />
				      </method>
				      <method name='setValue' return='void' visibility='public' abstract='false' final='false'>
				        <parameter name='value' type='int' />
				      </method>
				    </class>
				    <class name='Middle' extends='com.example.Base' visibility='public' abstract='true'>
				      {{(redeclareMiddle ? property : "")}}
				    </class>
				    <class name='Derived' extends='com.example.Middle' visibility='public' abstract='true'>
				      {{property}}
				    </class>
				  </package>
				</api>
				""";
			options.AssemblyName = "Callbacks";
			var gens = ParseApiDefinition (api);
			foreach (var gen in gens.Where (g => g.Namespace == "Com.Example").Reverse ())
				GetGeneratedTypeOutput (gen);
			var source = writer.ToString ();
			var assembly = CompileCallbacks (source);

			foreach (var typeName in new [] { "Derived", "DerivedInvoker" }) {
				var type = assembly.GetType ("Com.Example." + typeName);
				Assert.IsNotNull (type);
				var value = type.GetProperty ("Value", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
				Assert.IsNotNull (value);
#pragma warning disable MSTESTEXP // Preserve grouped assertions with the native preview API.
				using (Assert.Scope ()) {
					AssertInheritedConnector (assembly, value.GetMethod, (genericBase ? "GetGetValueHandler" : "n_GetValue") + ":Com.Example.Base, Callbacks", genericBase);
					AssertInheritedConnector (assembly, value.SetMethod, (genericBase ? "GetSetValue_JHandler" : "n_SetValue_1") + ":Com.Example.Base, Callbacks", genericBase);
				}
#pragma warning restore MSTESTEXP
				Assert.IsFalse (type.GetMethods (BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
					.Any (m => m.Name.StartsWith ("n_", StringComparison.Ordinal)), source);
			}
		}

		[TestMethodAttribute]
		public void ImportedCallbackNamesMatchCompiledAssembly ()
		{
			const string baseApi = """
				<api>
				  <package name='java.lang'><class name='Object' visibility='public' /></package>
				  <package name='com.example'>
				    <interface name='Parent' visibility='public' abstract='true'>
				      <method name='work' return='void' visibility='public' abstract='false' final='false' static='false'>
				        <parameter name='value' type='int' />
				      </method>
				      <method name='work' return='void' visibility='public' abstract='false' final='false' static='false'>
				        <parameter name='value' type='long' />
				      </method>
				    </interface>
				  </package>
				</api>
				""";
			options.AssemblyName = "BaseCallbacks";
			options.SupportDefaultInterfaceMethods = true;
			var baseSource = GetGeneratedTypeOutput (ParseApiDefinition (baseApi).Single (g => g.Name == "IParent"));
			var baseAssembly = CompileCallbacks (baseSource, out var baseAssemblyImage);

			var importOptions = CreateOptions ();
			importOptions.SupportDefaultInterfaceMethods = true;
			importOptions.UseShallowReferencedTypes = false;
			using var baseDefinition = AssemblyDefinition.ReadAssembly (new System.IO.MemoryStream (baseAssemblyImage));
			var importedParent = CecilApiImporter.CreateInterface (
				baseDefinition.MainModule.GetType ("Com.Example.IParent"), importOptions);
			foreach (var method in importedParent.Methods)
				Assert.IsTrue (method.Validate (importOptions, new GenericParameterDefinitionList (), new CodeGeneratorContext ()));
			var parentType = baseAssembly.GetType ("Com.Example.IParent");
			Assert.IsNotNull (parentType);
			var importedCallbackNames = importedParent.Methods
				.Where (method => method.Name == "Work")
				.Select (method => importOptions.CallbackNames.GetCallbackName (importedParent, method))
				.OrderBy (name => name, StringComparer.Ordinal)
				.ToList ();

			CollectionAssert.AreEqual (new [] { "n_Work", "n_Work_1" }, importedCallbackNames);
			foreach (var callbackName in importedCallbackNames)
				Assert.IsNotNull (parentType.GetMethod (callbackName,
					BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly), baseSource);
		}

		static void AssertInheritedConnector (Assembly assembly, MethodInfo accessor, string expected, bool legacy)
		{
			Assert.IsNotNull (accessor);
			var register = accessor.GetCustomAttributesData ().Single (a => a.AttributeType.Name == "RegisterAttribute");
			var connector = (string) register.ConstructorArguments [2].Value;
			Assert.AreEqual (expected, connector);
			var callbackName = connector.Split (':') [0];
			var callback = assembly.GetType ("Com.Example.Base")?.GetMethod (callbackName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
			Assert.IsNotNull (callback, connector);
			Assert.AreEqual (!legacy, callback.GetCustomAttributesData ().Any (a => a.AttributeType.Name == "UnmanagedCallersOnlyAttribute"), connector);
			if (!legacy) {
				Assert.AreEqual (accessor.ReturnType, callback.ReturnType, connector);
				CollectionAssert.AreEqual (accessor.GetParameters ().Select (p => p.ParameterType).ToArray (), callback.GetParameters ().Skip (2).Select (p => p.ParameterType).ToArray (), connector);
			}
		}
	}
}
