using Microsoft.Data.Sqlite;


namespace RoboRuckus.Logging
{
    /// <summary>
    /// Logs bot movements to a SQLite database
    /// </summary>
    public class SQLiteLogger : ILogger
    {
        private SQLiteConnection _connection;

        /// <summary>
        /// Creates a new instance of the SQLiteLogger  
        /// </summary>
        /// <param name="databasePath">The path to the SQLite database</param>
        public SQLiteLogger(string databasePath)
        {
            _connection = new SQLiteConnection(databasePath);
            _connection.CreateTable<BotMovement>();
        }

        /// <summary>
        /// Logs the bot movement
        /// </summary>
        /// <param name="botMovement">The bot movement to log</param>
        public void LogBotMovement(BotMovement botMovement)
        {
            _connection.Insert(botMovement);
        }
    }
}