# RoboRuckus Game Server
This game server code provides all the necessary logic and control for the RoboRuckus robot game. Robots and players connect to this server to play the game, which coordinates all the game states, robot movements, and player choices.

# Deploying
There are two main ways to deploy the game server. The first, and preferred, is via Docker as detailed below. The second is to create an all-in-one solution using the instructions found on [our website](https://www.roboruckus.com/documentation/setting-up-the-game/).

## Deploying via Docker
1. First, set up Docker Engine and Compose on the computer you want to host the sever following [these instructions](https://docs.docker.com/engine/install/).
2. Create or copy the [docker-compose.yml](/docker-compose.yml) file from this repository on the server computer.
3. Run the command `docker compose up` from the same directory as the compose file.
4. That's it!

# Developing
If you want to explore and edit the code for the RoboRuckus game server, that's great! While either Visual Studio or Visual Studio Code are recommended for this, these instructions will be specific to using the cross-platform Visual Studio Code (VS Code). Here are steps necessary to get started:
1. Install [Visual Studio Code](https://code.visualstudio.com/docs/setup/setup-overview).
2. Install the [C# Dev Kit Extension](https://code.visualstudio.com/docs/languages/csharp#_installing-c35-support).
3. Optionally: If you wish to develop the Docker container [install Docker and the VS Code extension](https://code.visualstudio.com/docs/containers/overview).
4. Download or clone this GitHub repository.
5. In VS Code, open the repository folder.

While the details of .Net development are beyond the scope of this guide, you can find basic information on debugging in VS Code [here](https://code.visualstudio.com/docs/editor/debugging). If you wish to publish a native version of the server program for a specific platform, like Linux on ARM for the Raspberry Pi, you can use, and modify, this command from the VS Code terminal:

```dotnet publish RoboRuckus.sln -r linux-arm -o PiReady\RoboRuckus -c Release --self-contained -p:PublishSingleFile=true```

Details on how to manually set the game up on a Linux or Raspberry Pi server can be found [here](https://www.roboruckus.com/documentation/setting-up-the-game/#Setting_Up_RoboRuckus).
