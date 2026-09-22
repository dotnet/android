namespace Android.Runtime;

public enum JniHandleOwnership
{
	DoNotTransfer,
	TransferLocalRef,
	TransferGlobalRef,
}
