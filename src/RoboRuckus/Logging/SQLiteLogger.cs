using Microsoft.Data.Sqlite;
using RoboRuckus.RuckusCode;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System.Net;
using System;

namespace RoboRuckus.Logging
{
    public class SQLiteLogger : ILogger
    {
        /// <summary>
        /// Connects to the SQLite database or creates it if necessary
        /// </summary>
        public SQLiteLogger()
        {
            _connectionString = new()
            {
                DataSource = serviceHelpers.rootPath + Path.DirectorySeparatorChar + "GameConfig" + Path.DirectorySeparatorChar + "GameLog.db"
            };

            // Check if the database already exists
            if (File.Exists(_connectionString.DataSource))
            {
                // Check if the database file is larger than 100 MB and perform log rotation if it is
                if (new FileInfo(_connectionString.DataSource).Length > 100000000)
                {
                    File.Delete(_connectionString.DataSource + ".old");
                    File.Move(_connectionString.DataSource, _connectionString.DataSource + ".old");
                }
            }
            // Check if database is empty
            List<string> tables = new();
            using (SqliteConnection connection = new(_connectionString.ToString()))
            {
                connection.Open();
                SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    @"
                        SELECT name
                        FROM sqlite_master
                    ";
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    tables.Add(reader.GetString(0));
                }
            }
            if (tables.Count == 0)
            {
                // Create database structure
                createDatabse();
            }
        }

        public void LogGameStart(Board board, List<Player> players)
        {
            using SqliteConnection connection = new(_connectionString.ToString());
            connection.Open();

            // Use transaction so that changes will only be committed if both commands succeed
            using SqliteTransaction transaction = connection.BeginTransaction();           
            transaction.Save("before-addition");

            SqliteCommand addRow = connection.CreateCommand();
            addRow.CommandText =
            @"
                INSERT INTO LoggedGames (board, players, timestamp)
                VALUES($board, $players, $timestamp);

                SELECT last_insert_rowid();
            ";
            JsonSerializerSettings settings = new();
            settings.Converters.Add(new IPAddressConverter());

            addRow.Parameters.AddWithValue("$board", JsonConvert.SerializeObject(board));
            addRow.Parameters.AddWithValue("$players", JsonConvert.SerializeObject(players, settings));
            addRow.Parameters.AddWithValue("$timestamp", DateTime.UtcNow);
            _currentgameID = (long)addRow.ExecuteScalar();

            SqliteCommand addTable = connection.CreateCommand();
            addTable.CommandText =
            @"
                CREATE TABLE Game_" + _currentgameID.ToString() + @" (
                    event   INTEGER,
                    data    TEXT
                );
            ";
            addTable.ExecuteNonQuery();
            transaction.Commit();
            
            
        }

        public void LogRoundStart(List<Player> players)
        {
            JsonSerializerSettings settings = new();
            settings.Converters.Add(new IPAddressConverter());
            addEvent(ILogger.eventTypes.roundStart, JsonConvert.SerializeObject(players, settings));
        }

        public void LogPlayerAdded(Player player) 
        {
            JsonSerializerSettings settings = new();
            settings.Converters.Add(new IPAddressConverter());
            addEvent(ILogger.eventTypes.playerAdded, JsonConvert.SerializeObject(new List<Player> { player }, settings));
        }

        public void LogBotDeath(Player player)
        {
            JsonSerializerSettings settings = new();
            settings.Converters.Add(new IPAddressConverter());
            addEvent(ILogger.eventTypes.botDeath, JsonConvert.SerializeObject(new List<Player> { player }, settings));
        }

        public void LogPlayerEntering(Player player)
        {
            JsonSerializerSettings settings = new();
            settings.Converters.Add(new IPAddressConverter());
            addEvent(ILogger.eventTypes.playerEntering, JsonConvert.SerializeObject(new List<Player> { player }, settings));
        }

        public void LogGameEnd(List<Player> players)
        {
            JsonSerializerSettings settings = new();
            settings.Converters.Add(new IPAddressConverter());
            addEvent(ILogger.eventTypes.gameEnd, JsonConvert.SerializeObject(players, settings));
        }

        public void LogPlayerUpdate(Player player) 
        {
            JsonSerializerSettings settings = new();
            settings.Converters.Add(new IPAddressConverter());
            addEvent(ILogger.eventTypes.playerUpdate, JsonConvert.SerializeObject(new List<Player> { player }, settings));
        }

        /// <summary>
        /// Retrieves a collection of the logged games by date
        /// </summary>
        /// <returns>The dictionary of dates of logged games linked to their rowid in the database</returns>
        public Dictionary<long, DateTime> GetLoggedGames()
        {
            Dictionary<long, DateTime> loggedGames = [];
            using SqliteConnection connection = new(_connectionString.ToString());            
            connection.Open();
            SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                @"
                    SELECT rowid, timestamp
                    FROM LoggedGames
                ";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                loggedGames.Add(reader.GetInt64(0), DateTime.Parse(reader.GetString(1)));
            }
            return loggedGames;
        }

        /// <summary>
        /// Gets the info to setup a game for replay
        /// </summary>
        /// <param name="gameId">The rowid of the logged game to get</param>
        /// <returns>The board used and a list of the initial player states</returns>
        public (Board board, List<Player> players) GetGameSetup(int gameId)
        {
            List<Player> players = [];
            Board board = null;
            using SqliteConnection connection = new(_connectionString.ToString());
            connection.Open();
            SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                @"
                    SELECT board, players
                    FROM LoggedGames WHERE rowid=" + gameId.ToString() + @"
                ";
            using SqliteDataReader reader = command.ExecuteReader();
            JsonSerializerSettings settings = new();
            settings.Converters.Add(new IPAddressConverter());
            while (reader.Read())
            {
                board = JsonConvert.DeserializeObject<Board>(reader.GetString(0));
                players = JsonConvert.DeserializeObject<List<Player>>(reader.GetString(1), settings);
            }
            return (board, players);
        }

        /// <summary>
        /// Retrieves all the logged events for a game
        /// </summary>
        /// <param name="gameID">The rowid of the logged game to get</param>
        /// <returns>A dictionary of paired event types, and a list of player snapshots for that event</returns>
        public List<(long, ILogger.eventTypes, List<Player>)> GetEvents(int gameID)
        {
            List<(long, ILogger.eventTypes, List<Player>)> events = [];
            using SqliteConnection connection = new(_connectionString.ToString());
            connection.Open();
            SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                @"
                    SELECT rowid, event, data
                    FROM Game_" + gameID.ToString() + @"
                ";
            using SqliteDataReader reader = command.ExecuteReader();
            JsonSerializerSettings settings = new();
            settings.Converters.Add(new IPAddressConverter());
            while (reader.Read())
            {
                events.Add((reader.GetInt64(0), (ILogger.eventTypes)reader.GetInt32(1), JsonConvert.DeserializeObject<List<Player>>(reader.GetString(2),settings)));
            }
            return events;
        }

        /// <summary>
        /// Stores the string to connect to the database.
        /// </summary>
        private SqliteConnectionStringBuilder _connectionString;

        /// <summary>
        /// The ID of the current game being logged or replayed
        /// </summary>
        private long _currentgameID;

        /// <summary>
        /// Validate a string to protect against SQL injection. This is an inelegant method that simply rejects any special characters in a string
        /// </summary>
        /// <param name="untrusted">the string to validate.</param>
        /// <returns>True if the string doesn't contain any forbidden characters.</returns>
        private static bool validateString(string untrusted)
        {
            if (untrusted.IndexOfAny([ ' ', '\'', '\\', '"', '@', '*', '$', '(', ')', '\r', '\n', '\t', '\b', '\0', ',', ';', '`' ]) != -1)
                return false;
            return true;
        }

        /// <summary>
        /// Populate a new or empty database with the initial structure.
        /// </summary>
        private void createDatabse()
        {
            using SqliteConnection connection = new(_connectionString.ToString());            
            connection.Open();
            SqliteCommand command = connection.CreateCommand();
            command.CommandText =
            @"
                CREATE TABLE LoggedGames (
                    board           TEXT,
                    players         TEXT,
                    timestamp TEXT  UNIQUE
                );
            ";
            command.ExecuteNonQuery();            
        }

        /// <summary>
        /// Adds an event to the log
        /// </summary>
        /// <param name="eventType">The event type to add</param>
        /// <param name="data">The data for the event</param>
        /// <returns>True on success</returns>
        private bool addEvent(ILogger.eventTypes eventType, string data)
        {
            using SqliteConnection connection = new(_connectionString.ToString());            
            connection.Open();
            // Add data to the game's table
            SqliteCommand addRow = connection.CreateCommand();
            addRow.CommandText =
                @"
                    INSERT INTO Game_" + _currentgameID.ToString() + @" (event, data)
                    VALUES($event, $data)                   
                ";
            addRow.Parameters.AddWithValue("$event", (int)eventType);
            addRow.Parameters.AddWithValue("$data", data);
            if (addRow.ExecuteNonQuery() > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Helper class for handling IP address (de)serialization.
        /// </summary>
        class IPAddressConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(IPAddress);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteValue(value.ToString());
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return IPAddress.Parse((string)reader.Value);
            }
        }

    }
}