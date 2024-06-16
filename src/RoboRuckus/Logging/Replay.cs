using RoboRuckus.RuckusCode;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;

namespace RoboRuckus.Logging
{
    /// <summary>
    /// Class to replay logged games
    /// </summary>
    public static class Replay
    {
        /// <summary>
        /// Set to true to abort any currently running replay simulation after the next event completes
        /// </summary>
        public static bool AbortReplay = false;

        /// <summary>
        /// Start a replay game
        /// </summary>
        /// <param name="board">The board to play on</param>
        /// <param name="players">The players configured at the start of the game</param>
        public static void StartGame(Board board, List<Player> players)
        {
            // Cache current loggers and disable logging of logged game
            Loggers.loggers.ForEach(logger =>
            {
                _buffer.Add(logger);
            });
            Loggers.loggers.Clear();

            // Check if game board already exists
            if (gameStatus.boards.FirstOrDefault(b => b.name == board.name) is null)
            {
                // Create new game board, note it will not make pretty corner walls.
                // Board can be edited after creation to add corner walls.
                boardImageMaker newBoardMaker = new(board, [], false);
                newBoardMaker.createImage();
                newBoardMaker.Dispose();
                
                // Temporarily remove flags to make board JSON
                int[][] flagBuffer = board.flags;
                board.flags = [];

                // Convert to JSON
                string newBoard = JsonConvert.SerializeObject(board);

                // Write new JSON file.
                char _separator = Path.DirectorySeparatorChar;
                using StreamWriter sw = new(serviceHelpers.rootPath + _separator + "GameConfig" + _separator + "Boards" + _separator + board.name.Replace(" ", "") + ".json", false);               
                sw.Write(newBoard);
                sw.Close();
            }

            // Setup the game
            gameStatus.SetupGame(board, 12, false, false, board.flags);
            
            // Add each player to the game
            players.ForEach(player =>
            {
                AddPlayer(player);
                Thread.Sleep(250);
            });
            gameStatus.gameStarted = true;
        }

        /// <summary>
        /// Execute a series simulates a series of game events.
        /// Execution can be halted after the next event by setting the AbortReplay property in this class to true.
        /// </summary>
        /// <param name="events">The list of eventType and list of players pairs</param>
        public static void RunGame(List<(ILogger.eventTypes, List<Player>)> events)
        {
            AbortReplay = false;
            Thread.Sleep(3000);
            foreach (var _event in events) 
            {
                if (AbortReplay)
                    break;
                switch(_event.Item1)
                {
                    case ILogger.eventTypes.roundStart:
                        StartRound(_event.Item2);
                        Thread.Sleep(1000);
                        SpinWait.SpinUntil(() => !gameStatus.roundRunning);
                        break;
                    case ILogger.eventTypes.playerUpdate:
                        UpdatePlayer(_event.Item2[0]);
                        break;
                    case ILogger.eventTypes.playerAdded:
                        AddPlayer(_event.Item2[0]);
                        break;
                    case ILogger.eventTypes.playerEntering:
                        EnterPlayer(_event.Item2[0]);
                        break;
                    case ILogger.eventTypes.botDeath:
                        RobotDied(_event.Item2[0]);
                        break;
                    case ILogger.eventTypes.gameEnd:
                        GameEnded(_event.Item2);
                        break;                        
                }
                Thread.Sleep(250);
            };
        }

        /// <summary>
        /// Start a round of play
        /// </summary>
        /// <param name="players">The players' status at the start of the round</param>
        public static void StartRound(List<Player> players)
        {
            players.ForEach(player =>
            {
                serviceHelpers.signals.submitMove(gameStatus.players[player.playerNumber], player.move);
                Thread.Sleep(250);
            });
        }

        /// <summary>
        /// Handles a dead robot. Nothing to do here yet
        /// </summary>
        /// <param name="player"></param>
        public static void RobotDied(Player player)
        {
            return;
        }

        /// <summary>
        /// Handles the end of the game. Nothing to do here yet
        /// </summary>
        /// <param name="players">The final player states</param>
        public static void GameEnded(List<Player> players)
        {
            // Re-enable game logging
            _buffer.ForEach(logger =>
            {
                Loggers.loggers.Add(logger);
            });
            _buffer.Clear();
        }

        /// <summary>
        /// Adds a player to the game
        /// </summary>
        /// <param name="player">The player to add</param>
        /// <returns>False if a player could not be added</returns>
        public static bool AddPlayer(Player player) 
        {
            if (gameStatus.addPlayer() == 0)
            {
                AbortReplay = true;
                return false;
            }

            // See if the player's robot is available, if not, assign a random one. This assumes there are enough bots available
            string botName = player.playerRobot.robotName;
            if (gameStatus.robotPen.FirstOrDefault(r => r.robotName == botName) is null)
            {
                Random randomBot = new();
                botName = gameStatus.robotPen[randomBot.Next(gameStatus.robotPen.Count - 1)].robotName;
            }
            gameStatus.assignBot(player.playerNumber, botName);

            // Configure robot position
            Robot playerBot = gameStatus.players[player.playerNumber].playerRobot;
            playerBot.x_pos = player.playerRobot.x_pos;
            playerBot.y_pos = player.playerRobot.y_pos;
            playerBot.lastLocation = player.playerRobot.lastLocation;
            playerBot.currentDirection = player.playerRobot.currentDirection;
            return true;
        }

        /// <summary>
        /// Re-enter a player that has died
        /// </summary>
        /// <param name="player">The player to enter</param>
        public static void EnterPlayer(Player player)
        {
            serviceHelpers.signals.enterPlayer(gameStatus.players[player.playerNumber], [ player.playerRobot.x_pos, player.playerRobot.y_pos ], player.playerRobot.currentDirection);
            gameStatus.playersNeedEntering = false;
        }

        /// <summary>
        /// Updates the settings for a player
        /// </summary>
        /// <param name="player">the player to update</param>
        public static void UpdatePlayer(Player player)
        {
            serviceHelpers.signals.updatePlayer(gameStatus.players[player.playerNumber], player.lives, player.playerRobot.damage, player.playerRobot.x_pos, player.playerRobot.y_pos, (int)player.playerRobot.currentDirection, player.playerRobot.robotName, player.playerRobot.flags);
        }

        private static readonly List<ILogger> _buffer = [];
    }
}
