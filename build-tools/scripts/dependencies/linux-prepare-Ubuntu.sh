. "`dirname $0`"/debian-common.sh

DISTRO_DEPS="$DEBIAN_COMMON_DEPS"

if [ "$OS_ARCH" = "x86_64" ]; then
DISTRO_DEPS="$DISTRO_DEPS
	libx32tinfo-dev
	linux-libc-dev:i386
	zlib1g-dev:i386
	"
fi

debian_install
