using System;
using System.Runtime.Versioning;
using Android.Runtime;
using Java.Interop;

[assembly: SupportedOSPlatform ("android24.0")]

namespace UserApp.JavaSourceParity;

[Register ("com/example/parity/Base", DoNotGenerateAcw = true)]
public class Base : Java.Lang.Object
{
	protected Base (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

	[Register (".ctor", "()V", "")]
	public Base () { }

	[Register (".ctor", "(I)V", "")]
	public Base (int value) { }

	[Register ("getValue", "()Ljava/lang/String;", "GetGetValueHandler")]
	public virtual string GetValue () => "";
}

[Register ("com/example/parity/SemanticPeer")]
public class SemanticPeer : Base, Android.Views.View.IOnClickListener, Android.Views.View.IOnLongClickListener
{
	public SemanticPeer () { }

	public SemanticPeer (int value) : base (value) { }

	public override string GetValue () => "derived";

	public void OnClick (Android.Views.View? view) { }

	public bool OnLongClick (Android.Views.View? view) => true;

	[Export ("checkedExport", Throws = new [] { typeof (Java.IO.IOException) })]
	protected int CheckedExport (string value) => value.Length;

	[ExportField ("STATIC_LABEL")]
	public static string GetStaticLabel () => "static";

	[ExportField ("LABEL")]
	public string GetLabel () => "instance";
}
