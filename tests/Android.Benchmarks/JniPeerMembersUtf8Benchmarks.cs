using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Java.Interop;
using System.Text;

namespace Xamarin.Android.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy (BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public unsafe class JniPeerMembersUtf8Benchmarks
{
	const string Constructor = "()V";
	const string InstanceMethod = "hashCode.()I";
	const string StaticMethod = "currentTimeMillis.()J";
	const string InstanceField = "detailMessage.Ljava/lang/String;";
	const string StaticField = "out.Ljava/io/PrintStream;";

	static readonly ReadOnlyMemory<byte> StringPeerType = "java/lang/String"u8.ToArray ();
	static ReadOnlySpan<byte> MemberPool => "()VhashCode.()IcurrentTimeMillis.()JdetailMessage.Ljava/lang/String;out.Ljava/io/PrintStream;"u8;
	static ReadOnlySpan<byte> GetMember (int value) => MemberPool.Slice (value & 4194303, (int) ((uint) value >> 22));
	static ReadOnlySpan<byte> ConstructorUtf8 => GetMember (12582912);
	static ReadOnlySpan<byte> InstanceMethodUtf8 => GetMember (50331651);
	static ReadOnlySpan<byte> StaticMethodUtf8 => GetMember (88080399);
	static ReadOnlySpan<byte> InstanceFieldUtf8 => GetMember (134217764);
	static ReadOnlySpan<byte> StaticFieldUtf8 => GetMember (104857668);

	BenchmarkPeerMembers? stringMembers;
	BenchmarkPeerMembers? throwableMembers;
	BenchmarkPeerMembers? systemMembers;
	Java.Lang.String? peer;
	Java.Lang.Throwable? throwable;
	JniMethodInfo? instanceMethodInfo;
	JniMethodInfo? staticMethodInfo;

	BenchmarkPeerMembers StringMembers => stringMembers ?? throw new InvalidOperationException ();
	BenchmarkPeerMembers ThrowableMembers => throwableMembers ?? throw new InvalidOperationException ();
	BenchmarkPeerMembers SystemMembers => systemMembers ?? throw new InvalidOperationException ();
	Java.Lang.String Peer => peer ?? throw new InvalidOperationException ();
	Java.Lang.Throwable Throwable => throwable ?? throw new InvalidOperationException ();
	JniMethodInfo InstanceMethodInfo => instanceMethodInfo ?? throw new InvalidOperationException ();
	JniMethodInfo StaticMethodInfo => staticMethodInfo ?? throw new InvalidOperationException ();

	[GlobalSetup]
	public void Setup ()
	{
		stringMembers = new BenchmarkPeerMembers ("java/lang/String", typeof (Java.Lang.String));
		throwableMembers = new BenchmarkPeerMembers ("java/lang/Throwable", typeof (Java.Lang.Throwable));
		systemMembers = new BenchmarkPeerMembers ("java/lang/System", typeof (Java.Lang.String));
		peer = new Java.Lang.String ("benchmark");
		throwable = new Java.Lang.Throwable ("benchmark");

		_ = StringMembers.InstanceMethods.GetConstructor (Constructor);
		_ = StringMembers.InstanceMethods.GetConstructor (ConstructorUtf8);
		instanceMethodInfo = StringMembers.InstanceMethods.GetMethodInfo (InstanceMethod);
		_ = StringMembers.InstanceMethods.GetMethodInfo (InstanceMethodUtf8);
		staticMethodInfo = SystemMembers.StaticMethods.GetMethodInfo (StaticMethod);
		_ = SystemMembers.StaticMethods.GetMethodInfo (StaticMethodUtf8);
		_ = ThrowableMembers.InstanceFields.GetFieldInfo (InstanceField);
		_ = ThrowableMembers.InstanceFields.GetFieldInfo (InstanceFieldUtf8);
		_ = SystemMembers.StaticFields.GetFieldInfo (StaticField);
		_ = SystemMembers.StaticFields.GetFieldInfo (StaticFieldUtf8);
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		peer?.Dispose ();
		throwable?.Dispose ();
		if (stringMembers is not null)
			JniPeerMembers.Dispose (stringMembers);
		if (throwableMembers is not null)
			JniPeerMembers.Dispose (throwableMembers);
		if (systemMembers is not null)
			JniPeerMembers.Dispose (systemMembers);
	}

	[Benchmark (Baseline = true)]
	[BenchmarkCategory ("CreatePeerMembers")]
	public JniPeerMembers CreatePeerMembersString () =>
		new BenchmarkPeerMembers ("java/lang/String", typeof (Java.Lang.String));

	[Benchmark]
	[BenchmarkCategory ("CreatePeerMembers")]
	public JniPeerMembers CreatePeerMembersUtf8 () =>
		new BenchmarkPeerMembers (StringPeerType, typeof (Java.Lang.String));

	[Benchmark (Baseline = true)]
	[BenchmarkCategory ("ResolvePeerType")]
	public IntPtr ResolvePeerTypeString ()
	{
		var members = new BenchmarkPeerMembers ("java/lang/String", typeof (Java.Lang.String));
		try {
			return members.JniPeerType.PeerReference.Handle;
		} finally {
			JniPeerMembers.Dispose (members);
		}
	}

	[Benchmark]
	[BenchmarkCategory ("ResolvePeerType")]
	public IntPtr ResolvePeerTypeUtf8 ()
	{
		var members = new BenchmarkPeerMembers (StringPeerType, typeof (Java.Lang.String));
		try {
			return members.JniPeerType.PeerReference.Handle;
		} finally {
			JniPeerMembers.Dispose (members);
		}
	}

	[Benchmark (Baseline = true)]
	[BenchmarkCategory ("ConstructorLookup")]
	public JniMethodInfo GetConstructorString () =>
		StringMembers.InstanceMethods.GetConstructor (Constructor);

	[Benchmark]
	[BenchmarkCategory ("ConstructorLookup")]
	public JniMethodInfo GetConstructorUtf8 () =>
		StringMembers.InstanceMethods.GetConstructor (ConstructorUtf8);

	[Benchmark (Baseline = true)]
	[BenchmarkCategory ("InstanceMethodLookup")]
	public JniMethodInfo GetInstanceMethodString () =>
		StringMembers.InstanceMethods.GetMethodInfo (InstanceMethod);

	[Benchmark]
	[BenchmarkCategory ("InstanceMethodLookup")]
	public JniMethodInfo GetInstanceMethodUtf8 () =>
		StringMembers.InstanceMethods.GetMethodInfo (InstanceMethodUtf8);

	[Benchmark (Baseline = true)]
	[BenchmarkCategory ("InstanceMethodInvoke")]
	public int InvokeInstanceMethodString () =>
		StringMembers.InstanceMethods.InvokeVirtualInt32Method (InstanceMethod, Peer, null);

	[Benchmark]
	[BenchmarkCategory ("InstanceMethodInvoke")]
	public int InvokeInstanceMethodUtf8 () =>
		StringMembers.InstanceMethods.InvokeVirtualInt32Method (InstanceMethodUtf8, Peer, null);

	[Benchmark]
	[BenchmarkCategory ("InstanceMethodInvoke")]
	public int InvokeInstanceMethodDirect () =>
		JniEnvironment.InstanceMethods.CallIntMethod (Peer.PeerReference, InstanceMethodInfo, null);

	[Benchmark (Baseline = true)]
	[BenchmarkCategory ("StaticMethodLookup")]
	public JniMethodInfo GetStaticMethodString () =>
		SystemMembers.StaticMethods.GetMethodInfo (StaticMethod);

	[Benchmark]
	[BenchmarkCategory ("StaticMethodLookup")]
	public JniMethodInfo GetStaticMethodUtf8 () =>
		SystemMembers.StaticMethods.GetMethodInfo (StaticMethodUtf8);

	[Benchmark (Baseline = true)]
	[BenchmarkCategory ("StaticMethodInvoke")]
	public long InvokeStaticMethodString () =>
		SystemMembers.StaticMethods.InvokeInt64Method (StaticMethod, null);

	[Benchmark]
	[BenchmarkCategory ("StaticMethodInvoke")]
	public long InvokeStaticMethodUtf8 () =>
		SystemMembers.StaticMethods.InvokeInt64Method (StaticMethodUtf8, null);

	[Benchmark]
	[BenchmarkCategory ("StaticMethodInvoke")]
	public long InvokeStaticMethodDirect () =>
		JniEnvironment.StaticMethods.CallStaticLongMethod (SystemMembers.JniPeerType.PeerReference, StaticMethodInfo, null);

	[Benchmark (Baseline = true)]
	[BenchmarkCategory ("InstanceFieldLookup")]
	public JniFieldInfo GetInstanceFieldInfoString () =>
		ThrowableMembers.InstanceFields.GetFieldInfo (InstanceField);

	[Benchmark]
	[BenchmarkCategory ("InstanceFieldLookup")]
	public JniFieldInfo GetInstanceFieldInfoUtf8 () =>
		ThrowableMembers.InstanceFields.GetFieldInfo (InstanceFieldUtf8);

	[Benchmark (Baseline = true)]
	[BenchmarkCategory ("InstanceFieldGet")]
	public bool GetInstanceFieldValueString ()
	{
		var value = ThrowableMembers.InstanceFields.GetObjectValue (InstanceField, Throwable);
		try {
			return value.IsValid;
		} finally {
			JniObjectReference.Dispose (ref value);
		}
	}

	[Benchmark]
	[BenchmarkCategory ("InstanceFieldGet")]
	public bool GetInstanceFieldValueUtf8 ()
	{
		var value = ThrowableMembers.InstanceFields.GetObjectValue (InstanceFieldUtf8, Throwable);
		try {
			return value.IsValid;
		} finally {
			JniObjectReference.Dispose (ref value);
		}
	}

	[Benchmark (Baseline = true)]
	[BenchmarkCategory ("StaticFieldLookup")]
	public JniFieldInfo GetStaticFieldInfoString () =>
		SystemMembers.StaticFields.GetFieldInfo (StaticField);

	[Benchmark]
	[BenchmarkCategory ("StaticFieldLookup")]
	public JniFieldInfo GetStaticFieldInfoUtf8 () =>
		SystemMembers.StaticFields.GetFieldInfo (StaticFieldUtf8);

	[Benchmark (Baseline = true)]
	[BenchmarkCategory ("StaticFieldGet")]
	public bool GetStaticFieldValueString ()
	{
		var value = SystemMembers.StaticFields.GetObjectValue (StaticField);
		try {
			return value.IsValid;
		} finally {
			JniObjectReference.Dispose (ref value);
		}
	}

	[Benchmark]
	[BenchmarkCategory ("StaticFieldGet")]
	public bool GetStaticFieldValueUtf8 ()
	{
		var value = SystemMembers.StaticFields.GetObjectValue (StaticFieldUtf8);
		try {
			return value.IsValid;
		} finally {
			JniObjectReference.Dispose (ref value);
		}
	}

	[Benchmark]
	[BenchmarkCategory ("Utf8DecodeControl")]
	public string DecodeUtf8Control () =>
		Encoding.UTF8.GetString ("hashCode.()I"u8);

	sealed class BenchmarkPeerMembers : JniPeerMembers
	{
		public BenchmarkPeerMembers (string jniPeerTypeName, Type managedPeerType)
			: base (jniPeerTypeName, managedPeerType)
		{
		}

		public BenchmarkPeerMembers (ReadOnlyMemory<byte> jniPeerTypeName, Type managedPeerType)
			: base (jniPeerTypeName, managedPeerType)
		{
		}
	}
}
