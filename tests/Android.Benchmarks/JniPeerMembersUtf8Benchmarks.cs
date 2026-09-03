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
	static ReadOnlySpan<byte> SplitMemberPool => "()VhashCode()IcurrentTimeMillis()JdetailMessageLjava/lang/String;outLjava/io/PrintStream;"u8;
	static ReadOnlySpan<byte> GetSplitMember (int value) => SplitMemberPool.Slice (value & 4194303, (int) ((uint) value >> 22));
	static ReadOnlySpan<byte> ConstructorUtf8 => GetMember (12582912);
	static ReadOnlySpan<byte> InstanceMethodUtf8 => GetMember (50331651);
	static ReadOnlySpan<byte> StaticMethodUtf8 => GetMember (88080399);
	static ReadOnlySpan<byte> InstanceFieldUtf8 => GetMember (134217764);
	static ReadOnlySpan<byte> StaticFieldUtf8 => GetMember (104857668);
	static JniUtf8EncodedMember InstanceMethodSplit => new (GetSplitMember (33554435), GetSplitMember (12582923));
	static JniUtf8EncodedMember StaticMethodSplit => new (GetSplitMember (71303182), GetSplitMember (12582943));
	static JniUtf8EncodedMember InstanceFieldSplit => new (GetSplitMember (54525986), GetSplitMember (75497519));
	static JniUtf8EncodedMember StaticFieldSplit => new (GetSplitMember (12582977), GetSplitMember (88080452));

	BenchmarkPeerMembers? stringMembers;
	BenchmarkPeerMembers? throwableMembers;
	BenchmarkPeerMembers? systemMembers;
	BenchmarkPeerMembers? splitStringMembers;
	BenchmarkPeerMembers? splitThrowableMembers;
	BenchmarkPeerMembers? splitSystemMembers;
	Java.Lang.String? peer;
	Java.Lang.Throwable? throwable;
	JniMethodInfo? instanceMethodInfo;
	JniMethodInfo? staticMethodInfo;

	BenchmarkPeerMembers StringMembers => stringMembers ?? throw new InvalidOperationException ();
	BenchmarkPeerMembers ThrowableMembers => throwableMembers ?? throw new InvalidOperationException ();
	BenchmarkPeerMembers SystemMembers => systemMembers ?? throw new InvalidOperationException ();
	BenchmarkPeerMembers SplitStringMembers => splitStringMembers ?? throw new InvalidOperationException ();
	BenchmarkPeerMembers SplitThrowableMembers => splitThrowableMembers ?? throw new InvalidOperationException ();
	BenchmarkPeerMembers SplitSystemMembers => splitSystemMembers ?? throw new InvalidOperationException ();
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
		splitStringMembers = new BenchmarkPeerMembers ("java/lang/String", typeof (Java.Lang.String));
		splitThrowableMembers = new BenchmarkPeerMembers ("java/lang/Throwable", typeof (Java.Lang.Throwable));
		splitSystemMembers = new BenchmarkPeerMembers ("java/lang/System", typeof (Java.Lang.String));
		peer = new Java.Lang.String ("benchmark");
		throwable = new Java.Lang.Throwable ("benchmark");

		_ = StringMembers.InstanceMethods.GetConstructor (Constructor);
		_ = StringMembers.InstanceMethods.GetConstructor (ConstructorUtf8);
		instanceMethodInfo = StringMembers.InstanceMethods.GetMethodInfo (InstanceMethod);
		_ = StringMembers.InstanceMethods.GetMethodInfo (InstanceMethodUtf8);
		_ = SplitStringMembers.InstanceMethods.GetMethodInfo (InstanceMethodSplit);
		staticMethodInfo = SystemMembers.StaticMethods.GetMethodInfo (StaticMethod);
		_ = SystemMembers.StaticMethods.GetMethodInfo (StaticMethodUtf8);
		_ = SplitSystemMembers.StaticMethods.GetMethodInfo (StaticMethodSplit);
		_ = ThrowableMembers.InstanceFields.GetFieldInfo (InstanceField);
		_ = ThrowableMembers.InstanceFields.GetFieldInfo (InstanceFieldUtf8);
		_ = SplitThrowableMembers.InstanceFields.GetFieldInfo (InstanceFieldSplit);
		_ = SystemMembers.StaticFields.GetFieldInfo (StaticField);
		_ = SystemMembers.StaticFields.GetFieldInfo (StaticFieldUtf8);
		_ = SplitSystemMembers.StaticFields.GetFieldInfo (StaticFieldSplit);
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
		if (splitStringMembers is not null)
			JniPeerMembers.Dispose (splitStringMembers);
		if (splitThrowableMembers is not null)
			JniPeerMembers.Dispose (splitThrowableMembers);
		if (splitSystemMembers is not null)
			JniPeerMembers.Dispose (splitSystemMembers);
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

	[Benchmark]
	[BenchmarkCategory ("InstanceMethodLookup")]
	public JniMethodInfo GetInstanceMethodSplitUtf8 () =>
		SplitStringMembers.InstanceMethods.GetMethodInfo (InstanceMethodSplit);

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
	public int InvokeInstanceMethodSplitUtf8 () =>
		SplitStringMembers.InstanceMethods.InvokeVirtualInt32Method (InstanceMethodSplit, Peer, null);

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

	[Benchmark]
	[BenchmarkCategory ("StaticMethodLookup")]
	public JniMethodInfo GetStaticMethodSplitUtf8 () =>
		SplitSystemMembers.StaticMethods.GetMethodInfo (StaticMethodSplit);

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
	public long InvokeStaticMethodSplitUtf8 () =>
		SplitSystemMembers.StaticMethods.InvokeInt64Method (StaticMethodSplit, null);

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

	[Benchmark]
	[BenchmarkCategory ("InstanceFieldLookup")]
	public JniFieldInfo GetInstanceFieldInfoSplitUtf8 () =>
		SplitThrowableMembers.InstanceFields.GetFieldInfo (InstanceFieldSplit);

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

	[Benchmark]
	[BenchmarkCategory ("InstanceFieldGet")]
	public bool GetInstanceFieldValueSplitUtf8 ()
	{
		var value = SplitThrowableMembers.InstanceFields.GetObjectValue (InstanceFieldSplit, Throwable);
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

	[Benchmark]
	[BenchmarkCategory ("StaticFieldLookup")]
	public JniFieldInfo GetStaticFieldInfoSplitUtf8 () =>
		SplitSystemMembers.StaticFields.GetFieldInfo (StaticFieldSplit);

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
	[BenchmarkCategory ("StaticFieldGet")]
	public bool GetStaticFieldValueSplitUtf8 ()
	{
		var value = SplitSystemMembers.StaticFields.GetObjectValue (StaticFieldSplit);
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
