ARG BUILD_DIR=/app/build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_DIR
WORKDIR /app
COPY src/RoboRuckus ./src
WORKDIR /app/src
RUN dotnet build "RoboRuckus.csproj" -v m -c Release -o $BUILD_DIR

FROM mcr.microsoft.com/dotnet/sdk:9.0
ARG BUILD_DIR
WORKDIR /app
COPY --from=build $BUILD_DIR/GameConfig/ /default/GameConfig
COPY --from=build $BUILD_DIR/wwwroot/images/boards /default/images
COPY --from=build $BUILD_DIR ./
COPY --chmod=755 startup.sh ./
ENV ASPNETCORE_URLS=http://*:8082

CMD ["./startup.sh"]