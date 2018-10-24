. "`dirname $0`"/debian-common.sh

DISTRO_DEPS="$DEBIAN_COMMON_DEPS"

if [ "$OS_ARCH" = "x86_64" ]; then
    DISTRO_DEPS="$DISTRO_DEPS lib32tinfo-dev"
else
    DISTRO_DEPS="$DISTRO_DEPS libtinfo-dev"
fi

MAJOR=$(echo $1 | cut -d '.' -f 1)
MINOR=$(echo $1 | cut -d '.' -f 2)

if [ $MAJOR -ge 19 ]; then
    NEED_LIBTOOL=yes
else
    NEED_LIBTOOL=no
fi

. "`dirname $0`"/ubuntu-common.sh

debian_install
