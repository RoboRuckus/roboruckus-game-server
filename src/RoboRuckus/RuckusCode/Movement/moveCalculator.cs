﻿
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace RoboRuckus.RuckusCode.Movement
{
    /// <summary>
    /// Controls all bot movement
    /// Wrapping public methods in lock statements is probably overkill, but doesn't hurt.
    /// </summary>
    public static class moveCalculator
    {
        /// <summary>
        /// Enumeration for movement commands for robot
        /// </summary>
        public enum movement
        {
            Left = 0,
            Right = 1,
            Forward = 2,
            Backward = 3,
            slideLeft = 4,
            slideRight = 5
        }

        /// <summary>
        /// Executes and resolves the round registers, including board effects
        /// </summary>
        public static void executeRegisters()
        {
            lock (gameStatus.locker)
            {
                List<orderModel> orders = new List<orderModel>();
                // Loop through each register
                for (int i = 0; i < 5; i++)
                {
                    if (gameStatus.robots.Any(r => !gameStatus.players[r.controllingPlayer].dead))
                    {
                        // Move robots
                        executePlayerMoves(i);
                        serviceHelpers.signals.updateHealth();
                        Thread.Sleep(1000);

                        // Move express conveyors
                        boardEffects.moveConveyors(true);
                        serviceHelpers.signals.updateHealth();
                        Thread.Sleep(1000);

                        // Move all conveyors
                        boardEffects.moveConveyors(false);
                        serviceHelpers.signals.updateHealth();
                        Thread.Sleep(1000);

                        // Rotate turntables
                        boardEffects.executeTurnTables();
                        Thread.Sleep(1000);

                        // Fire lasers
                        if (boardEffects.fireLasers())
                        {
                            Thread.Sleep(800);
                            serviceHelpers.signals.updateHealth();
                            Thread.Sleep(2000);
                        }
                        else
                        {
                            Thread.Sleep(800);
                        }

                        // Heal from wrenches and flags
                        Robot[] healed = boardEffects.wrenches();
                        if (healed.Length > 0)
                        {
                            foreach (Robot bot in healed)
                            {
                                bot.damage--;
                                // Update "archive marker" (a.k.a. new respawn location)
                                if (!gameStatus.players[bot.controllingPlayer].shutdown)
                                {
                                    bot.lastLocation[0] = bot.x_pos;
                                    bot.lastLocation[1] = bot.y_pos;
                                }
                            }
                            serviceHelpers.signals.updateHealth();
                            Thread.Sleep(1650);                            
                        }
                        
                        Robot winner = null;
                        if (gameStatus.gameBoard.flags.Length > 0)
                        {
                            // Touch flags
                            List<int[]> touched = boardEffects.flags();
                            if (touched.Count > 0)
                            {
                                bool sound = false;
                                foreach (int[] pair in touched)
                                {
                                    Robot bot = gameStatus.robots[pair[0]];
                                    if (bot.flags == pair[1])
                                    {
                                        bot.flags++;
                                        bot.lastLocation[0] = bot.x_pos;
                                        bot.lastLocation[1] = bot.y_pos;
                                        sound = true;
                                    }
                                    if (sound)
                                    {
                                        serviceHelpers.signals.showMessage("Touching flags", "flagTouch");
                                        Thread.Sleep(1000);
                                    }
                                }
                                
                            }
                            // Check for flag win condition
                            winner = gameStatus.robots.FirstOrDefault(r => r.flags == gameStatus.gameBoard.flags.Length);
                        }
                        if (winner == null && gameStatus.numPlayersInGame > 1)
                        {
                            Player[] alive = gameStatus.players.Where(p => p.lives > 0).ToArray();
                            if (alive.Length == 1)
                            {
                                winner = alive[0].playerRobot;
                            }
                        }
                        // Check for winner                       
                        if (winner != null)
                        {
                            serviceHelpers.signals.showMessage((winner.robotName).ToString() + " has won!", "winner");
                            gameStatus.winner = true;
                            Thread.Sleep(250);
                            // Do a victory dance
                            processMoveOrder(new orderModel { botNumber = winner.robotNum, magnitude = 4, move = movement.Right });
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Processes a bot move order
        /// </summary>
        /// <param name="order">The order to process</param>
        /// <returns>True if the bot successfully received and completed the move</returns>
        public static bool processMoveOrder(orderModel order)
        {
            lock(gameStatus.locker)
            {
                // Check if edge control is enabled
                bool offBoardMessage = false;
                if (gameStatus.edgeControl)
                {
                    // See if a robot is moving off the board
                    if (order.offBoard)
                    {
                        offBoardMessage = true;
                        // Prevent the robot from physically moving off the board
                        if (order.magnitude > 0)
                        {
                            order.magnitude--;
                        }
                    }
                }
                Timer watchDog;
                // For debugging/diagnostics
                //System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Reset the bot's wait handle
                gameStatus.robots[order.botNumber].moving.Reset();

                // Start watch dog to skip bots that don't respond in 3 seconds
                bool timeout = false;
                watchDog = new Timer(delegate { Console.WriteLine("Bot didn't acknowledge move order"); timeout = true; }, null, 3000, Timeout.Infinite);

                // Wait for bot to acknowledge receipt of orders
                SpinWait.SpinUntil(() => botSignals.sendMoveCommand(order) == "OK" || timeout);

                // Dispose the watch dog
                watchDog.Dispose();

                if (!timeout)
                {
                    // Start a watchdog to skip bots that don't finish moving in 7 seconds (may need tweaking or removing)
                    watchDog = new Timer(delegate { Console.WriteLine("Bot didn't finish moving"); timeout = true; gameStatus.robots[order.botNumber].moving.Set(); }, null, 7000, Timeout.Infinite);

                    // Wait for bot to finish moving
                    gameStatus.robots[order.botNumber].moving.WaitOne();

                    // Dispose the watch dog
                    watchDog.Dispose();
                }
                // Let the bot become ready again (min: 150ms)
                Thread.Sleep(250);

                // For debugging/diagnostics
                //stopwatch.Stop();
                //Console.WriteLine(stopwatch.Elapsed.TotalMilliseconds);

                if (offBoardMessage)
                {
                    // Display a message and pause the game to allow the GM to remove bots that are supposed to be off the board.
                    serviceHelpers.signals.showMessage(gameStatus.robots[order.botNumber].robotName + " is off the board and has died.");
                    Thread.Sleep(4000);
                }

                return !timeout;
            }
        }

        /// <summary>
        /// Calculates and resolves all necessary moves for a robot and
        /// any other robots affected by the move.
        /// </summary>
        /// <param name="move">The movmeModel to resolve</param>
        /// <returns>A list of orders for robots</returns>
        public static List<orderModel> calculateMove(moveModel move)
        {
            lock (gameStatus.locker)
            {
                Random rand = new Random();
                List<orderModel> orders = new List<orderModel>();
                // Check for the kind of movement, and resolve turns here
                switch (move.card.direction)
                {
                    case ("forward"):
                        // Resolve complicated move here
                        resolveMove(move.bot, move.bot.currentDirection, move.card.magnitude, ref orders, false);
                        break;

                    case ("left"):
                        // Change the robot's orientation to what it will be after the move
                        if (move.bot.currentDirection == Robot.orientation.NEG_Y)
                        {
                            move.bot.currentDirection = Robot.orientation.X;
                        }
                        else
                        {
                            move.bot.currentDirection++;
                        }
                        orders.Add(new orderModel { botNumber = move.bot.robotNum, move = movement.Left, magnitude = 1 });
                        break;

                    case ("right"):
                        if (move.bot.currentDirection == Robot.orientation.X)
                        {
                            move.bot.currentDirection = Robot.orientation.NEG_Y;
                        }
                        else
                        {
                            move.bot.currentDirection--;
                        }
                        orders.Add(new orderModel { botNumber = move.bot.robotNum, move = movement.Right, magnitude = 1 });
                        break;

                    case ("backup"):
                        Robot.orientation opposite = Robot.orientation.Y;
                        switch (move.bot.currentDirection)
                        {
                            case Robot.orientation.X:
                                opposite = Robot.orientation.NEG_X;
                                break;
                            case Robot.orientation.NEG_X:
                                opposite = Robot.orientation.X;
                                break;
                            case Robot.orientation.Y:
                                opposite = Robot.orientation.NEG_Y;
                                break;
                            case Robot.orientation.NEG_Y:
                                opposite = Robot.orientation.Y;
                                break;
                        }
                        resolveMove(move.bot, opposite, move.card.magnitude, ref orders, false);
                        break;

                    case ("uturn"):
                        switch (move.bot.currentDirection)
                        {
                            case Robot.orientation.X:
                                move.bot.currentDirection = Robot.orientation.NEG_X;
                                break;
                            case Robot.orientation.NEG_X:
                                move.bot.currentDirection = Robot.orientation.X;
                                break;
                            case Robot.orientation.Y:
                                move.bot.currentDirection = Robot.orientation.NEG_Y;
                                break;
                            case Robot.orientation.NEG_Y:
                                move.bot.currentDirection = Robot.orientation.Y;
                                break;
                        }
                        orders.Add(new orderModel { botNumber = move.bot.robotNum, move = (movement)rand.Next(0, 2), magnitude = 2 });
                        break;
                }
                return orders;
            }
        }

        /// <summary>
        /// Creates a movement program that resolves one bot's movement and its
        /// impact on any other bots on the board. Executes recursively.
        /// </summary>
        /// <param name="bot">The bot being moved</param>
        /// <param name="direction">The direction the bot is moving.</param>
        /// <param name="magnitude">The number of spaces being moved</param>
        /// <param name="orders">A reference to the list of move orders to modify</param>
        /// <param name="onConveyor">True if the movement is caused by a conveyor belt</param>
        /// <returns>The total number of spaces the bot will actually be moving (i.e. is able to move)</returns>
        public static int resolveMove(Robot bot, Robot.orientation direction, int magnitude, ref List<orderModel> orders, bool onConveyor)
        {
            lock(gameStatus.locker)
            {
                int newCordX = -1;
                int newCordY = -1;
                int[] destination = new int[2];
                bool _offBoard = false;
                bool botDied = false;
                // Check for edge or obstacles in path
                newCordX = bot.x_pos;
                newCordY = bot.y_pos;
                for (int i = 1; i <= magnitude; i++)
                {
                    switch (direction)
                    {
                        case Robot.orientation.X:
                            newCordX++;
                            break;
                        case Robot.orientation.Y:
                            newCordY++; ;
                            break;
                        case Robot.orientation.NEG_X:
                            newCordX--;
                            break;
                        case Robot.orientation.NEG_Y:
                            newCordY--;
                            break;
                    }
                    destination = new int[] { newCordX, newCordY };
                    // Check for walls and other obstacles
                    if (isObstacle(new int[] { bot.x_pos, bot.y_pos }, destination, direction))
                    {
                        magnitude = i - 1;
                        break;
                    }
                    // Check for pits
                    else if (boardEffects.onPit(destination))
                    {
                        magnitude = i;
                        botDied = true;
                        break;
                    }
                    // Check for edge of board
                    else if (newCordX > gameStatus.boardSizeX || newCordY > gameStatus.boardSizeY || newCordX < 0 || newCordY < 0)
                    {
                        _offBoard = true;
                        botDied = true;
                        magnitude = i;
                        break;
                    }
                }
                int total = magnitude;
                // Check if there's no movement
                if (magnitude == 0)
                {
                    orders.Add(new orderModel { botNumber = bot.robotNum, move = movement.Forward, magnitude = 0, offBoard = _offBoard });
                    return 0;
                }
                else
                {
                    Robot botFound;
                    int remaining;
                    // Robots on conveyors should have already been cleared of bumping into other robots in the moveConveyors method
                    if (!onConveyor)
                    {
                        // Check for, and resolve movements of, other robots in the way
                        remaining = magnitude;
                        botFound = null;
                        for (int i = 1; i <= magnitude; i++)
                        {
                            switch (direction)
                            {
                                case Robot.orientation.X:
                                    destination = new int[] { bot.x_pos + i, bot.y_pos };
                                    break;
                                case Robot.orientation.Y:
                                    destination = new int[] { bot.x_pos, newCordY = bot.y_pos + i };
                                    break;
                                case Robot.orientation.NEG_X:
                                    destination = new int[] { newCordX = bot.x_pos - i, bot.y_pos };
                                    break;
                                case Robot.orientation.NEG_Y:
                                    destination = new int[] { bot.x_pos, newCordY = bot.y_pos - i };
                                    break;
                            }
                            // Check for any other bots on that space
                            botFound = gameStatus.robots.FirstOrDefault(r => (r.robotNum != bot.robotNum && r.x_pos == destination[0] && r.y_pos == destination[1]));
                            
                            if (botFound != null)
                            {
                                break;
                            }
                            remaining--;
                        }
                    }
                    // Robot is on a conveyor, other bots should already have been factored in
                    else
                    {
                        remaining = 0;
                        botFound = null;
                    }

                    total = magnitude - remaining;
                    
                    // Move robot forward as far as possible before finishing the move or hitting an obstacle 
                    if (total > 0)
                    {                        
                        switch (direction - bot.currentDirection)
                        {
                            case 0:
                                orders.Add(new orderModel { botNumber = bot.robotNum, move = movement.Forward, magnitude = total, offBoard = _offBoard });
                                break;
                            case -2:
                            case 2:
                                orders.Add(new orderModel { botNumber = bot.robotNum, move = movement.Backward, magnitude = total, offBoard = _offBoard });
                                break;
                            case 3:
                            case -1:
                                orders.Add(new orderModel { botNumber = bot.robotNum, move = movement.slideRight, magnitude = total, offBoard = _offBoard });
                                break;
                            case -3:
                            case 1: 
                                orders.Add(new orderModel { botNumber = bot.robotNum, move = movement.slideLeft, magnitude = total, offBoard = _offBoard });
                                break;
                        }
                    }
                    if (botFound != null)
                    {
                        // If another bot was in the way, move it as far as possible and then check how far it moves
                        int otherMoved = resolveMove(botFound, direction, remaining, ref orders, false);
                        if (otherMoved > 0)
                        {
                            // Check how we're moving
                            switch (direction - bot.currentDirection)
                            {
                                case 0:
                                    orders.Add(new orderModel { botNumber = bot.robotNum, move = movement.Forward, magnitude = otherMoved, offBoard = _offBoard });
                                    break;
                                case -2:
                                case 2:
                                    orders.Add(new orderModel { botNumber = bot.robotNum, move = movement.Backward, magnitude = otherMoved, offBoard = _offBoard });
                                    break;
                                case 3:
                                case -1:
                                    orders.Add(new orderModel { botNumber = bot.robotNum, move = movement.slideRight, magnitude = otherMoved, offBoard = _offBoard });
                                    break;
                                case -3:
                                case 1:
                                    orders.Add(new orderModel { botNumber = bot.robotNum, move = movement.slideLeft, magnitude = otherMoved, offBoard = _offBoard });
                                    break;
                            }
                            total += otherMoved;
                        }
                    }

                    // Update robot's coordinates for total movement
                    if (total > 0)
                    {
                        switch (direction)
                        {
                            case Robot.orientation.X:
                                bot.x_pos += total;
                                break;
                            case Robot.orientation.NEG_X:
                                bot.x_pos -= total;
                                break;
                            case Robot.orientation.Y:
                                bot.y_pos += total;
                                break;
                            case Robot.orientation.NEG_Y:
                                bot.y_pos -= total;
                                break;
                        }
                    }

                    // Update if robot has died
                    if (botDied)
                    {
                        bot.damage = 10;
                    }
                    return total;
                }
            }
        }

        /// <summary>
        /// Executes all the player moves in the current register
        /// </summary>
        /// <param name="regsiter">The current register being executed</param>
        private static void executePlayerMoves(int regsiter)
        {
            List<orderModel> orders = new List<orderModel>();
            // Set the size of the register list to all the currently active players
            moveModel[] register = new moveModel[gameStatus.players.Count(p => (!p.dead && !p.shutdown))];
            // Add the cards to the register
            int reg = 0;
            for (int j = 0; j < gameStatus.players.Count; j++)
            {
                if (!gameStatus.players[j].dead && !gameStatus.players[j].shutdown)
                {
                    Player mover = gameStatus.players[j];
                    register[reg] = new moveModel { card = mover.move[regsiter], bot = mover.playerRobot };
                    reg++;
                }
            }
            // Sort the register by card priority
            register = register.OrderByDescending(order => order.card.priority).ToArray();

            // Display register to players
            if (gameStatus.showRegister)
            {
                serviceHelpers.signals.displayRegister(register);
                Thread.Sleep(7000);
            }

            // Resolve a move for each card
            foreach (moveModel move in register)
            {
                // Check if robot has died during the register
                if (!gameStatus.players[move.bot.controllingPlayer].dead)
                {
                    serviceHelpers.signals.displayMove(move, regsiter);
                    orders = calculateMove(move);
                    // Send each order to the appropriate robot
                    foreach (orderModel order in orders)
                    {
                        processMoveOrder(order);
                    }
                }
                if (gameStatus.botless)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        /// <summary>
        /// Checks for obstacles on the board that block bot movement
        /// </summary>
        /// <param name="fromCord">[x,y] The coordinate the bot will be moving from</param>
        /// <param name="toCord">[x,y] The coordinate the bot will be moving to</param>
        /// <param name="direction">The direction to search</param>
        /// <returns>True if there's a non-bot obstacle between those two spaces</returns>
        private static bool isObstacle(int[] fromCord, int[] toCord, Robot.orientation direction)
        {
            // More obstacles will go here if implemented
            int[] wall = boardEffects.findWall(fromCord, toCord, direction);
            return wall != null;
        }
    }

    /// <summary>
    /// A convenient way to pair a movement card with a robot
    /// </summary>
    public class moveModel
    {
        public cardModel card;
        public Robot bot;
    }

    /// <summary>
    /// A convenient representation of a movement order for a robot
    /// </summary>
    public class orderModel
    {
        public int botNumber;
        public moveCalculator.movement move;
        public int magnitude;
        public bool offBoard = false;

        /// <summary>
        /// Creates a string representation of an orderModel which can be sent to the robots
        /// </summary>
        /// <returns>The string representation of an order</returns>
        public override string ToString()
        {
            return ((int)move).ToString() + magnitude.ToString();
        }
    }
}