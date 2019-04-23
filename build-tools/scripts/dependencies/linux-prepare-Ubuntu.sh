. "`dirname $0`"/debian-common.sh

MAJOR=$(echo $1 | cut -d '.' -f 1)
MINOR=$(echo $1 | cut -d '.' -f 2)

if [ $MAJOR -eq 17 -a $MINOR -eq 10 ] || [ $MAJOR -ge 18 ]; then
    NEED_LIBTOOL=yes
    if [ $MAJOR -lt 19 ]; then
        DISTRO_DEPS="$DEBIAN_COMMON_DEPS openjdk-8-jdk"
    fi
else
    NEED_LIBTOOL=no
fi

. "`dirname $0`"/ubuntu-common.sh

debian_install
