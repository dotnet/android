using Java.Interop;

namespace Android.Content;

public partial class BroadcastReceiverAttribute
{
	string IJniNameProviderAttribute.Name => Name ?? "";
}
