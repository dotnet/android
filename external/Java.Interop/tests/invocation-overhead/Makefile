all: test-overheads.exe test-overheads-xa.exe

test-overheads.exe: test-overheads.cs jni.cs
	mcs -out:$@ -unsafe -d:FEATURE_HANDLES_ARE_SAFE_HANDLES -d:FEATURE_HANDLES_ARE_INTPTRS $^

test-overheads-xa.exe: test-overheads.cs jni-xa.cs
	mcs -out:$@ -unsafe -d:XA -d:FEATURE_HANDLES_ARE_SAFE_HANDLES -d:FEATURE_HANDLES_ARE_INTPTRS $^

run:
	mono --debug test-overheads.exe