using System;
using System.Collections.Generic;
using RoboRuckus.RuckusCode;
using RoboRuckus.RuckusCode.Movement;

namespace RoboRuckus.Logging
{
    /// <summary>
    /// Game logger interface
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// The reason a game ended
        /// </summary>
        enum GameEndReason 
        {
            /// <summary>
            /// Game ended when a player won
            /// </summary>
            Winner,

            /// <summary>
            /// Game ended with the reset button being pressed
            /// </summary>
            Reset        
        }

        /// <summary>
        /// Log the start of a new game
        /// </summary>
        /// <param name="board">The board being played on</param>
        /// <param name="players">The players in the game</param>
        void LogGameStart(Board board, List<Player> players);

        /// <summary>
        /// Log the end of a game
        /// </summary>
        /// <param name="players">The players in the game</param>
        void LogGameEnd(GameEndReason reason, List<Player> players);

        /// <summary>
        /// Log a card played to move a bot
        /// </summary>
        /// <param name="botMovement">The bot movement to log</param>
        void LogBotMovement(moveModel botMovement);

        /// <summary>
        /// Log an order to move a bot
        /// </summary>
        /// <param name="botOrder">The bot order to log</param>
        void LogBotOrder(orderModel botOrder);

        /// <summary>
        /// Log the dead of a robot
        /// </summary>
        /// <param name="bot">The robot that died</param>
        void LogBotDeath(Robot bot);

        /// <summary>
        /// Log the current status of the game
        /// </summary>
        /// <param name="players">The list of players in the game</param>
        void LogGameStatus(List<Player> players);
    }
}