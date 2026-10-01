using System;
using System.Collections;

using Android.Runtime;
using Android.Views;

using Java.Interop;

using NUnit.Framework;

using static Java.InteropTests.TrimmableTypeMapTestHelpers;

namespace Java.InteropTests
{
	[TestFixture]
	[Category ("Export")]
	[Category ("TrimmableTypeMapRuntimeCoverage")]
	public class TrimmableTypeMapExportTests
	{
		[Test]
		public void OverloadedAndRenamedExports_DispatchByJniNameAndSignature ()
		{
			using var peer = new TrimmableExportOverloadPeer ();
			using var argument = new Java.Lang.String ("hello");

			var intMethod = GetRequiredMethodID (peer.Class.Handle, "call", "(I)I");
			var stringMethod = GetRequiredMethodID (peer.Class.Handle, "call", "(Ljava/lang/String;)Ljava/lang/String;");
			var renamedMethod = GetRequiredMethodID (peer.Class.Handle, "javaSideName", "()I");

			Assert.AreEqual (42, JNIEnv.CallIntMethod (peer.Handle, intMethod, new JValue (41)));
			var result = JNIEnv.CallObjectMethod (peer.Handle, stringMethod, new JValue (argument.Handle));
			try {
				Assert.AreEqual ("hello!", JNIEnv.GetString (result, JniHandleOwnership.DoNotTransfer));
			} finally {
				JNIEnv.DeleteLocalRef (result);
			}
			Assert.AreEqual (99, JNIEnv.CallIntMethod (peer.Handle, renamedMethod));
		}

		[Test]
		public void InheritedVirtualExport_DispatchesToManagedOverride ()
		{
			using var peer = new TrimmableExportDerivedPeer ();

			var method = GetRequiredMethodID (peer.Class.Handle, "ping", "()I");
			Assert.AreEqual (42, JNIEnv.CallIntMethod (peer.Handle, method));
		}

		[Test]
		public void RenamedInterfaceExport_AndInterfaceCallback_ReachManagedMethod ()
		{
			using var listener = new TrimmableExportClickListener ();
			using var view = new View (Android.App.Application.Context);

			var interfaceMethod = GetRequiredMethodID (listener.Class.Handle, "onClick", "(Landroid/view/View;)V");
			var exportedMethod = GetRequiredMethodID (listener.Class.Handle, "onClickRenamed", "(Landroid/view/View;)V");

			JNIEnv.CallVoidMethod (listener.Handle, interfaceMethod, new JValue (view.Handle));
			JNIEnv.CallVoidMethod (listener.Handle, exportedMethod, new JValue (view.Handle));

			Assert.AreEqual (2, listener.Invocations);
			Assert.AreEqual (view.Handle, listener.LastViewHandle);
		}

		[Test]
		public void EnumExports_MarshalPrimitiveArgumentsAndReturns ()
		{
			using var peer = new TrimmableExportEnumPeer ();

			var intMethod = GetRequiredMethodID (peer.Class.Handle, "nextInt", "(I)I");
			var longMethod = GetRequiredMethodID (peer.Class.Handle, "nextLong", "(J)J");

			Assert.AreEqual (2, JNIEnv.CallIntMethod (peer.Handle, intMethod, new JValue (1)));
			Assert.AreEqual (long.MaxValue, JNIEnv.CallLongMethod (peer.Handle, longMethod, new JValue (1L)));
		}

		[Test]
		public void CharSequenceAndCollectionExports_MarshalReferenceTypes ()
		{
			using var peer = new TrimmableExportReferencePeer ();
			using var argument = new Java.Lang.String ("managed");

			var echo = GetRequiredMethodID (
				peer.Class.Handle, "echoCharSequence", "(Ljava/lang/CharSequence;)Ljava/lang/CharSequence;");
			var echoed = JNIEnv.CallObjectMethod (peer.Handle, echo, new JValue (argument.Handle));
			try {
				Assert.AreEqual ("managed", JNIEnv.GetString (echoed, JniHandleOwnership.DoNotTransfer));
			} finally {
				JNIEnv.DeleteLocalRef (echoed);
			}

			var makeList = GetRequiredMethodID (peer.Class.Handle, "makeList", "()Ljava/util/List;");
			var listHandle = JNIEnv.CallObjectMethod (peer.Handle, makeList);
			using var list = Java.Lang.Object.GetObject<JavaList> (listHandle, JniHandleOwnership.TransferLocalRef);
			Assert.IsNotNull (list);
			Assert.AreEqual ("alpha", list [0]);
		}

		[Test]
		public void UserPeerArrayExport_UsesGeneratedElementNameAndRoundTrips ()
		{
			using var peer = new TrimmableExportUserArrayPeer ();
			using var first = new TrimmableExportUserPeer ();
			using var second = new TrimmableExportUserPeer ();

			var input = JNIEnv.NewObjectArray (first, second);
			try {
				var arrayName = JNIEnv.GetClassNameFromInstance (input);
				Assert.IsNotNull (arrayName);
				StringAssert.EndsWith ("/TrimmableExportUserPeer;", arrayName);

				var method = GetRequiredMethodID (peer.Class.Handle, "echoUserPeers", $"({arrayName}){arrayName}");
				var output = JNIEnv.CallObjectMethod (peer.Handle, method, new JValue (input));
				try {
					Assert.AreEqual (arrayName, JNIEnv.GetClassNameFromInstance (output));
					Assert.AreEqual (1, peer.Invocations);
					Assert.AreEqual (2, peer.ReceivedLength);
					Assert.IsNotNull (peer.ReceivedFirst);
					Assert.IsTrue (JNIEnv.IsSameObject (first.Handle, peer.ReceivedFirst.Handle));

					var returnedFirst = JNIEnv.GetObjectArrayElement (output, 0);
					try {
						var returnedSecond = JNIEnv.GetObjectArrayElement (output, 1);
						try {
							Assert.IsTrue (JNIEnv.IsSameObject (first.Handle, returnedFirst));
							Assert.IsTrue (JNIEnv.IsSameObject (second.Handle, returnedSecond));
						} finally {
							JNIEnv.DeleteLocalRef (returnedSecond);
						}
					} finally {
						JNIEnv.DeleteLocalRef (returnedFirst);
					}
				} finally {
					JNIEnv.DeleteLocalRef (output);
				}
			} finally {
				JNIEnv.DeleteLocalRef (input);
			}
		}

		[Test]
		public void StreamExport_MarshalsJavaInputStream ()
		{
			using var peer = new TrimmableExportStreamPeer ();
			using var input = new Java.IO.ByteArrayInputStream (new byte [] { 42, 43 });

			var method = GetRequiredMethodID (peer.Class.Handle, "readStream", "(Ljava/io/InputStream;)I");
			Assert.AreEqual (42, JNIEnv.CallIntMethod (peer.Handle, method, new JValue (input.Handle)));
		}

		[Test]
		public void NonPublicExports_DispatchFromJava ()
		{
			using var peer = new TrimmableExportNonPublicPeer ();

			var protectedMethod = GetRequiredMethodID (peer.Class.Handle, "protectedCall", "(I)I");
			var privateMethod = GetRequiredMethodID (peer.Class.Handle, "privateCall", "(I)I");

			Assert.AreEqual (42, JNIEnv.CallIntMethod (peer.Handle, protectedMethod, new JValue (21)));
			Assert.AreEqual (43, JNIEnv.CallIntMethod (peer.Handle, privateMethod, new JValue (21)));
			Assert.AreEqual (2, peer.Invocations);
		}

		[Test]
		public void ExportConstructor_ForwardsSuperArgumentsToJavaBase ()
		{
			TrimmableExportSuperArgsPeer.ConstructorInvocations = 0;

			var handle = JNIEnv.StartCreateInstance (typeof (TrimmableExportSuperArgsPeer), "(I)V", new JValue (42));
			try {
				JNIEnv.FinishCreateInstance (handle, "(I)V", new JValue (42));
				using var peer = Java.Lang.Object.GetObject<TrimmableExportSuperArgsPeer> (handle, JniHandleOwnership.DoNotTransfer);
				Assert.IsNotNull (peer);

				var method = GetRequiredMethodID (peer.Class.Handle, "getSuperValue", "()I");
				Assert.AreEqual (42, JNIEnv.CallIntMethod (peer.Handle, method));
				Assert.AreEqual (1, TrimmableExportSuperArgsPeer.ConstructorInvocations);
			} finally {
				JNIEnv.DeleteLocalRef (handle);
			}
		}
	}

	class TrimmableExportOverloadPeer : Java.Lang.Object
	{
		[Export ("call")]
		public int Call (int value) => value + 1;

		[Export ("call")]
		public string Call (string value) => value + "!";

		[Export ("javaSideName")]
		public int ManagedName () => 99;
	}

	class TrimmableExportBasePeer : Java.Lang.Object
	{
		[Export ("ping")]
		public virtual int Ping () => 0;
	}

	class TrimmableExportDerivedPeer : TrimmableExportBasePeer
	{
		public override int Ping () => 42;
	}

	class TrimmableExportClickListener : Java.Lang.Object, View.IOnClickListener
	{
		public int Invocations;
		public IntPtr LastViewHandle;

		[Export ("onClickRenamed")]
		public void OnClick (View view)
		{
			Invocations++;
			LastViewHandle = view.Handle;
		}
	}

	enum TrimmableExportIntEnum { One = 1, Two = 2 }
	enum TrimmableExportLongEnum : long { One = 1, Max = long.MaxValue }

	class TrimmableExportEnumPeer : Java.Lang.Object
	{
		[Export ("nextInt")]
		public TrimmableExportIntEnum NextInt (TrimmableExportIntEnum value)
			=> value == TrimmableExportIntEnum.One ? TrimmableExportIntEnum.Two : TrimmableExportIntEnum.One;

		[Export ("nextLong")]
		public TrimmableExportLongEnum NextLong (TrimmableExportLongEnum value)
			=> value == TrimmableExportLongEnum.One ? TrimmableExportLongEnum.Max : TrimmableExportLongEnum.One;
	}

	class TrimmableExportReferencePeer : Java.Lang.Object
	{
		[Export ("echoCharSequence")]
		public Java.Lang.ICharSequence EchoCharSequence (Java.Lang.ICharSequence value) => value;

		[Export ("makeList")]
		public IList MakeList () => new ArrayList { "alpha" };
	}

	class TrimmableExportUserPeer : Java.Lang.Object
	{
	}

	class TrimmableExportUserArrayPeer : Java.Lang.Object
	{
		public int Invocations;
		public int ReceivedLength;
		public TrimmableExportUserPeer ReceivedFirst;

		[Export ("echoUserPeers")]
		public TrimmableExportUserPeer [] EchoUserPeers (TrimmableExportUserPeer [] values)
		{
			Invocations++;
			ReceivedLength = values.Length;
			ReceivedFirst = values [0];
			return values;
		}
	}

	class TrimmableExportStreamPeer : Java.Lang.Object
	{
		[Export ("readStream")]
		public int ReadStream ([ExportParameter (ExportParameterKind.InputStream)] System.IO.Stream stream)
			=> stream.ReadByte ();
	}

	class TrimmableExportNonPublicPeer : Java.Lang.Object
	{
		public int Invocations;

		[Export ("protectedCall")]
		protected int ProtectedCall (int value)
		{
			Invocations++;
			return value * 2;
		}

		[Export ("privateCall")]
		int PrivateCall (int value)
		{
			Invocations++;
			return value * 2 + 1;
		}
	}

	[Register ("net/dot/android/test/TrimmableSuperArgsBase", DoNotGenerateAcw = true)]
	class TrimmableSuperArgsBase : Java.Lang.Object
	{
		[Register (".ctor", "(I)V", "")]
		public TrimmableSuperArgsBase (int value)
		{
		}

		protected TrimmableSuperArgsBase (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}

	[Register ("net/dot/android/test/TrimmableExportSuperArgsPeer")]
	class TrimmableExportSuperArgsPeer : TrimmableSuperArgsBase
	{
		public static int ConstructorInvocations;

		[Export (SuperArgumentsString = "p0")]
		public TrimmableExportSuperArgsPeer (int value)
			: base (value)
		{
			ConstructorInvocations++;
		}
	}
}
