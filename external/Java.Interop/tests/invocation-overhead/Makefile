JNIENV_GEN        = ../../bin/Debug/jnienv-gen.exe
LOCAL_JDK_HEADERS = ../../LocalJDK/System/Library/Frameworks/JavaVM.framework/Versions/A/Headers

all: test-overheads.exe libJavaInterop.dylib

clean:
	-rm test-overheads.exe test-overheads.exe.mdb
	-rm -Rf libJavaInterop.dylib*

HANDLE_FEATURES = \
	-d:FEATURE_HANDLES_ARE_SAFE_HANDLES \
	-d:FEATURE_HANDLES_ARE_INTPTRS \
	-d:FEATURE_HANDLES_ARE_INTPTRS_WITH_PINVOKES_EX \
	-d:FEATURE_HANDLES_ARE_XA_INTPTRS

test-overheads.exe: test-overheads.cs jni.cs
	mcs -out:$@ -unsafe $(HANDLE_FEATURES) $^

jni.c jni.cs: $(JNIENV_GEN)
	mono $< jni.cs jni.c

libJavaInterop.dylib: jni.c
	gcc -g -shared -o $@ $< -m32 -I $(LOCAL_JDK_HEADERS)

run:
	mono --debug test-overheads.exe