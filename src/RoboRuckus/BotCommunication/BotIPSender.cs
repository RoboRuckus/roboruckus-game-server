using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;

namespace RoboRuckus.BotCommunication
{
    /// <summary>
    /// Static class with helper functions for sending messages to robots via HTTP.
    /// </summary>
    public static class BotIPSender
    {

        /// <summary>
        /// Sends data to a robot.
        /// </summary>
        /// <param name="url">The URL to use</param>
        /// <param name="method">The HTTP method to use.</param>
        /// <param name="data">Data to send, if any.</param>
        /// <returns>Response from robot or FAIL</returns>
        public static async Task<string> sendDataToRobot(string url, HttpMethod method, Dictionary<string, string> data = null)
        {
            string response = "FAIL";
            if (method == HttpMethod.Get)
            {
                response = await sendBotGetRequest(url);
            }
            else if (method == HttpMethod.Post)
            {
                response = await sendBotPostRequest(url, data, "application/x-www-form-urlencoded");
            }
            else if (method == HttpMethod.Put)
            {
                response = await sendBotPutRequest(url, data, "application/x-www-form-urlencoded");
            }
            return response;
        }

        /// <summary>
        /// Sends an HTTP GET request to a robot.
        /// </summary>
        /// <param name="url">The URL to request.</param>
        /// <returns>The response from the robot.</returns>
        private static async Task<string> sendBotGetRequest(string url)
        {
            http_client = new HttpClient(handler);
            using HttpResponseMessage response = await http_client.GetAsync(url);
            string message = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return message;
            }
            return "FAIL";
        }

        /// <summary>
        /// Sends an HTTP PUT request to a robot.
        /// </summary>
        /// <param name="url">The URL to request.</param>
        /// <param name="data">The data to send as key/value pairs.</param>
        /// <param name="contentType">The content type to send.</param>
        /// <returns>The response from the robot.</returns>
        private static async Task<string> sendBotPutRequest(string url, Dictionary<string, string> data, string contentType)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, url);
            if (data != null) 
                request.Content = new FormUrlEncodedContent(data);
            http_client = new HttpClient(handler);
            http_client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
            using HttpResponseMessage response = await http_client.SendAsync(request);
            string message = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return message;
            }
            return "FAIL";
        }

        /// <summary>
        /// Sends an HTTP PUT request to a robot.
        /// </summary>
        /// <param name="url">The URL to request.</param>
        /// <param name="data">The data to send as key/value pairs.</param>
        /// <param name="contentType">The content type to send.</param>
        /// <returns>The response from the robot.</returns>
        private static async Task<string> sendBotPostRequest(string url, Dictionary<string, string> data, string contentType)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            if (data != null)
                request.Content = new FormUrlEncodedContent(data);
            http_client = new HttpClient(handler);
            http_client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
            using HttpResponseMessage response = await http_client.SendAsync(request);
            string message = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return message;
            }
            return "FAIL";
        }

        /// <summary>
        /// Reusable HttpClient.
        /// </summary>
        private static HttpClient http_client = new();
        /// <summary>
        /// Reusable HttpClientHandler.
        /// </summary>
        private static HttpClientHandler handler = new()
        {
            AutomaticDecompression = DecompressionMethods.All
        };

    }
}
