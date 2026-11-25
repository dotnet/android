using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Java.Interop;
using NUnit.Framework;

namespace Java.InteropTests;

[TestFixture]
[NonParallelizable]
public class JniThreadAttachmentTests
{
	static int getEnvStatus;
	static IntPtr getEnvValue;
	static int attachStatus;
	static IntPtr attachValue;
	static int attachCount;
	static int registerCount;
	static IntPtr registeredValue;

	JniRuntime? runtime;
	MethodInfo? attach;
	FieldInfo? registration;
	IntPtr previousRegistration;

	/// <safety>
	/// <paramref name="env"/> must address a writable pointer-sized slot.
	/// </safety>
	[UnmanagedCallersOnly]
	static unsafe int GetEnv (IntPtr vm, IntPtr* env, int version)
	{
		unsafe {
			// SAFETY: The JavaVM invocation wrapper supplies a live writable output slot.
			*env = getEnvValue;
		}
		return getEnvStatus;
	}

	/// <safety>
	/// <paramref name="env"/> must address a writable pointer-sized slot.
	/// </safety>
	[UnmanagedCallersOnly]
	static unsafe int Attach (IntPtr vm, IntPtr* env, void* args)
	{
		++attachCount;
		unsafe {
			// SAFETY: The JavaVM invocation wrapper supplies a live writable output slot.
			*env = attachValue;
		}
		return attachStatus;
	}

	[UnmanagedCallersOnly]
	static void Register (IntPtr env)
	{
		++registerCount;
		registeredValue = env;
	}

	[OneTimeSetUp]
	public void CreateFakeRuntime ()
	{
		Type type = typeof (JniRuntime);
		// The fake runtime only exercises attachment; its real constructor requires a complete JVM.
		runtime = (JniRuntime) RuntimeHelpers.GetUninitializedObject (type);
		GC.SuppressFinalize (runtime);

		FieldInfo invoker = type.GetField ("Invoker", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException ("Missing VM invoker field.");
		unsafe {
			// SAFETY: The callback addresses remain live, and the complete fake table is copied synchronously.
			nint* table = stackalloc nint [8];
			for (int i = 0; i < 8; ++i)
				table [i] = 0;
			table [4] = (nint) (delegate* unmanaged<IntPtr, IntPtr*, void*, int>) &Attach;
			table [6] = (nint) (delegate* unmanaged<IntPtr, IntPtr*, int, int>) &GetEnv;
			object nativeTable = Marshal.PtrToStructure ((IntPtr) table, invoker.FieldType)
				?? throw new InvalidOperationException ("Could not marshal the VM table.");
			invoker.SetValue (runtime, nativeTable);
		}
		(type.GetProperty ("InvocationPointer") ?? throw new InvalidOperationException ("Missing VM pointer."))
			.SetValue (runtime, (IntPtr) 1);
		(type.GetProperty ("JniVersion") ?? throw new InvalidOperationException ("Missing JNI version."))
			.SetValue (runtime, JniVersion.v1_6);

		registration = type.GetField ("registerThreadDetach", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException ("Missing detach registration field.");
		previousRegistration = (IntPtr) (registration.GetValue (null) ?? IntPtr.Zero);
		MethodInfo setter = type.GetMethod ("SetThreadDetachCallback", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException ("Missing detach registration setter.");
		unsafe {
			// SAFETY: Register is an unmanaged-callable static method that outlives the fake runtime.
			setter.Invoke (null, [(IntPtr) (delegate* unmanaged<IntPtr, void>) &Register]);
		}
		attach = type.GetMethod ("_AttachCurrentThread", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException ("Missing attachment method.");
	}

	[OneTimeTearDown]
	public void RestoreCallback ()
	{
		if (registration != null)
			registration.SetValue (null, previousRegistration);
	}

	[SetUp]
	public void Reset ()
	{
		getEnvStatus = 0;
		getEnvValue = (IntPtr) 0x1234;
		attachStatus = 0;
		attachValue = (IntPtr) 0x1234;
		attachCount = 0;
		registerCount = 0;
		registeredValue = IntPtr.Zero;
	}

	IntPtr CallAttach ()
	{
		var method = attach ?? throw new InvalidOperationException ("The attachment method was not initialized.");
		var vm = runtime ?? throw new InvalidOperationException ("The fake runtime was not initialized.");
		object? result = method.Invoke (vm, [null, default (JniObjectReference)]);
		return result is IntPtr env ? env : throw new InvalidOperationException ("Attach returned no environment.");
	}

	[Test]
	public void ExistingAttachmentIsNotClaimed ()
	{
		Assert.That (CallAttach (), Is.EqualTo (getEnvValue));
		Assert.That (attachCount, Is.Zero);
		Assert.That (registerCount, Is.Zero);
	}

	[Test]
	public void NewAttachmentIsRegistered ()
	{
		getEnvStatus = -2;
		getEnvValue = IntPtr.Zero;
		Assert.That (CallAttach (), Is.EqualTo (attachValue));
		Assert.That (attachCount, Is.EqualTo (1));
		Assert.That (registerCount, Is.EqualTo (1));
		Assert.That (registeredValue, Is.EqualTo (attachValue));
	}

	[TestCase (-3, 0, 0, typeof (NotSupportedException), 0)]
	[TestCase (-2, -1, 0x1234, typeof (NotSupportedException), 1)]
	[TestCase (0, 0, 0, typeof (InvalidOperationException), 0)]
	[TestCase (-2, 0, 0, typeof (InvalidOperationException), 1)]
	public void FailedVmCallsDoNotRegisterCleanup (int getStatus, int newAttachStatus, int env, Type expectedException, int expectedAttaches)
	{
		getEnvStatus = getStatus;
		getEnvValue = (IntPtr) env;
		attachStatus = newAttachStatus;
		attachValue = (IntPtr) env;

		var error = Assert.Throws<TargetInvocationException> (() => CallAttach ());
		Assert.That (error?.InnerException, Is.TypeOf (expectedException));
		Assert.That (attachCount, Is.EqualTo (expectedAttaches));
		Assert.That (registerCount, Is.Zero);
	}
}
