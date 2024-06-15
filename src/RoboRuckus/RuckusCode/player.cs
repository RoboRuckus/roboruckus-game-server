using System.Collections.Generic;
using RoboRuckus.Logging;

namespace RoboRuckus.RuckusCode
{
    public class Player(byte Number)
    {
        /// <summary>
        /// Zero ordered player number
        /// </summary>
        public byte playerNumber = Number;

        public Robot playerRobot = null;

        /// <summary>
        /// The cards currently dealt to the player
        /// </summary>
        public byte[] cards = null;

        /// <summary>
        /// The player's submitted move
        /// </summary>
        public cardModel[] move = null;

        /// <summary>
        /// The player's currently locked cards
        /// </summary>
        public List<byte> lockedCards = [];

        /// <summary>
        /// True if player is currently shut down
        /// </summary>
        public bool shutdown = false;

        /// <summary>
        /// True if player is shutting down this turn
        /// </summary>
        public bool willShutdown = false;

        /// <summary>
        /// How many lives a player has left.
        /// When it reaches 0 they are out of the game.
        /// </summary>
        public int lives = 3;

        private bool _dead;
        /// <summary>
        /// Show's whether a player is dead, and if they are removes a life
        /// </summary>
        public bool dead
        {
            get
            {
                return _dead;
            }
            set
            {
                if (gameStatus.gameStarted)
                {
                    lock (gameStatus.locker)
                    {
                        if (value == true)
                        {
                            foreach (byte card in lockedCards)
                            {
                                gameStatus.lockedCards.Remove(card);
                            }
                            lockedCards.Clear();
                            playerRobot.x_pos = -1;
                            playerRobot.y_pos = -1;
                            if (lives > 0 && !_dead)
                            {
                                lives--;
                            }
                            Loggers.loggers.ForEach((Logger) => Logger.LogBotDeath(this));
                        }
                        _dead = value;
                    }
                }
            }
        }
    }
}