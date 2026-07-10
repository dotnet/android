#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Java.Interop {

	[SuppressMessage (
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Exceptions cannot cross a JNI boundary.")]
	public static partial class JniMarshal {

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction (IntPtr jnienv, IntPtr self, delegate* managed<IntPtr, IntPtr, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<TResult> (IntPtr jnienv, IntPtr self, delegate* managed<IntPtr, IntPtr, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0> (IntPtr jnienv, IntPtr self, T0 p0, delegate* managed<IntPtr, IntPtr, T0, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, TResult> (IntPtr jnienv, IntPtr self, T0 p0, delegate* managed<IntPtr, IntPtr, T0, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0, T1> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, delegate* managed<IntPtr, IntPtr, T0, T1, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0, p1);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, T1, TResult> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, delegate* managed<IntPtr, IntPtr, T0, T1, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0, p1);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0, T1, T2> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, delegate* managed<IntPtr, IntPtr, T0, T1, T2, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0, p1, p2);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, T1, T2, TResult> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, delegate* managed<IntPtr, IntPtr, T0, T1, T2, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0, p1, p2);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0, T1, T2, T3> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0, p1, p2, p3);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, T1, T2, T3, TResult> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0, p1, p2, p3);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0, T1, T2, T3, T4> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0, p1, p2, p3, p4);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, T1, T2, T3, T4, TResult> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0, p1, p2, p3, p4);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0, T1, T2, T3, T4, T5> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0, p1, p2, p3, p4, p5);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, T1, T2, T3, T4, T5, TResult> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0, p1, p2, p3, p4, p5);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0, T1, T2, T3, T4, T5, T6> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, T1, T2, T3, T4, T5, T6, TResult> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0, T1, T2, T3, T4, T5, T6, T7> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, T1, T2, T3, T4, T5, T6, T7, TResult> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0, T1, T2, T3, T4, T5, T6, T7, T8> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, T1, T2, T3, T4, T5, T6, T7, T8, TResult> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, T12 p12, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, T12 p12, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, T12 p12, T13 p13, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, T12 p12, T13 p13, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, T12 p12, T13 p13, T14 p14, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, T12 p12, T13 p13, T14 p14, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe void SafeInvokeAction<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, T12 p12, T13 p13, T14 p14, T15 p15, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, void> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[return: MaybeNull]
		[DebuggerDisableUserUnhandledExceptions]
		public static unsafe TResult SafeInvokeFunc<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult> (IntPtr jnienv, IntPtr self, T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, T12 p12, T13 p13, T14 p14, T15 p15, delegate* managed<IntPtr, IntPtr, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult> action)
		{
			if (!JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				return action (jnienv, self, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15);
			} catch (Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}
	}
}
