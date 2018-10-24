. "`dirname $0`"/debian-common.sh

DISTRO_DEPS="$DEBIAN_COMMON_DEPS \
    zlib1g-dev
"

if [ "$OS_ARCH" = "x86_64" ]; then
	DISTRO_DEPS="$DISTRO_DEPS lib32tinfo-dev"
else
	DISTRO_DEPS="$DISTRO_DEPS lib64ncurses5-dev"
fi

debian_install
