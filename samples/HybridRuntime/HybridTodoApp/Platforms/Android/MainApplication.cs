using Android.App;
using Android.Runtime;

namespace HybridTodoApp;

#if HYBRID_RUNTIME
[Register ("net.dot.hybrid.ManagedMauiApplication")]
#else
[Application (Name = "net.dot.hybrid.ManagedMauiApplication")]
#endif
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
