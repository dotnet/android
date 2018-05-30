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

_INCLUDE_MK     = bin/Build$(CONFIGURATION)/JdkInfo.mk
_INCLUDE_PROPS  = bin/Build$(CONFIGURATION)/JdkInfo.props

prepare:: $(_INCLUDE_MK)

-include $(_INCLUDE_MK)

ifeq ($(OS),Darwin)
_JDKS_ROOT  := /Library/Java/JavaVirtualMachines
endif # $(OS)=Darwin

ifeq ($(OS),Linux)
_JDKS_ROOT  := /usr/lib/jvm
endif # $(OS)=Linux

$(_INCLUDE_MK) $(_INCLUDE_PROPS): bin/Build$(CONFIGURATION)/Java.Interop.BootstrapTasks.dll
	$(MSBUILD) $(MSBUILD_FLAGS) build-tools/scripts/jdk.targets /t:GetPreferredJdkRoot \
		/p:JdksRoot="$(_JDKS_ROOT)" \
		$(if $(JI_MAX_JDK),"/p:MaximumJdkVersion=$(JI_MAX_JDK)")
