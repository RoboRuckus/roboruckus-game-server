#!/bin/bash

cp -r /default/GameConfig/* /app/GameConfig/
cp -r /default/images/* /app/wwwroot/images/boards/

# Run the service
./RoboRuckus $ARGS