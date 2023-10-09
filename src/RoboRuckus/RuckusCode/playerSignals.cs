﻿using System;
using System.Threading;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using System.Security.Cryptography;
using RoboRuckus.RuckusCode.Movement;
using RoboRuckus.Hubs;
using System.IO;
using System.Collections.Generic;

namespace RoboRuckus.RuckusCode
{
    /// <summary>
    /// Controls inputs from users.
    /// All public methods in this class should be wrapped in a lock on the same object
    /// since there is only one game state multiple players could try to modify.
    /// </summary>
    public class playerSignals
    {
        // RNG provider  
        private readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();

        // There can be no more than 256 cards in the deck
        private byte numberOfCards;

        // Prevents the player timer from being started multiple times
        private bool timerStarted = false;

        /// <summary>
        /// Player hub context.
        /// </summary>
        private static IHubContext<playerHub> _playerHub;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="hubcontext">DI for hub context</param>
        public playerSignals(IHubContext<playerHub> hubcontext)
        {
            _playerHub = hubcontext;
            numberOfCards = (byte)gameStatus.movementCards.Length;
        }

        /// <summary>
        /// Processes a player's move, if all players have submitted
        /// their moves, executes those moves.
        /// </summary>
        /// <param name="caller">The player client submitting the move</param>
        /// <param name="cards">The cards submitted for their move</param>
        public void submitMove(Player caller, cardModel[] cards)
        {
            lock (gameStatus.locker)
            {
                if (!gameStatus.winner)
                {
                    if (caller.move == null)
                    {
                        caller.move = cards;
                    }

                    // Check to see if timer needs to be started
                    if (!timerStarted && checkTimer())
                    {
                        return;
                    }
                    // Checks if all players have submitted their moves
                    else if (gameStatus.players.Count(p => (p.move != null || p.dead || p.shutdown)) < gameStatus.numPlayersInGame)
                    {
                        return;
                    }
                    timerStarted = false;

                    // List for logging, if enabled
                    List<string> Lines = new List<string>();
                    if (serviceHelpers.logging)
                    {
                        // Log board state and submitted moves to log file
                        Lines.Add("---- Start Round ----");
                        foreach (Player player in gameStatus.players)
                        {
                            Lines.Add("Player: " + (player.playerNumber + 1).ToString());
                            if (player.shutdown == false)
                            {
                                Lines.Add("X: " + player.playerRobot.x_pos.ToString());
                                Lines.Add("Y: " + player.playerRobot.y_pos.ToString());
                                Lines.Add("Facing: " + player.playerRobot.currentDirection.ToString());
                                Lines.Add("Damage: " + player.playerRobot.damage.ToString());
                                Lines.Add("Flags: " + player.playerRobot.flags.ToString());
                                Lines.Add("Moves: ");
                                foreach (cardModel card in player.move)
                                {
                                    Lines.Add(card.ToString());
                                }
                            }
                            else
                            {
                                Lines.Add("Player is shutdown.");
                            }
                            Lines.Add("");
                        }
                    }

                    // Execute player moves                  
                    moveCalculator.executeRegisters();

                    if (serviceHelpers.logging)
                    {
                        Lines.Add("---- Final State ----");
                        foreach (Player player in gameStatus.players)
                        {
                            Lines.Add("Player: " + (player.playerNumber + 1).ToString());
                            Lines.Add("X: " + player.playerRobot.x_pos.ToString());
                            Lines.Add("Y: " + player.playerRobot.y_pos.ToString());
                            Lines.Add("Facing: " + player.playerRobot.currentDirection.ToString());
                            Lines.Add("Damage: " + player.playerRobot.damage.ToString());
                            Lines.Add("Flags: " + player.playerRobot.flags.ToString());
                            Lines.Add("Moves: ");
                            Lines.Add("");
                        }
                        Lines.Add("---- End Round ----");
                    }

                    // Reset for next round
                    if (!gameStatus.winner)
                    {
                        nextRound();
                    }
                    else if (serviceHelpers.logging)
                    {
                        // Log end of game
                        Lines.Add("---- Game End ----");
                    }

                    if (serviceHelpers.logging)
                    {
                        // Write log file
                        File.AppendAllLines(serviceHelpers.rootPath + serviceHelpers.logfile, Lines.ToArray());
                    }
                }
            }
            // Checks is a timer needs to be started right away
            Thread.Sleep(2000);
            checkTimer();
        }

        /// <summary>
        /// Sends a message to the player screens
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="sound">An optional sound effect to play</param>
        public void showMessage(string message, string sound = "")
        {
            lock (gameStatus.locker)
            {
                _playerHub.Clients.All.SendAsync("displayMessage", message, sound);
            }
        }

        /// <summary>
        /// Used to have players request a deal
        /// </summary>
        /// <param name="player">the player to request a deal, -1 for all</param>
        public void dealPlayers(int player = -1)
        {
            lock (gameStatus.locker)
            {
                _playerHub.Clients.All.SendAsync("requestdeal", player);
            }
        }

        /// <summary>
        /// Sends the current move being executed to the players
        /// </summary>
        /// <param name="move">The move model being executed</param>
        /// <param name="register">The current register being executed</param>
        public void displayMove(moveModel move, int register)
        {
            lock (gameStatus.locker)
            {
                string card = gameStatus.movementCards[move.card.cardNumber];
                string robot = move.bot.robotName;
                _playerHub.Clients.All.SendAsync("showMove", card, robot, register + 1);
            }
        }

        /// <summary>
        /// Sends the current register  being executed to the players
        /// </summary>
        /// <param name="register">The register being executed</param>
        public void displayRegister(moveModel[] register)
        {
            lock (gameStatus.locker)
            {
                // Build the card and robots strings
                bool first = true;
                string cards = "[";
                string robots = "[";
                
                foreach (moveModel move in register)
                {
                    string currentCard = gameStatus.movementCards[move.card.cardNumber];
                    if (!first)
                    {
                        cards += ",";
                        robots += ", ";
                    }
                    first = false;
                    cards += currentCard.Insert(currentCard.LastIndexOf("}") - 1, ",\"cardNumber\": " + move.card.cardNumber.ToString());
                    robots += "\"" + move.bot.robotName + "\"";
                }
                cards += "]";
                robots += "]";
                _playerHub.Clients.All.SendAsync("showRegister", cards, robots);
            }
        }

        /// <summary>
        /// Deals cards to a player
        /// </summary>
        /// <param name="caller">The player client requesting a deal</param>
        /// <returns>The cards dealt to the player client</returns>
        public byte[] dealPlayer(Player caller)
        {
            lock (gameStatus.locker)
            {
                if (gameStatus.gameStarted && !caller.dead && !gameStatus.winner)
                {
                    // Check if the player has already been dealt
                    if (caller.cards == null)
                    {
                        byte[] cards;
                        if (caller.shutdown)
                        {
                            cards = new byte[0];
                        }
                        else
                        {
                            cards = new byte[9 - caller.playerRobot.damage];
                            // Draw some cards
                            for (int i = 0; i < cards.Length; i++)
                            {
                                cards[i] = drawCard();
                            }
                        }
                        // Assign cards to player
                        caller.cards = cards;
                        return cards;
                    }
                    else
                    {
                        return caller.cards;
                    }
                }
                else
                {
                    return new byte[0];
                }
            }
        }

        /// <summary>
        /// Updates the health of every player
        /// </summary>
        public void updateHealth()
        {
            lock (gameStatus.locker)
            {
                bool first = true;
                string result = "[";
                foreach (Player inGame in gameStatus.players)
                {
                    if (!first)
                    {
                        result += ",";
                    }
                    first = false;
                    result += inGame.playerRobot.damage.ToString();
                }
                result += "]";
                _playerHub.Clients.All.SendAsync("UpdateHealth", result);
            }
        }

        /// <summary>
        /// Resets the game to the initial state
        /// <param name="resetAll">If 0 reset game with current players, if 1 reset game to initial state</param>
        /// </summary>
        public void resetGame(int resetAll)
        {
            lock (gameStatus.locker)
            {
                gameStatus.winner = false;
                gameStatus.lockedCards.Clear();
                gameStatus.playersNeedEntering = false;
                gameStatus.gameStarted = false;
                if (resetAll == 0)
                {
                    foreach (Player p in gameStatus.players)
                    {
                        p.dead = false;
                        p.lockedCards.Clear();
                        p.move = null;
                        p.cards = null;
                        p.lives = 3;
                        p.shutdown = false;
                        p.willShutdown = false;
                    }
                }
                else
                {
                    gameStatus.players.Clear();
                    gameStatus.gameReady = false;
                    gameStatus.numPlayersInGame = 0;
                }
                foreach (Robot r in gameStatus.robots)
                {
                    r.y_pos = -1;
                    r.x_pos = -1;
                    r.damage = 0;
                    r.flags = 0;
                    if (resetAll == 1)
                    {
                        botSignals.sendReset(r.robotNum);
                        r.controllingPlayer = null;
                        gameStatus.robotPen.Add(r);
                    }
                }
                if (resetAll == 1)
                {
                    gameStatus.robots.Clear();
                }
                _playerHub.Clients.All.SendAsync("Reset", resetAll);
            }
        }

        /// <summary>
        /// Draws a random available card
        /// </summary>
        /// <returns>The card drawn</returns>
        public byte drawCard()
        {
            lock (gameStatus.locker)
            {
                if (numberOfCards <= 0)
                    throw new ArgumentOutOfRangeException("numberOfCards");

                byte drawn;
                do
                {
                    // Create a byte array to hold the random value. 
                    byte[] randomNumber = new byte[1];
                    do
                    {
                        rng.GetBytes(randomNumber);
                    }
                    while (randomNumber[0] >= numberOfCards);
                    drawn = randomNumber[0];
                } while (gameStatus.deltCards.Contains(drawn) || gameStatus.lockedCards.Contains(drawn));
                gameStatus.deltCards.Add(drawn);
                return drawn;
            }
        }

        /// <summary>
        /// Resets the bots and game for the next round
        /// </summary>
        private void nextRound()
        {
            // Reset players
            foreach (Player inGame in gameStatus.players)
            {
                // Remove player's cards
                inGame.cards = null;
                inGame.move = null;
                // Check if player is shutting down
                if (inGame.willShutdown && !inGame.dead)
                {
                    inGame.shutdown = true;
                    inGame.playerRobot.damage = 0;
                    inGame.willShutdown = false;
                }
                else
                {
                    // Bring any shutdown players back online
                    inGame.shutdown = false;
                }
            }
            // Clear dealt cards
            gameStatus.deltCards.Clear();

            // Check for winner
            if (!gameStatus.winner)
            {
                // Check if there are dead players with lives left who need to re-enter the game
                if (gameStatus.players.Any(p => (p.dead && p.lives > 0)))
                {
                    gameStatus.playersNeedEntering = true;
                    showMessage("Dead robots re-entering floor, please be patient.", "entering");
                }
                else
                {
                    showMessage("");
                    // Check if all players are shutdown or out of the game
                    if (!gameStatus.players.All(p => p.lives <= 0) && gameStatus.players.All(p => p.shutdown || p.lives <= 0))
                    {
                        // Clear dealt cards
                        _playerHub.Clients.All.SendAsync("deal", new byte[0], new byte[0]);                        

                        // Alert players to what's happening
                        showMessage("All active players are shutdown, next round starting now.");
                        Thread.Sleep(3000);

                        // Skip directly to executing the movement registers                 
                        moveCalculator.executeRegisters();

                        // Reset for next round
                        if (!gameStatus.winner)
                        {
                            nextRound();
                        }
                        return;
                    }
                    dealPlayers();
                }
            }
        }

        /// <summary>
        /// Checks to see if a player timer needs to be started, and starts one if needed.
        /// </summary>
        /// <returns>Whether a timer was started</returns>
        private bool checkTimer()
        {
            // Makes sure there is more than one living player in the game, and checks if there is only one player who hasn't submitted their program.
            if (gameStatus.playerTimer && gameStatus.players.Count(p => !p.dead) > 1 && gameStatus.players.Count(p => (p.move != null || p.dead || p.shutdown)) == (gameStatus.numPlayersInGame - 1))
            {
                timerStarted = true;
                _playerHub.Clients.All.SendAsync("startTimer");
                return true;
            }
            return false;
        }
    }
}
