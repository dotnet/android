if [ "$OS_ARCH" = "x86_64" ]; then
    DISTRO_DEPS="$DISTRO_DEPS
	libx32tinfo-dev
	linux-libc-dev:i386
	zlib1g-dev:i386
	"
fi

if [ "$NEED_LIBTOOL" = "yes" ]; then
    DISTRO_DEPS="$DISTRO_DEPS libtool-bin"
fi
