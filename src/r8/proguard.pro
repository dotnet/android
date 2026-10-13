-dontobfuscate
-keepattributes Exceptions,InnerClasses,Signature,SourceFile,LineNumberTable,*Annotation*,EnclosingMethod

# Unchanged R8 callers, reflection, and service providers require the complete ABI.
-keep class ** { *; }

# R8 is intentionally a library referencing these kept program classes.
# Upstream relocation also leaves original type names in generic signatures.
-dontwarn com.android.tools.r8.**
-dontwarn com.google.protobuf.**

# Compile-time annotations absent from the distributed R8 JAR.
-dontwarn com.google.errorprone.annotations.**
-dontwarn javax.annotation.**
-dontwarn org.codehaus.mojo.animal_sniffer.IgnoreJRERequirement
