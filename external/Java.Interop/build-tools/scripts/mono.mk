
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
	echo '  <PropertyGroup>' >> "$@"
	echo "    <MonoFrameworkPath>$(JI_MONO_FRAMEWORK_PATH)</MonoFrameworkPath>" >> "$@"
	echo '    <MonoLibs>$(JI_MONO_LIBS)</MonoLibs>'
	echo '  </PropertyGroup>' >> "$@"
	echo '  <ItemGroup>' >> "$@"
	for p in $(JI_MONO_INCLUDE_PATHS); do \
		echo "    <MonoIncludePath Include=\"$$p\" />" >> "$@"; \
	done
	echo '  </ItemGroup>' >> "$@"
	echo '</Project>' >> "$@"
