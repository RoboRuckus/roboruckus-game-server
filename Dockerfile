FROM mcr.microsoft.com/dotnet/sdk:7.0-jammy AS build
WORKDIR /app
RUN git clone https://github.com/RoboRuckus/roboruckus-game-server.git
WORKDIR  /app/roboruckus-game-server/src/RoboRuckus
RUN dotnet build "RoboRuckus.csproj" -v m -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RoboRuckus.csproj" -v m -c Release -o /app/publish --no-self-contained

FROM  mcr.microsoft.com/dotnet/sdk:7.0-jammy
WORKDIR /app
COPY --from=publish /app/build/GameConfig/Boards /default/boards
COPY --from=publish /app/build/wwwroot/images/boards /default/images
COPY --from=publish /app/build /app
COPY startup.sh /app/
ENV ASPNETCORE_URLS=http://*:8082

CMD ["./startup.sh"]