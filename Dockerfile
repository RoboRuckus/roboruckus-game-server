FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /app
COPY src/RoboRuckus /app/src
WORKDIR /app/src
RUN dotnet build "RoboRuckus.csproj" -v m -c Release -o /app/build

FROM mcr.microsoft.com/dotnet/sdk:7.0
WORKDIR /app
COPY --from=build /app/build/GameConfig/Boards /default/boards
COPY --from=build /app/build/wwwroot/images/boards /default/images
COPY --from=build /app/build /app
COPY startup.sh /app
ENV ASPNETCORE_URLS=http://*:8082

CMD ["./startup.sh"]