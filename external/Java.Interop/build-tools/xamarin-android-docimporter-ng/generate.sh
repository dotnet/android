#!/bin/bash

ANDROID_TOOLCHAIN=~/android-toolchain

DROID_DOC_API_LEVELS="10 15 16 17 18 19 20 21 22 23"
JAVA_STUB_API_LEVELS="24 25 26 27 28 29"


for n in $JAVA_STUB_API_LEVELS
do
	time mono --debug xamarin-android-docimporter-ng/bin/Debug/xamarin-android-docimporter-ng.exe -source-stub-zip=$ANDROID_TOOLCHAIN/sdk/platforms/android-$n/android-stubs-src.jar -output-text=api-$n.params.txt -output-xml=api-$n.params.xml -verbose -framework-only
done

for API_LEVEL in $DROID_DOC_API_LEVELS
do
	time mono --debug xamarin-android-docimporter-ng/bin/Debug/xamarin-android-docimporter-ng.exe -droiddoc=$ANDROID_TOOLCHAIN/docs/docs-api-$API_LEVEL/ -output-text=api-$API_LEVEL.params.txt -output-xml=api-$API_LEVEL.params.xml -verbose -framework-only
done
