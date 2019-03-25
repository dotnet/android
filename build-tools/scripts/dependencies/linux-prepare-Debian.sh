. "`dirname $0`"/debian-common.sh

MAJOR=$(echo $1 | cut -d '.' -f 1)

if [ $MAJOR -ge 10 ]; then
    DISTRO_DEPS="$DISTRO_DEPS libncurses6"
else
    DISTRO_DEPS="$DISTRO_DEPS libncurses5"
fi

DISTRO_DEPS="$DEBIAN_COMMON_DEPS $DISTRO_DEPS \
    zlib1g-dev
"

debian_install
