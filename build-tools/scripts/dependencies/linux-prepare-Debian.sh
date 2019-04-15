. "`dirname $0`"/debian-common.sh

DISTRO_DEPS="$DEBIAN_COMMON_DEPS $DISTRO_DEPS \
    zulu-8
    zlib1g-dev
"

debian_install
