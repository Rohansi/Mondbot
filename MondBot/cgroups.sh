#!/bin/bash

if [ -z "$MOND_CGROUP_BASE" ]; then
	echo "MOND_CGROUP_BASE is not set!"
	exit 1
fi

SLAVE_CGROUP="$MOND_CGROUP_BASE/mondbot-slave"

sudo cgdelete -g memory:$SLAVE_CGROUP
sudo cgcreate -a rohan:rohan -t rohan:rohan -g memory:$SLAVE_CGROUP
echo "256M" > /sys/fs/cgroup/$SLAVE_CGROUP/memory.high
echo "300M" > /sys/fs/cgroup/$SLAVE_CGROUP/memory.max
