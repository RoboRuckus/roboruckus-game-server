#!/usr/bin/env /bin/bash

cp -r /default/boards/* /app/GameConfig/Boards/
cp -r /default/images/* /app/wwwroot/images/boards/

# Run the service
./RoboRuckus