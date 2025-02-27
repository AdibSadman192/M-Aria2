using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MAria2.Infrastructure.Engines.Aria2;

public class Aria2ConnectionManager
{
    private readonly Aria2Configuration _configuration;
    private readonly HttpClient _httpClient;

    public Aria2ConnectionManager(Aria2Configuration configuration)
    {
        _configuration = configuration;
        _httpClient = new HttpClient
        {
            Timeout = configuration.Timeout
        };
    }

    public async Task<JToken> SendRpcRequestAsync(string method, params object[] parameters)
    {
        var request = new
        {
            jsonrpc = "2.0",
            method = $"aria2.{method}",
            id = Guid.NewGuid().ToString(),
            @params = CreateParameterList(parameters)
        };

        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_configuration.GetRpcUri(), content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var responseObject = JObject.Parse(responseContent);

        if (responseObject["error"] != null)
        {
            throw new Exception($"Aria2 RPC Error: {responseObject["error"]["message"]}");
        }

        return responseObject["result"];
    }

    private object[] CreateParameterList(object[] parameters)
    {
        // Add secret token if configured
        if (!string.IsNullOrEmpty(_configuration.Secret))
        {
            return new[] { $"token:{_configuration.Secret}" }.Concat(parameters).ToArray();
        }
        return parameters;
    }
}
