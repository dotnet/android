using System;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test
{
	public abstract class AdapterView<T> : AdapterView  where T : IAdapter
	{
			public AdapterView (IntPtr handle, JniHandleOwnership transfer)
				: base (handle, transfer)
			{
			}

			protected override Java.Lang.Object RawAdapter {
				get { return JavaObjectExtensions.JavaCast<Java.Lang.Object>(JavaConvert.ToJavaObject (Adapter)); }
				set { Adapter = JavaConvert.FromJavaObject<T>(value); }
			}

			public abstract T Adapter {
				[Register ("getAdapter", "()Landroid/widget/Adapter;", "GetGetAdapterHandler")] get;
				[Register ("setAdapter", "(Landroid/widget/Adapter;)V", "GetSetAdapter_Landroid_widget_Adapter_Handler")] set;
			}
	}
}

