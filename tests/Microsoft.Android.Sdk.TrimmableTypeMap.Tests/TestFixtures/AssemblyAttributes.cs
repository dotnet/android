using Android.App;

[assembly: UsesFeature ("android.hardware.camera")]
[assembly: UsesFeature ("android.hardware.camera.autofocus", Required = false)]
[assembly: UsesFeature (GLESVersion = 0x00020000)]
[assembly: UsesPermission ("android.permission.INTERNET")]
[assembly: UsesPermission ("android.permission.POST_NOTIFICATIONS", UsesPermissionFlags = "neverForLocation")]
[assembly: UsesLibrary ("org.apache.http.legacy")]
[assembly: UsesLibrary ("com.example.optional", false)]
[assembly: MetaData ("com.example.key", Value = "test-value")]
[assembly: SupportsGLTexture ("GL_OES_compressed_ETC1_RGB8_texture")]
