JNIENV_GEN        = ../../bin/Debug/jnienv-gen.exe
LOCAL_JDK_HEADERS = ../../LocalJDK/System/Library/Frameworks/JavaVM.framework/Versions/A/Headers

all: test-overheads.exe libjava-interop.dylib

clean:
	-rm test-overheads.exe test-overheads.exe.mdb
	-rm -Rf libJavaInterop.dylib*

HANDLE_FEATURES = \
	-d:FEATURE_JNIENVIRONMENT_JI_INTPTRS \
	-d:FEATURE_JNIENVIRONMENT_JI_PINVOKES \
	-d:FEATURE_JNIENVIRONMENT_SAFEHANDLES \
	-d:FEATURE_JNIENVIRONMENT_XA_INTPTRS

test-overheads.exe: test-overheads.cs jni.cs
	mcs -out:$@ -unsafe $(HANDLE_FEATURES) $^

jni.c jni.cs: $(JNIENV_GEN)
	mono $< jni.cs jni.c

libjava-interop.dylib: jni.c
	gcc -g -shared -o $@ $< -m32 -DJI_DLL_EXPORT -fvisibility=hidden -I $(LOCAL_JDK_HEADERS)

run:
	mono --debug test-overheads.exe