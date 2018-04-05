. "`dirname $0`"/debian-common.sh

DISTRO_DEPS="$DEBIAN_COMMON_DEPS"

if [ "$OS_ARCH" = "x86_64" ]; then
DISTRO_DEPS="$DISTRO_DEPS
	libx32tinfo-dev
	linux-libc-dev:i386
	zlib1g-dev:i386
	"
fi

MAJOR=$(echo $1 | cut -d '.' -f 1)
MINOR=$(echo $1 | cut -d '.' -f 2)

if [ $MAJOR -eq 17 -a $MINOR -eq 10 ] || [ $MAJOR -ge 18 ]; then
	DISTRO_DEPS="$DISTRO_DEPS libtool-bin"
fi

debian_install
