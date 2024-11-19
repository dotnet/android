using Java.Interop;

namespace Android.App;

public sealed partial class ServiceAttribute
{
	string IJniNameProviderAttribute.Name => Name ?? "";
}
