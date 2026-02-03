# classes.dex Comparison: MonoVM vs NewTypeMap

## Summary
- MonoVM: 64 class references
- NewTypeMap: 60 class references

## Full Class List

| Class | MonoVM | NewTypeMap |
|-------|:------:|:----------:|
| `Landroid/app/ActionBar$Tab;` | ❌ | ✅ |
| `Landroid/app/Activity;` | ✅ | ✅ |
| `Landroid/app/Application;` | ✅ | ✅ |
| `Landroid/app/IntentService;` | ✅ | ✅ |
| `Landroid/app/WallpaperColors;` | ❌ | ✅ |
| `Landroid/content/ContentValues;` | ✅ | ✅ |
| `Landroid/content/Context;` | ✅ | ✅ |
| `Landroid/content/Intent;` | ✅ | ✅ |
| `Landroid/content/IntentFilter;` | ✅ | ✅ |
| `Landroid/database/Cursor;` | ✅ | ✅ |
| `Landroid/net/Uri;` | ✅ | ✅ |
| `Landroid/os/Build;` | ✅ | ✅ |
| `Landroid/os/Build$VERSION;` | ✅ | ✅ |
| `Landroid/os/Bundle;` | ✅ | ✅ |
| `Landroid/util/Log;` | ✅ | ✅ |
| `Landroid/widget/DatePicker;` | ❌ | ✅ |
| `Landroid/widget/TimePicker;` | ❌ | ✅ |
| `Lcom/typemap/mono/R;` | ✅ | ❌ |
| `Lcom/typemap/mono/R$color;` | ✅ | ❌ |
| `Lcom/typemap/mono/R$layout;` | ✅ | ❌ |
| `Lcom/typemap/mono/R$mipmap;` | ✅ | ❌ |
| `Lcom/typemap/mono/R$string;` | ✅ | ❌ |
| `Ldalvik/annotation/InnerClass;` | ✅ | ✅ |
| `Ldalvik/annotation/Signature;` | ✅ | ✅ |
| `Ldalvik/annotation/Throws;` | ✅ | ❌ |
| `Ljava/io/File;` | ✅ | ✅ |
| `Ljava/io/InputStream;` | ✅ | ❌ |
| `Ljava/io/OutputStream;` | ✅ | ❌ |
| `Ljava/lang/CharSequence;` | ✅ | ✅ |
| `Ljava/lang/Class;` | ✅ | ✅ |
| `Ljava/lang/ClassLoader;` | ✅ | ✅ |
| `Ljava/lang/Error;` | ✅ | ✅ |
| `Ljava/lang/Math;` | ✅ | ✅ |
| `Ljava/lang/Object;` | ✅ | ✅ |
| `Ljava/lang/reflect/Field;` | ✅ | ✅ |
| `Ljava/lang/Runnable;` | ✅ | ✅ |
| `Ljava/lang/RuntimeException;` | ✅ | ✅ |
| `Ljava/lang/String;` | ✅ | ✅ |
| `Ljava/lang/StringBuilder;` | ✅ | ✅ |
| `Ljava/lang/System;` | ✅ | ✅ |
| `Ljava/lang/Thread;` | ✅ | ✅ |
| `Ljava/lang/Throwable;` | ✅ | ✅ |
| `Ljava/net/Socket;` | ✅ | ✅ |
| `Ljava/nio/Buffer;` | ✅ | ✅ |
| `Ljava/nio/ByteBuffer;` | ✅ | ✅ |
| `Ljava/security/Key;` | ✅ | ✅ |
| `Ljava/security/PrivateKey;` | ✅ | ✅ |
| `Ljava/time/OffsetDateTime;` | ✅ | ✅ |
| `Ljava/time/ZoneOffset;` | ✅ | ✅ |
| `Ljava/util/ArrayList;` | ✅ | ✅ |
| `Ljava/util/Calendar;` | ✅ | ✅ |
| `Ljava/util/Iterator;` | ✅ | ✅ |
| `Ljava/util/List;` | ✅ | ✅ |
| `Ljava/util/Locale;` | ✅ | ✅ |
| `Ljava/util/TimeZone;` | ✅ | ✅ |
| `Ljavax/crypto/Mac;` | ✅ | ✅ |
| `Ljavax/net/ssl/X509KeyManager;` | ✅ | ✅ |
| `Lmono/android/BuildConfig;` | ✅ | ✅ |
| `Lmono/android/DebugRuntime;` | ✅ | ✅ |
| `Lmono/android/GCUserPeer;` | ✅ | ✅ |
| `Lmono/android/IGCUserPeer;` | ✅ | ✅ |
| `Lmono/android/MultiDexLoader;` | ✅ | ✅ |
| `Lmono/android/Runtime;` | ✅ | ✅ |
| `Lmono/android/TypeManager;` | ✅ | ❌ |
| `Lmono/java/lang/Runnable;` | ❌ | ✅ |
| `Lmono/MonoPackageManager;` | ✅ | ✅ |
| `Lmono/MonoRuntimeProvider;` | ✅ | ✅ |
| `Lnet/dot/jni/GCUserPeerable;` | ✅ | ✅ |
| `Lnet/dot/jni/ManagedPeer;` | ✅ | ✅ |

## Differences Only

| Class | MonoVM | NewTypeMap | Notes |
|-------|:------:|:----------:|-------|
| `Landroid/app/ActionBar$Tab;` | ❌ | ✅ | **Extra in NewTypeMap** |
| `Landroid/app/WallpaperColors;` | ❌ | ✅ | **Extra in NewTypeMap** |
| `Landroid/widget/DatePicker;` | ❌ | ✅ | **Extra in NewTypeMap** |
| `Landroid/widget/TimePicker;` | ❌ | ✅ | **Extra in NewTypeMap** |
| `Lmono/java/lang/Runnable;` | ❌ | ✅ | **Extra in NewTypeMap** |
| `Lcom/typemap/mono/R;` | ✅ | ❌ | Missing in NewTypeMap |
| `Lcom/typemap/mono/R$color;` | ✅ | ❌ | Missing in NewTypeMap |
| `Lcom/typemap/mono/R$layout;` | ✅ | ❌ | Missing in NewTypeMap |
| `Lcom/typemap/mono/R$mipmap;` | ✅ | ❌ | Missing in NewTypeMap |
| `Lcom/typemap/mono/R$string;` | ✅ | ❌ | Missing in NewTypeMap |
| `Ldalvik/annotation/Throws;` | ✅ | ❌ | Missing in NewTypeMap |
| `Ljava/io/InputStream;` | ✅ | ❌ | Missing in NewTypeMap |
| `Ljava/io/OutputStream;` | ✅ | ❌ | Missing in NewTypeMap |
| `Lmono/android/TypeManager;` | ✅ | ❌ | Missing in NewTypeMap |
