using RoboRuckus.RuckusCode.Movement;
namespace RoboRuckus.Logging
{
    /// <summary>  
    ///  Interface for logging bot movements
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs the bot movement
        /// </summary>
    
        void LogBotMovement(moveModel botMovement);
    }
}