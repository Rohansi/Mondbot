sudo cgdelete -g memory:mondbot/slave
sudo cgdelete -g memory:mondbot
sudo cgcreate -a rohan:rohan -t rohan:rohan -g memory:mondbot
sudo cgcreate -a rohan:rohan -t rohan:rohan -g memory:mondbot/slave
echo "256M" > /sys/fs/cgroup/mondbot-slave/memory.high
echo "300M" > /sys/fs/cgroup/mondbot-slave/memory.max
