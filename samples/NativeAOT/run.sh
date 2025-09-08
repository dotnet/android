#!/bin/bash -e
APK="bin/Debug/net10.0-android/net.dot.hellonativeaot-Signed.apk"
PACKAGE="net.dot.hellonativeaot"
ACTIVITY="my.MainActivity"

FAILED=no
rm -rf bin obj
../../dotnet-local.sh build -bl || FAILED=yes
../../dotnet-local.sh msbuild -v:diag msbuild.binlog > msbuild.txt
if [ "${FAILED}" == "yes" ]; then
  echo Build failed
  exit 1
fi

if [ -n "${1}" ]; then
  exit 0
fi

adb uninstall "${PACKAGE}" || true
adb install -r -d --no-streaming --no-fastdeploy "${APK}"
adb shell setprop debug.mono.log default,assembly,timing=bare
adb logcat -G 128M
adb logcat -c
adb shell am start -S --user "0" -a "android.intent.action.MAIN" -c "android.intent.category.LAUNCHER" -n "${PACKAGE}/${ACTIVITY}" -W
echo "Waiting for the app to start..."
sleep 5
adb logcat -d > log.txt