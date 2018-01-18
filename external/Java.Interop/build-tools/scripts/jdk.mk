#
# JDK Path Probing
#
# Inputs:
#
#   $(OS): Optional; **uname**(1) value of the host operating system
#   $(CONFIGURATION): Build configuration name, e.g. Debug or Release
#   $(RUNTIME): `mono` executable for the host operating system
#   $(JI_MAX_JDK):
#       Maximum allowed JDK version, blank by default.
#       `xamarin-android` will need to specify 8 here
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
#   $(JI_JDK_BIN_PATH):
#       Location of the JDK `/bin` directory, which contains `java/`javac`/etc.

OS           ?= $(shell uname)
JI_JAVAC_PATH = javac
JI_JAR_PATH   = jar

JI_JDK_BIN_PATH = $(dir $(shell which java))


# Filter on <= JI_MAX_JDK
ifneq ($(JI_MAX_JDK),)
_VERSION_MAX  := | awk '$$1 <= $(JI_MAX_JDK)'
endif #JI_MAX_JDK

# Sort numerically on version numbers with `sort -n`, filtering on $(JI_MAX_JDK) if needed
# Replace each line so it starts with a number (sed 's/...'\1 &/), sort on the leading number, then remove the leading number.
# Grab the last path name printed.
_VERSION_SORT := sed 's/[^0-9]*\([0-9.]*\)/\1 &/' $(_VERSION_MAX) | sort -n | sed 's/^[0-9.]* //g' | tail -1

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
_DARWIN_JDK_ROOT      := $(shell ls -dtr $(_DARWIN_JDK_FALLBACK_DIRS) | $(_VERSION_SORT))
JI_JDK_BIN_PATH       = $(_DARWIN_JDK_ROOT)/Contents/Home/bin
JI_JAVAC_PATH         = $(_DARWIN_JDK_ROOT)/Contents/Home/bin/javac
JI_JAR_PATH           = $(_DARWIN_JDK_ROOT)/Contents/Home/bin/jar
JI_JDK_INCLUDE_PATHS  = \
	$(_DARWIN_JDK_ROOT)/$(_DARWIN_JDK_JNI_INCLUDE_DIR) \
	$(_DARWIN_JDK_ROOT)/$(_DARWIN_JDK_JNI_OS_INCLUDE_DIR)

ifeq ($(_MONO_BITNESS),64-bit)
JI_JVM_PATH	= $(shell find $(_DARWIN_JDK_ROOT)/Contents/Home -name libjli.dylib)
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

# This is for all linux distributions with which and java installed
_DEFAULT_LINUX_JAVA_ROOT          = $(shell java -XshowSettings:properties -help 2>&1 | grep java.home | sed 's/^.*java.home = //g')/../
_DEFAULT_LINUX_JAVA_INCLUDE_DIRS  = $(_DEFAULT_LINUX_JAVA_ROOT)/include/
_LINUX_JAVA_FALLBACK_DIRS         = /usr/lib/jvm/java*
_LINUX_JAVA_JNI_INCLUDE_DIR       = include
_LINUX_JAVA_ROOT                  = $(_DEFAULT_LINUX_JAVA_ROOT)
_LINUX_JAVA_ARCH_64               = amd64
_LINUX_JAVA_ARCH_32               = i386

_DESKTOP_JAVA_INCLUDE_DIRS = $(_DEFAULT_LINUX_JAVA_INCLUDE_DIRS)

ifeq ($(wildcard $(_DESKTOP_JAVA_INCLUDE_DIRS)),)
_DESKTOP_JAVA_INCLUDE_DIRS  = $(wildcard $(JAVA_HOME)/include)
_LINUX_JAVA_ROOT            = $(JAVA_HOME)
endif # No default Java location, $JAVA_HOME check

ifeq ($(wildcard $(_DESKTOP_JAVA_INCLUDE_DIRS)),)
LATEST_JDK                  := $(shell ls -dtr $(_LINUX_JAVA_FALLBACK_DIRS) | $(_VERSION_SORT))
_DESKTOP_JAVA_INCLUDE_DIRS  = $(LATEST_JDK)/$(_LINUX_JAVA_JNI_INCLUDE_DIR)
_LINUX_JAVA_ROOT            = $(LATEST_JDK)
endif # No $JAVA_HOME, find the latest version

JI_JDK_INCLUDE_PATHS = $(_DESKTOP_JAVA_INCLUDE_DIRS) $(_DESKTOP_JAVA_INCLUDE_DIRS)/linux

ifneq ($(wildcard $(_LINUX_JAVA_ROOT)/jre/lib/$(_LINUX_JAVA_ARCH_64)/server/libjvm.so),)
JI_JVM_PATH                 = $(_LINUX_JAVA_ROOT)/jre/lib/$(_LINUX_JAVA_ARCH_64)/server/libjvm.so
endif # Find 64-bit libjvm

ifeq ($(JI_JVM_PATH),) # (1) No 64-bit java arch
ifneq ($(wildcard $(_LINUX_JAVA_ROOT)/jre/lib/$(_LINUX_JAVA_ARCH_32)/server/libjvm.so),) # (2) check 32-bit instead, even on a 64-bit system
JI_JVM_PATH                 = $(_LINUX_JAVA_ROOT)/jre/lib/$(_LINUX_JAVA_ARCH_32)/server/libjvm.so
endif # (2)
endif # (1)

JI_JDK_BIN_PATH             = $(_LINUX_JAVA_ROOT)/bin
JI_JAVAC_PATH               = $(_LINUX_JAVA_ROOT)/bin/javac
JI_JAR_PATH                 = $(_LINUX_JAVA_ROOT)/bin/jar

endif   # Linux

$(JI_JVM_PATH):
	@echo "error: No JVM found\!";
	@exit 1

bin/Build$(CONFIGURATION)/JdkInfo.props: $(JI_JDK_INCLUDE_PATHS) $(JI_JVM_PATH)
	-mkdir -p `dirname "$@"`
	-rm "$@"
	echo '<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">' > "$@"
	echo '  <Choose>' >> "$@"
	echo "    <When Condition=\" '\$$(JdkJvmPath)' == '' \">" >> "$@"
	echo '      <PropertyGroup>' >> "$@"
	echo "        <JdkJvmPath>$(JI_JVM_PATH)</JdkJvmPath>" >> "$@"
	echo '      </PropertyGroup>' >> "$@"
	echo '      <ItemGroup>' >> "$@"
	for p in $(JI_JDK_INCLUDE_PATHS); do \
		echo "        <JdkIncludePath Include=\"$$p\" />" >> "$@"; \
	done
	echo '      </ItemGroup>' >> "$@"
	echo '    </When>' >> "$@"
	echo '  </Choose>' >> "$@"
	echo '  <PropertyGroup>' >> "$@"
	echo "    <JdkBinPath Condition=\" '\$$(JdkBinPath)' == '' \">$(JI_JDK_BIN_PATH)</JdkBinPath>" >> "$@"
	echo "    <JavaCPath Condition=\" '\$$(JavaCPath)' == '' \">$(JI_JAVAC_PATH)</JavaCPath>" >> "$@"
	echo "    <JarPath Condition=\" '\$$(JarPath)' == '' \">$(JI_JAR_PATH)</JarPath>" >> "$@"
	echo '  </PropertyGroup>' >> "$@"
	echo '</Project>' >> "$@"
