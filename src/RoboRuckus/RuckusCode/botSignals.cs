using RoboRuckus.BotCommunication;
using System.Net;
using System.Threading;
using RoboRuckus.RuckusCode.Movement;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace RoboRuckus.RuckusCode
{
    public static class botSignals
    {
        // Used for thread control
        private static object _locker = new object();
        private static int _port = 8080;

        /// <summary>
        /// Sends a movement command to a robot
        /// </summary>
        /// <param name="order">The order to send</param>
        /// <returns>The response from the bot</returns>
        public static string sendMoveCommand(orderModel order)
        {
            Robot bot = gameStatus.robots[order.botNumber];
            if (gameStatus.botless)
            {
                bot.moving.Set();
                return "OK";
            }
            // Check communication method used by bot
            switch (bot.mode)
            {
                case Robot.communicationModes.IP:
                    Dictionary<string, string> data = new() { { "move", ((int)order.move).ToString() }, {"magnitude", order.magnitude.ToString() } };
                    // Might be a better way to handle this, or just rewrite everything as async.
                    return Task.Run(() => BotIPSender.sendDataToRobot("http://" + bot.robotAddress.ToString() + "/move",  HttpMethod.Post, data)).GetAwaiter().GetResult();
                default:
                    throw new NotImplementedException("Communication mode not implemented yet.");
            }
        }

        /// <summary>
        /// Sends a damage value to a bot
        /// </summary>
        /// <param name="botNumber">The bot to send the value to</param>
        /// <param name="damage">The damage value</param>
        /// <returns>The response from the bot</returns>
        public static string sendDamage(int botNumber, sbyte damage)
        {
            Robot bot = gameStatus.robots[botNumber];
            if (gameStatus.botless)
            {
                bot.moving.Set();
                return "OK";
            }
            // Check communication method used by bot
            switch (bot.mode)
            {
                case Robot.communicationModes.IP:
                    Dictionary<string, string> data = new() { { "magnitude", damage.ToString() } };
                    // Might be a better way to handle this, or just rewrite everything as async.
                    return Task.Run(() => BotIPSender.sendDataToRobot("http://" + bot.robotAddress.ToString() + "/takeDamage", HttpMethod.Put, data)).GetAwaiter().GetResult();
                default:
                    throw new NotImplementedException("Communication mode not implemented yet.");
            }
        }

        /// <summary>
        /// Assigns a player number to a bot
        /// </summary>
        /// <param name="botNumber">The bot to assign the player to</param>
        /// <param name="playerNumber">The player to assign</param>
        /// <returns>True on a successful response (OK) from the bot</returns>
        public static bool sendPlayerAssignment(int botNumber, int playerNumber)
        {
            Robot bot = gameStatus.robots[botNumber];
            if (gameStatus.botless)
            {
                bot.moving.Set();
                return true;
            }
            // Check communication method used by bot
            switch (bot.mode)
            {
                case Robot.communicationModes.IP:
                    Dictionary<string, string> data = new() { { "player", playerNumber.ToString() }, { "botNumber", botNumber.ToString() } };
                    // Might be a better way to handle this, or just rewrite everything as async.
                    return Task.Run(() => BotIPSender.sendDataToRobot("http://" + bot.robotAddress.ToString() + "/assignPlayer", HttpMethod.Put, data)).GetAwaiter().GetResult() == "OK";
                default:
                    throw new NotImplementedException("Communication mode not implemented yet.");
            }
        }

        /// <summary>
        /// Sends a reset order to a bot
        /// </summary>
        /// <param name="botNumber">The bot to reset</param>
        /// <returns>True on a successful response (OK) from the bot</returns>
        public static bool sendReset(int botNumber)
        {
            Robot bot = gameStatus.robots[botNumber];
            if (gameStatus.botless)
            {
                bot.moving.Set();
                return true;
            }
            // Check communication method used by bot
            switch (bot.mode)
            {
                case Robot.communicationModes.IP:
                    // Might be a better way to handle this, or just rewrite everything as async.
                    return Task.Run(() => BotIPSender.sendDataToRobot("http://" + bot.robotAddress.ToString() + "/reset", HttpMethod.Put)).GetAwaiter().GetResult() == "OK";
                default:
                    throw new NotImplementedException("Communication mode not implemented yet.");
            }
        }

        /// <summary>
        /// Updates configuration parameters to a robot in setup mode
        /// also sends movement test, quit, and other commands.
        /// </summary>
        /// <param name="botNumber">The robot to send the parameter to</param>
        /// <param name="option">The tuning mode option to use</param>
        /// <param name="parameters">A JSON string of the parameters to update</param>
        /// <returns>True on a successful response (OK) from the bot</returns>
        public static bool sendSetupInstruction(int botNumber, int option, string parameters)
        {
            Robot bot = gameStatus.robots[botNumber];
            if (gameStatus.botless)
            {
                bot.moving.Set();
                return true;
            }
            // Check communication method used by bot
            switch (bot.mode)
            {
                case Robot.communicationModes.IP:
                    Dictionary<string, string> data = new() { { "option", option.ToString() }, { "parameters", parameters } };
                    // Might be a better way to handle this, or just rewrite everything as async.
                    return Task.Run(() => BotIPSender.sendDataToRobot("http://" + bot.robotAddress.ToString() + "/setupInstruction", HttpMethod.Post, data)).GetAwaiter().GetResult() == "OK";
                default:
                    throw new NotImplementedException("Communication mode not implemented yet.");
            }
        }

        // <summary>
        /// Gets the current configuration settings from a robot in setup mode
        /// </summary>
        /// <param name="botNumber">The robot to get the settings from</param>
        /// <returns>A JSON separated list of values</returns>
        public static string getRobotSettings(int botNumber)
        {
            Robot bot = gameStatus.robots[botNumber];
            if (gameStatus.botless)
            {
                bot.moving.Set();
                return "";
            }
            // Check communication method used by bot
            switch (bot.mode)
            {
                case Robot.communicationModes.IP:
                    // Might be a better way to handle this, or just rewrite everything as async.
                    return Task.Run(() => BotIPSender.sendDataToRobot("http://" +  bot.robotAddress.ToString() + "/getSettings", HttpMethod.Get)).GetAwaiter().GetResult();
                default:
                    throw new NotImplementedException("Communication mode not implemented yet.");
            }
        }


        /// <summary>
        /// Adds a bot using IP
        /// </summary>
        /// <param name="ip">The IP address of the robot</param>
        /// <param name="name">The robot's name</param>
        /// <returns>True acknowledging the accepted robot</returns>
        public static bool addBot(IPAddress ip, string name)
        {
            // Lock used so player assignment is sent after this method exits
            lock (_locker)
            {
                int result = gameStatus.addBot(ip, name);
                // Check if bot is already in pen
                if (result != -1 && !gameStatus.tuneRobots)
                {
                    // Check if bot already has player assigned
                    if ((result & 0x10000) != 0)
                    {
                        // Get assigned player number
                        int player = (result & 0xffff) >> 8;
                        // Get assigned bot number
                        result &= 255;
                        // Set thread to assign player to bot
                        new Thread(() => alreadyAssigned(player + 1, result)).Start();
                    }
                }
                // Send confirmation
                return true;
            }
        }

        /// <summary>
        /// Adds a bot using Bluetooth
        /// </summary>
        /// <param name="BTAddress">The IP address of the robot</param>
        /// <param name="name">The robot's name</param>
        /// <returns>True acknowledging the accepted robot</returns>
        public static bool addBot(string BTAddress, string name)
        {
            // Lock used so player assignment is sent after this method exits
            lock (_locker)
            {
                int result = gameStatus.addBot(BTAddress, name);
                // Check if bot is already in pen
                if (result != -1 && !gameStatus.tuneRobots)
                {
                    // Check if bot already has player assigned
                    if ((result & 0x10000) != 0)
                    {
                        // Get assigned player number
                        int player = (result & 0xffff) >> 8;
                        // Get assigned bot number
                        result &= 255;
                        // Set thread to assign player to bot
                        new Thread(() => alreadyAssigned(player + 1, result)).Start();
                    }
                }
                // Send confirmation
                return true;
            }
        }

        /// <summary>
        /// Signals a bot has completed its move
        /// </summary>
        /// <param name="bot">The bot number</param>
        public static void Done(int bot)
        {
            // Make sure the bot is in the game
            if (gameStatus.robots.Count > bot)
            {
                // Signal bot has finished moving.
                gameStatus.robots[bot].moving.Set();
            }
        }

        /// <summary>
        /// Sends a player assignment to a bot which
        /// has already had a player assigned previously
        /// </summary>
        /// <param name="player">The player assigned</param>
        /// <param name="bot">The bot the player is assigned to</param>
        private static void alreadyAssigned(int player, int bot)
        {
            lock (_locker)
            {
                // Wait for the bot server to become ready
                Thread.Sleep(500);
                SpinWait.SpinUntil(() => sendPlayerAssignment(bot, player), 1000);
            }
        }


        /// <summary>
        /// Not implemented yet
        /// Sends data to a robot via Bluetooth
        /// </summary>
        /// <param name="bot">The robot to send the data to</param>
        /// <param name="data">The data to send</param>
        /// <returns>The response from the robot or an empty string on failure</returns>
        private static string sendDataToRobotBT(Robot bot, string data)
        {
            return "";
        }
    }
}