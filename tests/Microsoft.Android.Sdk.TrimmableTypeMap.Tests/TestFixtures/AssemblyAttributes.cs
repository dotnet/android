using Android.App;

[assembly: UsesFeature ("android.hardware.camera")]
[assembly: UsesFeature ("android.hardware.camera.autofocus", Required = false)]
[assembly: UsesFeature (GLESVersion = 0x00020000)]
[assembly: UsesPermission ("android.permission.INTERNET")]
[assembly: UsesLibrary ("org.apache.http.legacy")]
[assembly: UsesLibrary ("com.example.optional", false)]
[assembly: MetaData ("com.example.key", Value = "test-value")]
