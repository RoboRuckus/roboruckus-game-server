#!/bin/sh

cp -r /default/boards/* /app/GameConfig/Boards/
cp -r /default/images/* /app/wwwroot/images/boards/

./RoboRuckus --options=botless
