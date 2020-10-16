#!/system/bin/sh
HERE="$(cd "$(dirname "$0")" && pwd)"
UBSAN_LIB=$(ls $HERE/libclang_rt.ubsan_standalone-*-android.so)
if [ -f "$HERE/libc++_shared.so" ]; then
    # Workaround for https://github.com/android-ndk/ndk/issues/988.
    export LD_PRELOAD="$UBSAN_LIB $HERE/libc++_shared.so"
else
    export LD_PRELOAD="$UBSAN_LIB"
fi
"$@"
