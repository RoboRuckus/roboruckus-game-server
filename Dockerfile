FROM ubuntu:jammy AS buildlayer

RUN apt-get update && apt-get install -y git dotnet-sdk-7.0

WORKDIR /app
RUN git clone https://github.com/RoboRuckus/roboruckus-game-server.git

WORKDIR  /app/roboruckus-game-server/src/RoboRuckus
RUN dotnet build -v m RoboRuckus.csproj



FROM ubuntu:jammy

RUN apt-get update && apt-get install -y dotnet-sdk-7.0

WORKDIR /app
COPY --from=buildlayer /app/roboruckus-game-server/src/RoboRuckus/bin/Debug/net7.0/ /app

COPY --from=buildlayer /app/roboruckus-game-server/src/RoboRuckus/bin/Debug/net7.0/GameConfig/Boards /default/boards
COPY --from=buildlayer /app/roboruckus-game-server/src/RoboRuckus/bin/Debug/net7.0/wwwroot/images/boards /default/images
COPY startup.sh /app/

CMD ["./startup.sh"]
