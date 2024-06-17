using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using RoboRuckus.RuckusCode;
using System.Collections.Generic;
using System.Linq;
using RoboRuckus.Models;
using RoboRuckus.Logging;
using Newtonsoft.Json;
using System.Threading;
using System.IO;


namespace RoboRuckus.Controllers
{
    public class SetupController : Controller
    {
        /// <summary>
        /// Allows a user to set up and initialize the game
        /// </summary>
        /// <returns>The setup index view</returns>
        public IActionResult Index()
        {
            if (gameStatus.gameReady)
            {
                return RedirectToAction("Monitor");
            }
            else if (gameStatus.tuneRobots)
            {
                return RedirectToAction("Tuning");
            }
            // Get a list of currently loaded boards and add it to the model
            IEnumerable<SelectListItem> _boards = gameStatus.boards.OrderBy(b => b.name).Select(b => new SelectListItem
            {
                Text = b.name,
                Value = b.name
            });
            setupViewModel _model = new() { boards = _boards };
            // Send the board sizes to the view as a JSON object
            string _sizes = "{";
            bool first = true;
            foreach (Board gameBorad in gameStatus.boards)
            {
                if (!first)
                {
                    _sizes += ",";
                }
                first = false;
                _sizes += "\"" + gameBorad.name + "\": [" + gameBorad.size[0].ToString() + ", " + gameBorad.size[1].ToString() + "]";
            }
            _sizes += "}";
            ViewBag.sizes = _sizes;
            return View(_model);
        }

        /// <summary>
        /// Loads the game replay interface
        /// </summary>
        /// <returns>The view</returns>
        [HttpGet]
        public IActionResult Replay()
        {
            return View();
        }

        /// <summary>
        /// Starts a replay game, for testing
        /// </summary>
        /// <param name="gameID">The game ID to replay</param>
        /// <param name="logger">The logger to use</param>
        /// <param name="startRound">The round number to start on</param>
        /// <returns>Redirects to the monitor</returns>    
        [HttpGet]
        public IActionResult startReplay(int gameID, int logger, int startRound = 1)
        {
            if (gameID <= 0)
            {
                var loggedGames = Loggers.loggers[logger].GetLoggedGames();
                return Content(JsonConvert.SerializeObject(loggedGames), "text/json");
                // Add interface for selecting game to replay
            }
            else if (startRound <= 0) 
            {
                List<long> rounds = [];
                var gameEvents = Loggers.loggers[logger].GetEvents(gameID);
                gameEvents.ForEach(gameEvent => 
                {
                    if (gameEvent.Item2 == ILogger.eventTypes.roundStart) 
                    {
                        rounds.Add(gameEvent.Item1);
                    }
                });          
                return Content(JsonConvert.SerializeObject(rounds), "text/json");
            }
            else
            {
                var gameSetup = Loggers.loggers[logger].GetGameSetup(gameID);
                var gameEvents = Loggers.loggers[logger].GetEvents(gameID);
                if (startRound > 1) {
                    gameEvents.RemoveRange(0, startRound - 1);
                    gameSetup.players = gameEvents[0].Item3;
                }
                GameReplay.StartGame(gameSetup.board, gameSetup.players);
                Thread simulation = new(() => GameReplay.RunGame(gameEvents));
                simulation.Start();

                return RedirectToAction("Monitor");
            }
        }

        /// <summary>
        /// Sets up a game
        /// </summary>
        /// <param name="gameData">The game data needed for setup</param>
        /// <returns>Redirects to the appropriate action</returns>
        [HttpPost]
        public IActionResult setupGame(setupViewModel gameData)
        {
            if (gameData.numberOfPlayers > 0)
            {
                Board _board = gameStatus.boards.FirstOrDefault(b => b.name == gameData.selBoard);
                if (_board != null)
                {
                    int[][] flags = [];
                    gameStatus.gameBoard = _board;
                    if (gameData.flags != null && gameData.flags.Length > 1)
                    {
                        // Assign the flag coordinates, ordered according to the flag number
                        flags = JsonConvert.DeserializeObject<int[][]>(gameData.flags).OrderBy(f => f[0]).Select(f => new int[] { f[1], f[2] }).ToArray();
                    }
                    gameStatus.SetupGame(_board, gameData.numberOfPlayers, gameData.showRegistersEnabled, gameData.edgeControlEnabled, flags);
                }
                return RedirectToAction("Monitor");
            }
            else
            {
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Starts the game and has the first hand of cards dealt to players.
        /// </summary>
        /// <param name="status">Status code for start command (not yet used)</param>
        /// <returns>The string "Done"</returns>
        [HttpGet]
        public IActionResult startGame(int status)
        {
            gameStatus.gameStarted = true;
            serviceHelpers.signals.dealPlayers();
            Loggers.loggers.ForEach((Logger) => Logger.LogGameStart(gameStatus.gameBoard, gameStatus.players));
            return Content("Done", "text/plain");
        }

        /// <summary>
        /// Monitors the game board status
        /// </summary>
        /// <returns>The view</returns>
        [HttpGet]
        public IActionResult Monitor()
        {
            if (gameStatus.gameBoard != null)
            {
                ViewBag.flags = JsonConvert.SerializeObject(gameStatus.gameBoard.flags);
                ViewBag.players = gameStatus.numPlayers;
                ViewBag.board_x = gameStatus.boardSizeX;
                ViewBag.board_y = gameStatus.boardSizeY;
                ViewBag.board = gameStatus.gameBoard.name.Replace(" ", "");
                ViewBag.timer = gameStatus.playerTimer;
                ViewBag.started = gameStatus.gameStarted;
                return View();
            }
            if (gameStatus.tuneRobots)
            {
                return RedirectToAction("Tuning");
            }
            else
            {
                return RedirectToAction("Index");
            }

        }

        /// <summary>
        /// Re-enters dead players in the game the game
        /// </summary>
        /// <param name="players">[[player number, bot X position, bot Y position, bot orientation],etc...]</param>
        /// <returns>The string "OK"</returns>
        [HttpPost]
        public IActionResult enterPlayers(string players)
        {
            lock(gameStatus.locker)
            {
                lock (gameStatus.setupLocker)
                {
                    int[][] entering = JsonConvert.DeserializeObject<int[][]>(players);
                    foreach (int[] enter in entering)
                    {
                        Player sender = gameStatus.players[enter[0]];
                        int[] location = [ enter[1], enter[2] ];
                        Robot.orientation facing = (Robot.orientation)Enum.Parse(typeof(Robot.orientation), enter[3].ToString());
                        serviceHelpers.signals.enterPlayer(sender, location, facing);
                    }
                    serviceHelpers.signals.dealPlayers();
                    gameStatus.playersNeedEntering = false;
                }
                return Content("OK", "text/plain");
            }
        }

        /// <summary>
        /// Allows the game master to manage players
        /// </summary>
        /// <param name="player">The player number to manage</param>
        /// <returns>The view</returns>
        public IActionResult Manage(int player = 0)
        {
            lock (gameStatus.locker)
            {
                lock (gameStatus.locker)
                {
                    if (player != 0 && player <= gameStatus.players.Count)
                    {
                        Player caller = gameStatus.players[player - 1];

                        ViewBag.board_x = gameStatus.boardSizeX;
                        ViewBag.board_y = gameStatus.boardSizeY;
                        ViewBag.player = player;
                        ViewBag.dir = (int)caller.playerRobot.currentDirection;
                        ViewBag.bots = JsonConvert.SerializeObject(gameStatus.robotPen.Select(r => r.robotName).ToArray()).Replace("\"", "&quot;");
                        return View(new manageViewModel(caller));
                    }
                    return Content("Error", "text/plain");
                }
            }
        }
        /// <summary>
        /// Updates the status of a player
        /// </summary>
        /// <param name="lives">The number of lives the player has</param>
        /// <param name="damage">The damage the robot has</param>
        /// <param name="botX">The robot's x position</param>
        /// <param name="botY">The robot's y position</param>
        /// <param name="botDir">The robots orientation</param>
        /// <param name="botName">The robot the player is using</param>
        /// <param name="flags">The flags they've touched</param>
        /// <param name="player">The player being updated</param>
        /// <returns>The view</returns>
        public IActionResult updatePlayer(int lives, sbyte damage, int botX, int botY, int botDir, string botName, int flags, int player = 0)
        {
            lock (gameStatus.locker)
            {
                if (player != 0 && player <= gameStatus.players.Count)
                {
                    lock (gameStatus.setupLocker)
                    {
                        Player caller = gameStatus.players[player - 1];
                        serviceHelpers.signals.updatePlayer(caller, lives, damage, botX, botY, botDir, botName, flags);
                    }
                    serviceHelpers.signals.updateHealth();
                    return View();
                }
            }
            return Content("<h1>Error</h1>", "text/HTML");
        }

        /// <summary>
        /// Deals a player a new hand of cards
        /// </summary>
        /// <param name="player">The player to deal</param>
        /// <returns>The string OK</returns>
        [HttpPost]
        public IActionResult redealPlayer(int player)
        {
            if (gameStatus.gameStarted)
            {
                Player _player = gameStatus.players[player - 1];
                foreach (byte card in _player.cards)
                {
                    gameStatus.dealtCards.Remove(card);
                }
                _player.cards = null;
                serviceHelpers.signals.dealPlayers(player);
            }
            return Content("OK", "text/plain");
        }

        /// <summary>
        /// Used to create new game boards
        /// </summary>
        /// <returns>The view</returns>
        public IActionResult boardMaker()
        {
            if (gameStatus.gameReady)
            {
                return RedirectToAction("Monitor");
            }
            else if (gameStatus.tuneRobots)
            {
                return RedirectToAction("Tuning");
            }

            // Get a list of currently loaded boards and add it to the model.
            IEnumerable<SelectListItem> _boards = gameStatus.boards.OrderBy(b => b.name).Select(b => new SelectListItem
            {
                Text = b.name,
                Value = b.name
            });
            boardMakerViewModel _model = new() { boards = _boards };
            return View(_model);
        }

        /// <summary>
        /// Creates a new board or overwrites an existing one
        /// </summary>
        /// <param name="newBoard">The board maker view model containing the board data</param>
        /// <returns>A redirect to the board maker</returns>
        [HttpPost]
        public IActionResult makeBoard(boardMakerViewModel newBoard)
        {
            if (newBoard.boardData == null)
            {
                return BadRequest();
            }
            Board board = JsonConvert.DeserializeObject<Board>(newBoard.boardData);
            int[][] corners = JsonConvert.DeserializeObject<int[][]>(newBoard.cornerData);
            // Ensure only one instance of the board image maker is running at a time since it uses a lot of RAM
            lock (gameStatus.locker) 
            {
                // Create the images for the board
                using boardImageMaker maker = new(board, corners, false);
                maker.createImage();
                
            }
            Board oldBoard = gameStatus.boards.FirstOrDefault(x => x.name == newBoard.name);
            if (oldBoard != null)
            {
                // Remove old board from memory
                gameStatus.boards.Remove(oldBoard);
            }
            char _separator = Path.DirectorySeparatorChar;
            // Write new JSON file.
            using (StreamWriter sw = new(serviceHelpers.rootPath + _separator + "GameConfig" + _separator + "Boards" + _separator + newBoard.name.Replace(" ", "") + ".json", false))
            {
                sw.Write(newBoard.boardData);
                sw.Close();
            }
            // Add new board to memory
            gameStatus.boards.Add(board);
            return RedirectToAction("boardMaker");
        }

        /// <summary>
        /// Creates a new printable board or overwrites an existing one
        /// </summary>
        /// <param name="newBoard">The board maker view model containing the board data</param>
        /// <returns>A redirect to the board maker</returns>
        [HttpPost]
        public IActionResult makePrintableBoard([FromBody]boardMakerViewModel newBoard)
        {
            if (newBoard.boardData == null)
            {
                return BadRequest();
            }
            Board board = JsonConvert.DeserializeObject<Board>(newBoard.boardData);
            int[][] corners = JsonConvert.DeserializeObject<int[][]>(newBoard.cornerData);
            // Ensure only one instance of the board image maker is running at a time since it uses a lot of RAM
            lock (gameStatus.locker)
            {
                // Create the images for the board
                using boardImageMaker maker = new(board, corners, true);
                maker.createImage();
            }
            return Content("OK", "text/plain");
        }

        /// <summary>
        /// Gets the JSON encoding of a board in memory
        /// </summary>
        /// <param name="name">The name of the board to retrieve</param>
        /// <returns>The JSON encoding of the board</returns>
        [HttpGet]
        public IActionResult getBoard(string name)
        {
            Board board = gameStatus.boards.FirstOrDefault(x => x.name == name);
            return Content(JsonConvert.SerializeObject(board), "text/json");
        }

        /// <summary>
        /// Deletes a board from the server
        /// </summary>
        /// <param name="name">The name of the board to delete</param>
        /// <returns>The string OK</returns>
        [HttpPost]
        public IActionResult deleteBoard(string name)
        {
            Board board = gameStatus.boards.FirstOrDefault(x => x.name == name);
            // Check if board exists
            if (board != null)
            {
                char _separator = Path.DirectorySeparatorChar;
                string _root = serviceHelpers.rootPath + _separator;
                string filename = name.Replace(" ", "");
                // Remove board from memory
                gameStatus.boards.Remove(board);
                // Delete board files
                if (System.IO.File.Exists(_root + "wwwroot" + _separator + "images" + _separator + "printable_boards" + _separator + filename + ".png"))
                {
                    System.IO.File.Delete(_root + "wwwroot" + _separator + "images" + _separator + "printable_boards" + _separator + filename + ".png");
                }
                if (System.IO.File.Exists(_root + "wwwroot" + _separator + "images" + _separator + "boards" + _separator + filename + ".png"))
                {
                    System.IO.File.Delete(_root + "wwwroot" + _separator + "images" + _separator + "boards" + _separator + filename + ".png");
                }
                if (System.IO.File.Exists(_root + "GameConfig" + _separator + "Boards" + _separator + filename + ".json"))
                {
                    System.IO.File.Delete(_root + "GameConfig" + _separator + "Boards" + _separator + filename + ".json");
                }
            }
            return Content("OK", "text/plain");
        }

        /// <summary>
        /// Used to tune the robot settings
        /// </summary>
        /// <returns>The view</returns>
        public IActionResult Tuning()
        {
            // These locks are likely overkill, but it's important that only one setup instance is interacting with the bots
            lock (gameStatus.locker)
            {
                lock (gameStatus.setupLocker)
                {
                    // Check if physical bots are being used
                    if (gameStatus.botless)
                    {
                        return RedirectToAction("Index");                        
                    }
                    // Check if game is started
                    if (!gameStatus.gameStarted)
                    {
                        // Check if already tuning robots
                        if (!gameStatus.tuneRobots)
                        {
                            // Collect all the robots
                            foreach (Robot r in gameStatus.robots)
                            {
                                r.y_pos = -1;
                                r.x_pos = -1;
                                r.damage = 0;
                                r.flags = 0;
                                gameStatus.robotPen.Add(r);

                            }
                            gameStatus.robots.Clear();
                            byte i = 0;
                            // Assign a number to each bot
                            foreach (Robot bot in gameStatus.robotPen)
                            {
                                bot.robotNum = i;
                                // Add dummy player to bot, prevents any real player from being assigned to one accidentally while tuning
                                bot.controllingPlayer = i;
                                i++;
                                gameStatus.robots.Add(bot);
                            }
                            gameStatus.robotPen.Clear();
                            gameStatus.tuneRobots = true;
                        }
                        // Build the JSON string of all the robots
                        string bots = "[";
                        bool first = true;
                        foreach (Robot bot in gameStatus.robots)
                        {
                            if (!first)
                            {
                                bots += ",";
                            }
                            else
                            {
                                first = false;
                            }
                            bots += "{\"number\":" + bot.robotNum + ", \"name\":" + "\"" + bot.robotName + "\"}";
                        }
                        bots += "]";
                        ViewBag.Robots = bots;
                        return View();
                    }
                    else
                    {
                        return RedirectToAction("Monitor");
                    }
                }
            }
        }

        /// <summary>
        /// Has a robot enter setup/tuning mode
        /// </summary>
        /// <param name="bot">The bot ID</param>
        /// <returns>OK on success or ER on fail</returns>
        [HttpGet]
        public IActionResult enterBotConfig (int bot)
        {
            string result = "ER";
            bool success;
            int i = -1;
            do
            {
                success = botSignals.sendSetupInstruction(bot, 0, "");
                Thread.Sleep(50);
                i++;
            } while (!success && i < 5);
            if (success)
            {
                result = "OK";
            }
            // Pause to allow robot to get ready
            Thread.Sleep(100);
            return Content(result, "text/plain");
        }

        /// <summary>
        /// Retrieves the current settings from a robot
        /// </summary>
        /// <param name="bot">The bot ID</param>
        /// <returns>A JSON encoded string of the robot settings</returns>
        [HttpGet]
        public IActionResult getBotConfig(int bot)
        {
            string result;

            // Pause to allow robot to get ready
            // Get robot settings
            result = botSignals.getRobotSettings(bot);
            // Cursory check for a JSON looking string
            if (result != "ER" && result != "" && result.Contains("}}}") && result.Contains('{'))
            {
                // Extract the JSON portion of the string only in case there's other stuff in there
                result = result.Substring(result.IndexOf('{'), result.IndexOf("}}}") + 3);
            }
            else
            {
                result = "ER";
            }
            Thread.Sleep(100);
            Console.WriteLine(result);
            return Content(result, "text/plain");
        }

        /// <summary>
        /// Sends a configuration instruction to a robot in setup mode
        /// </summary>
        /// <param name="bot">The robot to send the config to</param>
        /// <param name="option">The action/parameter to set</param>
        /// <param name="value">The value to set</param>
        /// <param name="robotName">The robot's name</param>
        /// <returns>The response from the robot</returns>
        [HttpGet]
        public IActionResult botConfig(int bot, int option, string value, string robotName)
        {
            string result = "ER";
            Console.WriteLine("Sending config info. bot: " + bot + " option: " + option + " value: " + value);
            bool success;
            int i = -1;
            // Try several times to send instruction to bot
            do
            {
                success = botSignals.sendSetupInstruction(bot, option, value);
                Thread.Sleep(50);
                i++;
            } while (!success && i < 5);
            if (success)
            {
                // Update robot's name if finished with tuning
                if (option == 3)
                {
                    gameStatus.robots[bot].robotName = robotName;
                }
                result = "OK";
                // If robot is entering tuning/setup mode, pause to allow it get ready
                if (option == 1)
                {
                    Thread.Sleep(100);
                }
            }
            return Content(result, "text/plain");
        }


        /// <summary>
        /// Exits the bot tuning mode
        /// </summary>
        /// <returns>A redirect to the setup index page</returns>
        public IActionResult finishBotConfig()
        {
            lock(gameStatus.locker)
            {
                lock(gameStatus.setupLocker)
                {
                    if (gameStatus.tuneRobots)
                    {
                        // Reset bots so they're ready to use
                        foreach (Robot r in gameStatus.robots)
                        {
                            r.y_pos = -1;
                            r.x_pos = -1;
                            r.damage = 0;
                            r.flags = 0;
                            r.controllingPlayer = -1;
                            gameStatus.robotPen.Add(r);
                        }
                        gameStatus.robots.Clear();
                        gameStatus.tuneRobots = false;
                    }
                }
            }
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Gets the status of the game
        /// </summary>
        /// <returns>JSON object containing bot positions, orientations, damage, and flags. Also indicates if bots are re-entering the game and their last checkpoint locations</returns>
        [HttpGet]
        public IActionResult Status()
        {
            lock (gameStatus.setupLocker)
            {
                string result = "{\"players\": {";
                bool first = true;
                string entering = gameStatus.playersNeedEntering ? "1" : "0";
                Robot[] sorted = gameStatus.robots.OrderBy(r => r.controllingPlayer).ToArray();
                // Build the JSON string
                foreach (Robot active in sorted)
                {
                    if (active.controllingPlayer != -1)
                    {
                        if (!first)
                        {
                            result += ",";
                        }
                        first = false;
                        string reenter = "0";
                        if (gameStatus.players[active.controllingPlayer].dead && gameStatus.players[active.controllingPlayer].lives > 0)
                        {
                            reenter = "1";
                        }

                        result += "\"" + active.controllingPlayer.ToString() + "\": {\"number\": " + active.controllingPlayer.ToString() + ", \"lives\":" + gameStatus.players[active.controllingPlayer].lives.ToString() + ", \"x\": " + active.x_pos.ToString() + ", \"y\": " + active.y_pos.ToString() + ", \"direction\": " + active.currentDirection.ToString("D") + ", \"damage\": " + active.damage.ToString() + ", \"flags\": " + active.flags.ToString() + ", \"totalFlags\": " + gameStatus.gameBoard.flags.Length.ToString() + ", \"reenter\": " + reenter + ", \"last_x\": " + active.lastLocation[0].ToString() + ", \"last_y\": " + active.lastLocation[1] + ", \"name\": \"" + active.robotName + "\"}";
                    }
                }
                result += "}, \"entering\": " + entering + "}";
                return Content(result, "application/json");
            }
        }

        /// <summary>
        /// Resets the game to the initial state
        /// </summary>
        /// <param name="resetAll">
        /// 0 to reset current game with same players
        /// 1 to reset entire game to startup state (with same bots)
        /// </param>
        /// <returns>The string "Done"</returns>
        [HttpGet]
        public IActionResult Reset(int resetAll = 0)
        {
            if (!gameStatus.winner)
                Loggers.loggers.ForEach((Logger) => Logger.LogGameEnd(gameStatus.players));
            serviceHelpers.signals.resetGame(resetAll);
            return Content("Done", "text/plain");
        }

        /// <summary>
        /// Toggles the timer state
        /// </summary>
        /// <param name="timerEnable"></param>
        /// <returns>The string "OK"</returns>
        [HttpGet]
        public IActionResult Timer(bool timerEnable)
        {
            gameStatus.playerTimer = timerEnable;
            return Content("OK", "text/plain");
        }
    }
}
