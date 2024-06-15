using System;
using System.Collections.Generic;
using RoboRuckus.RuckusCode;
using static RoboRuckus.Logging.SQLiteLogger;

namespace RoboRuckus.Logging
{
    /// <summary>
    /// Game logger interface
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// The types of events that are logged
        /// </summary>
        public enum eventTypes
        {
            gameEnd,
            roundStart,
            botDeath,
            playerEntering,
            playerUpdate
        }

        /// <summary>
        /// Log the start of a new game
        /// </summary>
        /// <param name="board">The board being played on</param>
        /// <param name="players">The players in the game</param>
        public void LogGameStart(Board board, List<Player> players);

        /// <summary>
        /// Log the end of a game
        /// </summary>
        /// <param name="players">The players in the game at their end state</param>
        public void LogGameEnd(List<Player> players);

        /// <summary>
        /// Log the start of a round
        /// </summary>
        /// <param name="players">The list of players in the game</param>
        public void LogRoundStart(List<Player> players);

        /// <summary>
        /// Log the death of a robot
        /// </summary>
        /// <param name="bot">The player whose bot died</param>
        public void LogBotDeath(Player player);

        /// <summary>
        /// Log a player re-entering the game from death or otherwise
        /// </summary>
        /// <param name="player">The player the was re-entered and their updated settings</param>
        public void LogPlayerEntering(Player player);

        /// <summary>
        /// Log that a player's status was manually updated by the game master
        /// </summary>
        /// <param name="player">The updated player</param>
        public void LogPlayerUpdate(Player player);

        /// <summary>
        /// Retrieves a collection of the logged games by date
        /// </summary>
        /// <returns>The dictionary of dates of logged games linked to their game ID value in the log</returns>
        public Dictionary<DateTime,long> GetLoggedGames();

        /// <summary>
        /// Gets the info to setup a game for replay
        /// </summary>
        /// <param name="gameId">The game ID of the logged game to get</param>
        /// <returns>The board used and a list of the initial player states</returns>
        public (Board boad, List<Player> players) GetGameSetup(int gameId);

        /// <summary>
        /// Retrieves all the logged events for a game
        /// </summary>
        /// <param name="gameID">The game ID of the logged game to get</param>
        /// <returns>A dictionary of paired event types, and a list of player snapshots for that event</returns>
        public List<(eventTypes, List<Player>)> getEvents(int gameID);

    }
}