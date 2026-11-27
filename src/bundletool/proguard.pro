-dontobfuscate
-keepattributes Signature,*Annotation*,InnerClasses,EnclosingMethod

-keep public class com.android.tools.build.bundletool.BundleToolMain {
    public static void main(java.lang.String[]);
}

-keepclassmembers enum * {
    public static <fields>;
    public static **[] values();
    public static ** valueOf(java.lang.String);
}

# Protocol buffer descriptors and JSON parsing access generated members reflectively.
-keep class * extends com.google.protobuf.GeneratedMessageV3 { *; }
-keep class * extends com.google.protobuf.GeneratedMessageV3$Builder { *; }
-keep class * extends com.google.protobuf.GeneratedMessageLite { *; }
-keep class * extends com.google.protobuf.GeneratedMessageLite$Builder { *; }
-keep class * implements com.google.protobuf.ProtocolMessageEnum { *; }

# Gson uses fields and generic type information rather than direct references.
-keepclassmembers class * {
    @com.google.gson.annotations.SerializedName <fields>;
}

# APK signature decoding constructs annotated ASN.1 models and reads their fields.
-keep @com.android.apksig.internal.asn1.Asn1Class class * { *; }

-keep class org.slf4j.impl.StaticLoggerBinder { *; }

# Providers named in the XML pull-parser factory resource.
-keep class org.kxml2.io.KXmlParser { *; }
-keep class org.kxml2.io.KXmlSerializer { *; }

# The untouched upstream R8 library retains original type names in metadata.
-dontwarn com.android.tools.r8.**

# Optional dependencies, including JNA removed from the packaged input JAR.
-dontwarn com.sun.jna.**
-dontwarn org.bouncycastle.**
-dontwarn javax.annotation.**
-dontwarn com.google.errorprone.annotations.**
-dontwarn com.google.j2objc.annotations.**
-dontwarn org.codehaus.mojo.animal_sniffer.IgnoreJRERequirement
-dontwarn java.lang.invoke.MethodHandle
