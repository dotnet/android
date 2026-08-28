using Java.Interop;

namespace MyApp;

[JniTypeSignature ("java/util/Collection", GenerateJavaPeer = false, InvokerType = typeof (ExplicitJavaInteropCollectionProxy))]
public interface IExplicitJavaInteropCollection
{
}

public sealed class ExplicitJavaInteropCollectionProxy : Java.Lang.Object
{
	public ExplicitJavaInteropCollectionProxy (ref JniObjectReference reference, JniObjectReferenceOptions options)
		: base ((System.IntPtr) 0, Android.Runtime.JniHandleOwnership.DoNotTransfer)
	{
	}
}

[JniTypeSignature ("java/util/List", GenerateJavaPeer = false, InvokerType = typeof (InheritedJavaInteropListProxy))]
public interface IInheritedJavaInteropList : IExplicitJavaInteropCollection
{
}

public sealed class InheritedJavaInteropListProxy : Java.Lang.Object
{
	public InheritedJavaInteropListProxy (ref JniObjectReference reference, JniObjectReferenceOptions options)
		: base ((System.IntPtr) 0, Android.Runtime.JniHandleOwnership.DoNotTransfer)
	{
	}
}

[JniTypeSignature ("java/util/AbstractList", GenerateJavaPeer = false, InvokerType = typeof (AbstractJavaInteropListProxy))]
public abstract class AbstractJavaInteropList : Java.Lang.Object
{
	protected AbstractJavaInteropList (ref JniObjectReference reference, JniObjectReferenceOptions options)
		: base ((System.IntPtr) 0, Android.Runtime.JniHandleOwnership.DoNotTransfer)
	{
	}
}

public sealed class AbstractJavaInteropListProxy : AbstractJavaInteropList
{
	public AbstractJavaInteropListProxy (ref JniObjectReference reference, JniObjectReferenceOptions options)
		: base (ref reference, options)
	{
	}
}
