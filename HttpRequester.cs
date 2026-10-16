using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Crestron.SimplSharp;

namespace MatroxMaevex;

/// <summary>
/// Exécute une requête HTTP(S) à partir des serials de l'EISC à chaque impulsion
/// sur le digital SendRequest, et renvoie le résultat vers SIMPL.
/// </summary>
public class HttpRequester
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Intersystem _intersystem;
    private readonly HttpClient _httpClient;
    private int _busy;

    public HttpRequester(Intersystem intersystem)
    {
        _intersystem = intersystem;

        // Les Maevex utilisent un certificat auto-signé (équivalent de curl -k).
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        _httpClient = new HttpClient(handler) { Timeout = Timeout };

        _intersystem.BoolChanged += Intersystem_BoolChanged;
    }

    private void Intersystem_BoolChanged(uint join, bool value)
    {
        if (join == Joins.SendRequest && value)
        {
            _ = SendAsync();
        }
    }

    private async Task SendAsync()
    {
        if (System.Threading.Interlocked.Exchange(ref _busy, 1) == 1)
        {
            CrestronConsole.PrintLine("Requête ignorée : une requête est déjà en cours.");
            return;
        }

        var statusCode = 0;
        var response = "";
        var error = "";

        try
        {
            _intersystem.SetBool(Joins.Busy, true);
            _intersystem.SetString(Joins.Response, "");
            _intersystem.SetString(Joins.ErrorMessage, "");

            var request = BuildRequest();
            CrestronConsole.PrintLine("HTTP {0} {1}", request.Method, request.RequestUri);

            using var httpResponse = await _httpClient.SendAsync(request);
            statusCode = (int)httpResponse.StatusCode;
            response = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
            {
                error = $"HTTP {statusCode} {httpResponse.ReasonPhrase}";
            }
            else if (ResultIsFailure(response, out var resultValue))
            {
                error = $"Result.IsSuccess = false ({resultValue})";
            }
        }
        catch (TaskCanceledException)
        {
            error = "Timeout";
        }
        catch (Exception e)
        {
            error = e.Message;
        }
        finally
        {
            CrestronConsole.PrintLine("HTTP {0} : {1}", statusCode, error == "" ? "OK" : error);

            // Les valeurs sont posées avant l'impulsion pour que SIMPL puisse les lire sur le front.
            _intersystem.SetUShort(Joins.StatusCode, (ushort)statusCode);
            _intersystem.SetString(Joins.Response, response);
            _intersystem.SetString(Joins.ErrorMessage, error);
            _intersystem.PulseBool(error == "" ? Joins.Success : Joins.Error);
            _intersystem.SetBool(Joins.Busy, false);

            System.Threading.Interlocked.Exchange(ref _busy, 0);
        }
    }

    private HttpRequestMessage BuildRequest()
    {
        var host = _intersystem.GetString(Joins.Host).Trim();
        var login = _intersystem.GetString(Joins.Login);
        var password = _intersystem.GetString(Joins.Password);
        var method = _intersystem.GetString(Joins.Method).Trim().ToUpperInvariant();
        var path = _intersystem.GetString(Joins.Path).Trim();
        var body = _intersystem.GetString(Joins.Body);

        if (host == "")
        {
            throw new InvalidOperationException("Host non renseigné");
        }

        var baseUrl = host.Contains("://") ? host.TrimEnd('/') : $"https://{host.TrimEnd('/')}";
        if (path != "" && !path.StartsWith('/'))
        {
            path = "/" + path;
        }

        var request = new HttpRequestMessage(new HttpMethod(method == "" ? "GET" : method), baseUrl + path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (login != "")
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{login}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        if (!string.IsNullOrEmpty(body))
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return request;
    }

    /// <summary>
    /// Les réponses Maevex contiennent un objet Result { IsSuccess, Value } :
    /// un HTTP 200 peut donc quand même signaler un échec.
    /// </summary>
    private static bool ResultIsFailure(string response, out string value)
    {
        value = "";
        try
        {
            var result = JsonNode.Parse(response)?["Result"];
            if (result?["IsSuccess"] is JsonValue isSuccess && isSuccess.TryGetValue(out bool ok) && !ok)
            {
                value = result["Value"]?.ToString() ?? "";
                return true;
            }
        }
        catch (Exception)
        {
            // Réponse non JSON : on se fie au code HTTP.
        }
        return false;
    }
}
