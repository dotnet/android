. "`dirname $0`"/debian-common.sh

DISTRO_DEPS="$DEBIAN_COMMON_DEPS \
    zlib1g-dev
"

debian_install
