// using museum.Models;
// using museum.Pages;
using Newtonsoft.Json;
using System.Text;

namespace MuseumAdmin.Utils
{
    public class CommonHttpRequest
    {
        public static async Task<T> MakeRequest<T>(string httpMethod, string route, Dictionary<string, string> postParams = null)
        {
            using (var client = new HttpClient())
            {
                HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod(httpMethod), "https://membyapi.azurewebsites.net/memby/api/Musium/" + route);

                if (postParams != null)
                {
                    string json = JsonConvert.SerializeObject(postParams, Newtonsoft.Json.Formatting.Indented);
                    requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                // 🔍 DEBUG: log exact request being sent
                Console.WriteLine("========== HTTP OUTGOING REQUEST ==========");
                Console.WriteLine($"Method : {requestMessage.Method}");
                Console.WriteLine($"URL    : {requestMessage.RequestUri}");

                if (requestMessage.Content != null)
                {
                    var body = await requestMessage.Content.ReadAsStringAsync();
                    Console.WriteLine("Body:");
                    Console.WriteLine(body);
                }

                Console.WriteLine("===========================================");

                HttpResponseMessage response = await client.SendAsync(requestMessage);

                string apiResponse = await response.Content.ReadAsStringAsync();
                
                if (string.IsNullOrWhiteSpace(apiResponse))
                    throw new Exception("Empty API response");

                try
                {
                    return JsonConvert.DeserializeObject<T>(apiResponse);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ JSON PARSE FAILED");
                    Console.WriteLine(apiResponse);
                    throw;
                }
            }
        }

        public static async Task<T> MakeRequest<T>(string httpMethod, string route, object body)
        {
            using (var client = new HttpClient())
            {
                HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod(httpMethod), "https://membyapi.azurewebsites.net/memby/api/Musium/" + route);

                if (body != null)
                {
                    string json = JsonConvert.SerializeObject(body, Newtonsoft.Json.Formatting.Indented);
                    requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                // 🔍 DEBUG: log exact request being sent
                Console.WriteLine("========== HTTP OUTGOING REQUEST (OBJECT) ==========");
                Console.WriteLine($"Method : {requestMessage.Method}");
                Console.WriteLine($"URL    : {requestMessage.RequestUri}");

                if (requestMessage.Content != null)
                {
                    // var content = await requestMessage.Content.ReadAsStringAsync();
                    // Console.WriteLine("Body:");
                    // Console.WriteLine(content);
                }

                Console.WriteLine("====================================================");

                HttpResponseMessage response = await client.SendAsync(requestMessage);

                string apiResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                     Console.WriteLine($"❌ API ERROR: {response.StatusCode}");
                     // Console.WriteLine(apiResponse); // Avoid logging potentially sensitive error details in production logs unless sanitized
                }

                if (string.IsNullOrWhiteSpace(apiResponse))
                {
                     // If it's a void-like success (e.g. 204 or just 200 OK with empty body), and T is not a strong type, handle gracefully?
                     // But user wants robust code.
                     if (response.IsSuccessStatusCode) return default;
                     throw new Exception($"Empty API response with status {response.StatusCode}");
                }

                try
                {
                    return JsonConvert.DeserializeObject<T>(apiResponse);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ JSON PARSE FAILED");
                    Console.WriteLine(apiResponse);
                    throw;
                }
            }
        }



        public static async Task<bool> MakeDeleteRequest(string route)
        {
            using (var client = new HttpClient())
            {
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Delete, "https://membyapi.azurewebsites.net/memby/api/Musium/" + route);

                Console.WriteLine("========== HTTP DELETE REQUEST ==========");
                Console.WriteLine($"URL    : {requestMessage.RequestUri}");
                Console.WriteLine("=========================================");

                HttpResponseMessage response = await client.SendAsync(requestMessage);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ DELETE FAILED: {response.StatusCode}");
                    Console.WriteLine(errorResponse);
                    return false;
                }
            }
        }


        public static async Task<T> MakeRequestMemby<T>(string httpMethod, string route, Dictionary<string, string> postParams = null)
        {
            using (var client = new HttpClient())
            {
                HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod(httpMethod), "https://membyapi.azurewebsites.net/memby/api/Message/" + route);

                if (postParams != null)
                {
                    string json = JsonConvert.SerializeObject(postParams, Newtonsoft.Json.Formatting.Indented);
                    requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }


                HttpResponseMessage response = await client.SendAsync(requestMessage);

                string apiResponse = await response.Content.ReadAsStringAsync();
                try
                {
                    // Attempt to deserialise the reponse to the desired type, otherwise throw an expetion with the response from the api.
                    if (apiResponse != "")
                        return JsonConvert.DeserializeObject<T>(apiResponse);
                    else
                        throw new Exception();
                }
                catch (Exception)
                {
                    throw new Exception($"An error ocurred while calling the API. It responded with the following message: {response.StatusCode} {response.ReasonPhrase}");
                }
            }
        }

        public static int museumId = 0;
        //public static string museumLocation = "";
        public static bool IsSignedIn = false;
        // public static List<Musesum> musesums = new List<Musesum>();
        public static List<object> musesums = new();
        public static string loggedinUserName = string.Empty;
        public static string appPin = string.Empty;
    }
}
