using Microsoft.Data.Sqlite;
using RoboRuckus.RuckusCode;
using RoboRuckus.RuckusCode.Movement;
using System.Collections.Generic;

namespace RoboRuckus.Logging
{
    public class SQLiteLogger : ILogger 
    {
        public void LogBotDeath(Robot bot)
        {
            throw new System.NotImplementedException();
        }

        public void LogBotOrder(orderModel botOrder)
        {
            throw new System.NotImplementedException();
        }
        public void LogGameEnd(ILogger.GameEndReason reason, List<Player> players)
        {
            throw new System.NotImplementedException();
        }

        public void LogGameStart(Board board, List<Player> players)
        {
            throw new System.NotImplementedException();
        }

        public void LogGameStatus(List<Player> players)
        {
            throw new System.NotImplementedException();
        }

        public void LogBotMovement(moveModel botMovement) 
        {
            throw new System.NotImplementedException();
        }
    }
}