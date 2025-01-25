using Microsoft.AspNetCore.Mvc;
using System.Net;
using RoboRuckus.RuckusCode;
using RoboRuckus.Models;
using System.Net.Mime;

namespace RoboRuckus.Controllers
{
    [Route("/bot/")]
    [ApiController]
    public class BotController : ControllerBase
    {
        /// <summary>
        /// Index fore GET requests
        /// </summary>
        /// <returns>Generic message</returns>
        [HttpGet]
        public IActionResult Index()
        {
            return Content("API is working.", MediaTypeNames.Text.Plain);
        }

        /// <summary>
        /// A bot calls this action to be added to the game as an available robot
        /// </summary>
        /// <param name="botInfo">Description of bot info</param>
        /// <returns>202 Accepted</returns>
        [HttpPut]
        public IActionResult Index(botDescriptionModel botInfo)
        {
            IPAddress botIP = IPAddress.Parse(botInfo.ip);
            botSignals.addBot(botIP, botInfo.name);
            // Send acknowledgment to bot
            return Accepted();
        }

        /// <summary>
        /// A bot calls this action when it's completed a move
        /// </summary>
        /// <param name="botModel">The bot number</param>
        /// <returns>202 Accepted</returns>
        [HttpPost("Done/")]
        public IActionResult Done(botNumberModel botModel)
        {
            botSignals.Done(botModel.bot);
            // Send acknowledgment to bot
            return Accepted();
        } 
        
    }
}