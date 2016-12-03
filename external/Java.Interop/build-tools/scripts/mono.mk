#
# Mono Path Probing
#
# Inputs:
#
#   $(OS): Optional; **uname**(1) value of the host operating system
#   $(CONFIGURATION): Build configuration name, e.g. Debug or Release
#   $(V): Output verbosity. If != 0, then `MONO_OPTIONS` is exported with --debug.
#
# Outputs:
#
#   bin/Build$(CONFIGURATION)/MonoInfo.props:
#       MSBuild property file which contains:
#       * `$(MonoFrameworkPath)`: `$(JI_MONO_FRAMEWORK_PATH)` value.
#       * `$(MonoLibs)`: `$(JI_MONO_LIBS)` value.
#       * `@(MonoIncludePath)`: `$(JI_MONO_INCLUDE_PATHS)` values.
#   $(JI_MONO_FRAMEWORK_PATH):
#       Path to the `libmonosgen-2.0.1.dylib` file to link against.
#   $(JI_MONO_INCLUDE_PATHS):
#       One or more space separated paths containing Mono headers to pass as
#       -Ipath values to the compiler.
#       It DOES NOT contain the -I itself; use $(JI_MONO_INCLUDE_PATHS:%=-I%) for that.
#   $(JI_MONO_LIBS)
#       C compiler linker arguments to link against `$(JI_MONO_FRAMEWORK_PATH)`.
#   $(RUNTIME):
#       The **mono**(1) program to use to execute managed code.

OS            ?= $(shell uname)
RUNTIME       := $(shell if [ -f "`which mono64`" ] ; then echo mono64 ; else echo mono; fi) --debug=casts

ifneq ($(V),0)
MONO_OPTIONS  += --debug
endif   # $(V) != 0

ifneq ($(MONO_OPTIONS),)
export MONO_OPTIONS
endif   # $(MONO_OPTIONS) != ''

ifeq ($(OS),Darwin)
JI_MONO_FRAMEWORK_PATH = /Library/Frameworks/Mono.framework/Libraries/libmonosgen-2.0.1.dylib
JI_MONO_INCLUDE_PATHS = /Library/Frameworks/Mono.framework/Headers/mono-2.0
JI_MONO_LIBS = -L /Library/Frameworks/Mono.framework/Libraries -lmonosgen-2.0
endif
ifeq ($(OS),Linux)
JI_MONO_FRAMEWORK_PATH = $(shell pkg-config --variable=libdir mono-2)/libmonosgen-2.0.so
JI_MONO_INCLUDE_PATHS = $(shell pkg-config --variable=includedir mono-2)
JI_MONO_LIBS = -L $(shell pkg-config --variable=libdir mono-2) -lmonosgen-2.0
endif



$(JI_MONO_FRAMEWORK_PATH):
	@echo "error: No Mono framework found\!";
	@exit 1

bin/Build$(CONFIGURATION)/MonoInfo.props: $(JI_MONO_INCLUDE_PATHS) $(JI_MONO_FRAMEWORK_PATH)
	-mkdir -p `dirname "$@"`
	-rm "$@"
	echo '<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">' > "$@"
	echo '  <Choose>' >> "$@"
	echo "    <When Condition=\" '\$$(MonoFrameworkPath)' == '' \">" >> "$@"
	echo '      <PropertyGroup>' >> "$@"
	echo "        <MonoFrameworkPath>$(JI_MONO_FRAMEWORK_PATH)</MonoFrameworkPath>" >> "$@"
	echo "        <MonoLibs         >$(JI_MONO_LIBS)</MonoLibs>" >> "$@"
	echo '      </PropertyGroup>' >> "$@"
	echo '      <ItemGroup>' >> "$@"
	for p in $(JI_MONO_INCLUDE_PATHS); do \
		echo "        <MonoIncludePath Include=\"$$p\" />" >> "$@"; \
	done
	echo '      </ItemGroup>' >> "$@"
	echo '    </When>' >> "$@"
	echo '  </Choose>' >> "$@"
	echo '</Project>' >> "$@"
