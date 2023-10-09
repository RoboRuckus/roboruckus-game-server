﻿using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RoboRuckus.Models;
using RoboRuckus.RuckusCode;
using System.Linq;

namespace RoboRuckus.Controllers
{
    public class PlayerController : Controller
    {
        /// <summary>
        /// Handles when a player connects. If the player is already
        /// in game, sends them their status, otherwise attempts to add
        /// them to the game.
        /// </summary>
        /// <param name="player">The player number, if they have one</param>
        /// <returns>The view or action context</returns>
        public IActionResult Index(int player = 0)
        {
            if (gameStatus.gameReady)
            {
                // See if player is not in game or needs setup
                if (!gameStatus.players.Any(p => p.playerNumber == (player - 1)) || gameStatus.players[player - 1].playerRobot == null)
                {
                    return RedirectToAction("playerSetup", new { player });
                }
                // Player is in game, return view
                ViewBag.player = player;
                ViewBag.robot = gameStatus.players[player - 1].playerRobot.robotName;
                ViewBag.damage = gameStatus.players[player - 1].playerRobot.damage;
                ViewBag.started = gameStatus.gameStarted;
                return View();
            }
            else
            {
                // Game is not set up
                return View("~/Views/Player/settingUp.cshtml");
            }
        }

        /// <summary>
        /// Attempts to add a player to the game
        /// </summary>
        /// <param name="player">The player number</param>
        /// <returns>The view</returns>
        public IActionResult addPlayer(int player = 0)
        {
            int playerNumber;
            // Attempt to add player to game
            if (player == 0)
            {
                playerNumber = gameStatus.addPlayer();
            }
            // See if player is already in game, and if not, try to add them
            else
            {
                if (gameStatus.players.Any(p => p.playerNumber == (player - 1)))
                {
                    playerNumber = player;
                }
                else
                {
                    playerNumber = gameStatus.addPlayer();
                }
            }
            // Check if game is full
            if (playerNumber == 0)
            {
                return View("~/Views/Player/Full.cshtml");
            }
            else
            {
                // Success!
                return RedirectToAction("playerSetup", new { player = playerNumber });
            }
        }

        /// <summary>
        /// Set's up a player
        /// </summary>
        /// <param name="player">The player number</param>
        /// <returns>The view</returns>
        public IActionResult playerSetup(int player = 0, int reset = 0)
        {
            // Double check player is in game
            if (!gameStatus.players.Any(p => p.playerNumber == (player - 1)))
            {
                return RedirectToAction("addPlayer", new { player });
            }
            // Check if player is already set up
            else if (gameStatus.players[player - 1].playerRobot != null && reset !=1)
            {
                return RedirectToAction("Index", new { player });
            }
            // Set up player
            else
            {
                ViewBag.flags = JsonConvert.SerializeObject(gameStatus.gameBoard.flags);
                ViewBag.board_x = gameStatus.boardSizeX;
                ViewBag.board_y = gameStatus.boardSizeY;
                ViewBag.player = player;
                ViewBag.board = gameStatus.gameBoard.name.Replace(" ", "");
                ViewBag.reset = reset;
                if (reset == 1)
                {
                    ViewBag.botName = gameStatus.players[player - 1].playerRobot.robotName;
                }
                return View();
            }
        }

        /// <summary>
        /// Let's a player setup their parameters
        /// </summary>
        /// <param name="playerData">The player data needed for setup</param>
        /// <returns>The view</returns>
        [HttpPost]
        public IActionResult setupPlayer(playerSetupViewModel playerData)
        {
            lock (gameStatus.locker)
            {
                lock (gameStatus.setupLocker)
                {
                    // Check if robot was already assigned
                    if (!gameStatus.assignBot(playerData.player, playerData.botName))
                    {
                        return RedirectToAction("playerSetup", new { playerData.player });
                    }
                    // Check it robot's coordinates are taken
                    if (gameStatus.robots.Any(r => (r.x_pos == playerData.botX && r.y_pos == playerData.botY)))
                    {
                        return RedirectToAction("playerSetup", new { playerData.player });
                    }
                    else
                    {
                        Player sender = gameStatus.players[playerData.player - 1];
                        sender.playerRobot.x_pos = playerData.botX;
                        sender.playerRobot.y_pos = playerData.botY;
                        sender.playerRobot.lastLocation = new int[] { playerData.botX, playerData.botY };
                        sender.playerRobot.currentDirection = (Robot.orientation)playerData.botDir;
                        return RedirectToAction("Index", new { playerData.player });
                    }
                }
            }
        }

        /// <summary>
        /// Gets the status of the player setup
        /// </summary>
        /// <returns>JSON object containing bot positions, orientations, and available robot names</returns>
        [HttpGet]
        public IActionResult Status()
        {
            string result = "{\"robots\": {";
            bool first = true;
            foreach (Robot active in gameStatus.robots)
            {
                if (active.controllingPlayer != null)
                {
                    if (!first)
                    {
                        result += ",";
                    }
                    first = false;
                    result += "\"" + active.controllingPlayer.playerNumber.ToString() + "\": {\"number\": " + active.controllingPlayer.playerNumber.ToString() + ",\"x\": " + active.x_pos.ToString() + ",\"y\": " + active.y_pos.ToString() + ",\"direction\": " + active.currentDirection.ToString("D") + "}";
                }
            }
            result += "}, \"botNames\": " + JsonConvert.SerializeObject(gameStatus.robotPen.Select(r => r.robotName).ToArray()) + "}";
            return Content(result, "application/json");
        }

        /// <summary>
        /// Shows statuses of all current players
        /// </summary>
        /// <returns>The view</returns>
        public IActionResult Statuses()
        {
            if (gameStatus.gameStarted)
            {
                string[][] stats = new string[gameStatus.numPlayersInGame][];
                int i = 0;
                foreach (Player player in gameStatus.players)
                {
                    stats[i] = new string[] { player.playerRobot.robotName, player.playerRobot.damage.ToString(), player.playerRobot.flags.ToString(), player.lives.ToString() };
                    i++;
                }
                ViewBag.totalFlags = gameStatus.gameBoard.flags.Count();
                ViewBag.stats = stats;
                return View();
            }
            else
            {
                return Content("<h2>Game is not set up.</h2>", "text/html");
            }
        }

        public IActionResult Error()
        {
            return View("~/Views/Shared/Error.cshtml");
        }
    }
}