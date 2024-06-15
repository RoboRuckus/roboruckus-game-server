using RoboRuckus.RuckusCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RoboRuckus.Logging
{
    public static class Replay
    {

        /// <summary>
        /// Set to true to abort any currently running replay simulation after the next event completes
        /// </summary>
        public static bool abortReplay = false;

        /// <summary>
        /// Start a replay game
        /// </summary>
        /// <param name="board">The board to play on</param>
        /// <param name="players">The players configured at the start of the game</param>
        public static void startGame(Board board, List<Player> players)
        {
            // Disable logging of logged game
            Loggers.loggers.ForEach(logger =>
            {
                _buffer.Add(logger);
            });
            Loggers.loggers.Clear();
            // setup the game
            gameStatus.setupGame(board, players.Count, false, false, board.flags);
            
            // Add each player to the game
            players.ForEach(player =>
            {
                gameStatus.addPlayer();

                // See if the player's robot is available, if not, assign a random one. This assumes there are enough bots available
                string botName = player.playerRobot.robotName;
                if (gameStatus.robotPen.FirstOrDefault(r => r.robotName == botName) is null)
                {
                    Random randomBot = new Random();
                    botName = gameStatus.robotPen[randomBot.Next(gameStatus.robotPen.Count - 1)].robotName;
                }
                gameStatus.assignBot(player.playerNumber, botName);

                // Configure robot position
                Robot playerBot = gameStatus.players[player.playerNumber].playerRobot;
                playerBot.x_pos = player.playerRobot.x_pos;
                playerBot.y_pos = player.playerRobot.y_pos;
                playerBot.lastLocation = player.playerRobot.lastLocation;
                playerBot.currentDirection = player.playerRobot.currentDirection;
                Thread.Sleep(250);
            });
            gameStatus.gameStarted = true;
        }

        /// <summary>
        /// Runs a loop that simulates a series of game events
        /// </summary>
        /// <param name="events">The list of eventType and list of players pairs</param>
        public static void runGame(List<(ILogger.eventTypes, List<Player>)> events)
        {
            abortReplay = false;
            Thread.Sleep(3000);
            foreach (var _event in events) 
            {
                if (abortReplay)
                    break;
                switch(_event.Item1)
                {
                    case ILogger.eventTypes.roundStart:
                        startRound(_event.Item2);
                        Thread.Sleep(1000);
                        SpinWait.SpinUntil(() => !gameStatus.roundRunning);
                        break;
                    case ILogger.eventTypes.playerUpdate:
                        updatePlayer(_event.Item2[0]);
                        break;
                    case ILogger.eventTypes.playerEntering:
                        enterPlayer(_event.Item2[0]);
                        break;
                    case ILogger.eventTypes.botDeath:
                        robotDied(_event.Item2[0]);
                        break;
                    case ILogger.eventTypes.gameEnd:
                        gameEnded(_event.Item2);
                        break;                        
                }
                Thread.Sleep(250);
            });
        }

        /// <summary>
        /// Start a round of play
        /// </summary>
        /// <param name="players">The players' status at the start of the round</param>
        public static void startRound(List<Player> players)
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
        public static void robotDied(Player player)
        {
            return;
        }

        /// <summary>
        /// Handles the end of the game. Nothing to do here yet
        /// </summary>
        /// <param name="players"></param>
        public static void gameEnded(List<Player> players)
        {
            // Re-enable game logging
            _buffer.ForEach(logger =>
            {
                Loggers.loggers.Add(logger);
            });
            _buffer.Clear();
        }

        /// <summary>
        /// Re-enter a player that has died
        /// </summary>
        /// <param name="player">the player to enter</param>
        public static void enterPlayer(Player player)
        {
            serviceHelpers.signals.enterPlayer(gameStatus.players[player.playerNumber], [ player.playerRobot.x_pos, player.playerRobot.y_pos ], player.playerRobot.currentDirection);
        }

        /// <summary>
        /// Updates the settings for a player
        /// </summary>
        /// <param name="player">the player to update</param>
        public static void updatePlayer(Player player)
        {
            serviceHelpers.signals.updatePlayer(gameStatus.players[player.playerNumber], player.lives, player.playerRobot.damage, player.playerRobot.x_pos, player.playerRobot.y_pos, (int)player.playerRobot.currentDirection, player.playerRobot.robotName, player.playerRobot.flags);
        }

        private static List<ILogger> _buffer = [];
    }
}
