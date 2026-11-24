-dontobfuscate

-keep public class com.xamarin.manifestmerger.Main {
    public static void main(java.lang.String[]);
}

-keepclassmembers enum * {
    public static **[] values();
    public static ** valueOf(java.lang.String);
}

# Optional dependencies of the bundled libraries, not used by the merger CLI.
-dontwarn android.annotation.SuppressLint
-dontwarn com.github.luben.zstd.**
-dontwarn com.google.auto.service.AutoService
-dontwarn com.ibm.icu.lang.UCharacter
-dontwarn com.sun.activation.registries.**
-dontwarn javax.servlet.**
-dontwarn org.apache.avalon.framework.logger.Logger
-dontwarn org.apache.log.**
-dontwarn org.apache.log4j.**
-dontwarn org.brotli.dec.BrotliInputStream
-dontwarn org.codehaus.mojo.animal_sniffer.IgnoreJRERequirement
-dontwarn org.objectweb.asm.**
-dontwarn org.tukaani.xz.**
-dontwarn org.apache.commons.compress.harmony.pack200.**
# JSR 305 annotations; Guava no longer brings in com.google.code.findbugs:jsr305.
-dontwarn javax.annotation.**

# Signature-polymorphic calls have descriptors that differ from the JDK declaration.
-dontwarn java.lang.invoke.MethodHandle
-dontwarn java.lang.invoke.VarHandle
