#
# JDK Path Probing
#
# Inputs:
#
#   $(OS): `uname` value of the host operating system
#   $(CONFIGURATION): Build configuration name, e.g. Debug or Release
#
# Outputs:
#
#   bin/Build$(CONFIGURATION)/JdkInfo.props:
#       MSBuild property file which contains a @(JdkIncludePath) MSBuild
#       ItemGroup which contains the $(JI_JDK_INCLUDE_PATHS) values, and
#       a $(JdkJvmPath) MSBuild Property which contains $(JI_JVM_PATH).
#   $(JI_JDK_INCLUDE_PATHS):
#       One or more space separated paths which contain directories to pass as
#       -Ipath values to the compiler.
#       It DOES NOT contain the -I itself; use $(JI_JDK_INCLUDE_PATHS:%=-I%) for that.
#   $(JI_JVM_PATH):
#       Location of the Java native library that contains e.g. JNI_CreateJavaVM().



ifeq ($(OS),Darwin)

_MONO_BITNESS = $(shell file `which $(word 1, $(RUNTIME))` | awk 'BEGIN { val = "32-bit" } /64-bit/ { val = "64-bit" } END { print val; }')

ifeq ($(_MONO_BITNESS),32-bit)
# The only 32-bit JVM I know of is the Apple-provided one.
JI_JVM_PATH	= /System/Library/Frameworks/JavaVM.framework/JavaVM
endif # 32-bit

# Darwin supports three possible search locations:
#
# 1. `/Library/Java/JavaVirtualMachines/jdk*`
#     These are where 3rd party JDKs are installed, such as the Oracle JDK.
#     This is the preferred search directory.
#
# 2. The Xcode.app MacOSX.platform SDK, which is for the ancient JDK6 that
#     continues to be available
#
# 3. A "locally" hosted .pkg, in case Xcode.app isn't installed.

_DARWIN_JDK_FALLBACK_DIRS         = $(wildcard /Library/Java/JavaVirtualMachines/jdk*)
_DARWIN_JDK_JNI_INCLUDE_DIR       = Contents/Home/include
_DARWIN_JDK_JNI_OS_INCLUDE_DIR    = $(_DARWIN_JDK_JNI_INCLUDE_DIR)/darwin

_XCODE_APP_JAVAVM_FRAMEWORK_PATH  = \
	$(word 1, $(wildcard /Applications/Xcode.app/Contents/Developer/Platforms/MacOSX.platform/Developer/SDKs/MacOSX*.sdk/System/Library/Frameworks/JavaVM.framework/Headers))


_LOCAL_JDK_PKG                    = JavaDeveloper-2013005_dp__11m4609.pkg
_LOCAL_JDK_URL                    = http://storage.bos.xamarin.com/android-sdk-tool/archives/$(FALLBACK_JDK_PKG)
_LOCAL_JDK_HEADERS                = LocalJDK/System/Library/Frameworks/JavaVM.framework/Versions/A/Headers

# Ancient source for (3)
_APPLE_JDK6_URL                   = http://adcdownload.apple.com/Developer_Tools/java_for_os_x_2013005_developer_package/java_for_os_x_2013005_dp__11m4609.dmg

ifneq ($(_DARWIN_JDK_FALLBACK_DIRS),)
_DARWIN_JDK_ROOT      := $(shell ls -dtr $(_DARWIN_JDK_FALLBACK_DIRS) | sort | tail -1)
JI_JDK_INCLUDE_PATHS  = \
	$(_DARWIN_JDK_ROOT)/$(_DARWIN_JDK_JNI_INCLUDE_DIR) \
	$(_DARWIN_JDK_ROOT)/$(_DARWIN_JDK_JNI_OS_INCLUDE_DIR)

ifeq ($(_MONO_BITNESS),64-bit)
JI_JVM_PATH	= $(_DARWIN_JDK_ROOT)/Contents/Home/jre/lib/server/libjvm.dylib
endif # 64-bit

else    # (1) failed; try Xcode.app's copy?
ifneq ($(_XCODE_APP_JAVAVM_FRAMEWORK_PATH),)
JI_JDK_INCLUDE_PATHS  = $(_XCODE_APP_JAVAVM_FRAMEWORK_PATH)
else    # (2) failed; hail mary pass!
JI_JDK_INCLUDE_PATHS  = LocalJDK/System/Library/Frameworks/JavaVM.framework/Versions/A/Headers

bin/Build$(CONFIGURATION)/JdkHeaders.props: $(JI_JDK_INCLUDE_PATHS)/jni.h

$(JI_JDK_INCLUDE_PATHS)/jni.h:
	@if [ ! -f $(_LOCAL_JDK_PKG) ]; then \
		curl -o $(_LOCAL_JDK_PKG) $(_LOCAL_JDK_URL) ; \
	fi
	-mkdir LocalJDK
	_jdk="$$(cd `dirname "$(_LOCAL_JDK_PKG)"`; pwd)/`basename "$(_LOCAL_JDK_PKG)"`" ; \
	(cd LocalJDK; xar -xf $$_jdk)
	(cd LocalJDK; gunzip -c JavaEssentialsDev.pkg/Payload | cpio -i)
endif   # (3)
endif   # (1)

endif   # Darwin


ifeq ($(OS),Linux)

# This is for Ubuntu and derivatives (possibly Debian too)
_LINUX_JAVA_INCLUDE_DIRS          = /usr/lib/jvm/default-java/include/
_LINUX_JAVA_FALLBACK_DIRS         = /usr/lib/jvm/java*
_LINUX_JAVA_JNI_INCLUDE_DIR       = include
_LINUX_JAVA_JNI_OS_INCLUDE_DIR    = $(DESKTOP_JAVA_JNI_INCLUDE_DIR)/linux


ifeq ($(wildcard $(DESKTOP_JAVA_INCLUDE_DIRS)),)
JI_JDK_INCLUDE_PATHS  = $(wildcard $(JAVA_HOME)/include)
endif
ifeq ($(wildcard $(JI_JDK_INCLUDE_PATHS)),)
LATEST_JDK            := $(shell ls -dtr $(_LINUX_JAVA_FALLBACK_DIRS) | sort | tail -1)
JI_JDK_INCLUDE_PATHS  = $(LATEST_JDK)/$(_LINUX_JAVA_JNI_INCLUDE_DIR) $(LATEST_JDK)/$(_LINUX_JAVA_JNI_OS_INCLUDE_DIR)
endif

endif   # Linux

$(JI_JVM_PATH):
	@echo "error: No JVM found\!";
	@exit 1

bin/Build$(CONFIGURATION)/JdkInfo.props: $(JI_JDK_INCLUDE_PATHS) $(JI_JVM_PATH)
	-mkdir -p `dirname "$@"`
	-rm "$@"
	echo '<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">' > "$@"
	echo '  <PropertyGroup>' >> "$@"
	echo "    <JdkJvmPath>$(JI_JVM_PATH)</JdkJvmPath>" >> "$@"
	echo '  </PropertyGroup>' >> "$@"
	echo '  <ItemGroup>' >> "$@"
	for p in $(JI_JDK_INCLUDE_PATHS); do \
		echo "    <JdkIncludePath Include=\"$$p\" />" >> "$@"; \
	done
	echo '  </ItemGroup>' >> "$@"
	echo '</Project>' >> "$@"
