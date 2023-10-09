#!/bin/bash
cd ~/
wget https://github.com/RoboRuckus/roboruckus-game-server/archive/refs/heads/main.zip -O Ruckus.zip
unzip -o Ruckus.zip -d ./
systemctl stop RoboRuckus
pkill RoboRuckus
cp -R RoboRuckus-master/PiReady/RoboRuckus ~/RoboRuckus
chmod +x ~/RoboRuckus/RoboRuckus ~/RoboRuckus/start_roboruckus.sh
rm Ruckus.zip
rm -R RoboRuckus-master
systemctl start RoboRuckus
exit 0