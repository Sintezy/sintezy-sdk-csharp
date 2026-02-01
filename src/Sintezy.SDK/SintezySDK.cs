using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Sintezy.SDK
{
    /// <summary>
    /// SDK C# oficial para integração com a API Sintezy.
    /// </summary>
    public class SintezySDK : IDisposable
    {
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _baseUrl;
        private readonly HttpClient _httpClient;
        private AuthToken? _token;

        public SintezySDK(string clientId, string clientSecret, string? baseUrl = null)
        {
            _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
            _baseUrl = baseUrl ?? "https://api.sintezy.com";
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Autentica o SDK obtendo um token de acesso.
        /// </summary>
        public async Task AuthenticateAsync()
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret)
            });

            var response = await _httpClient.PostAsync($"{_baseUrl}/oauth/token", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new SintezySDKException($"Falha na autenticação: {responseContent}", (int)response.StatusCode);
            }

            _token = JsonSerializer.Deserialize<AuthToken>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// Garante que o SDK está autenticado.
        /// </summary>
        public async Task EnsureAuthenticatedAsync()
        {
            if (_token == null || _token.IsExpired)
            {
                await AuthenticateAsync();
            }
        }

        /// <summary>
        /// Cria um novo agendamento.
        /// </summary>
        public async Task<Appointment> CreateAppointmentAsync(CreateAppointmentParams parameters)
        {
            await EnsureAuthenticatedAsync();

            var body = new
            {
                layoutId = parameters.LayoutId,
                userName = parameters.UserName,
                userPhone = parameters.UserPhone,
                layout = parameters.Layout?.Fields ?? new Dictionary<string, object>()
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token!.AccessToken);
            var response = await _httpClient.PostAsync($"{_baseUrl}/sdk/appointments", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new SintezySDKException($"Erro ao criar agendamento: {responseContent}", (int)response.StatusCode);
            }

            return JsonSerializer.Deserialize<Appointment>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;
        }

        /// <summary>
        /// Busca um agendamento pelo ID.
        /// </summary>
        public async Task<Appointment> GetAppointmentAsync(string secureId)
        {
            await EnsureAuthenticatedAsync();

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token!.AccessToken);
            var response = await _httpClient.GetAsync($"{_baseUrl}/sdk/appointments/{secureId}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new SintezySDKException($"Erro ao buscar agendamento: {responseContent}", (int)response.StatusCode);
            }

            return JsonSerializer.Deserialize<Appointment>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;
        }

        /// <summary>
        /// Lista todos os agendamentos.
        /// </summary>
        public async Task<List<Appointment>> ListAppointmentsAsync()
        {
            await EnsureAuthenticatedAsync();

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token!.AccessToken);
            var response = await _httpClient.GetAsync($"{_baseUrl}/sdk/appointments");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new SintezySDKException($"Erro ao listar agendamentos: {responseContent}", (int)response.StatusCode);
            }

            return JsonSerializer.Deserialize<List<Appointment>>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;
        }

        /// <summary>
        /// Gera um documento para o agendamento.
        /// </summary>
        public async Task<Document> GenerateDocumentAsync(string appointmentSecureId, string documentType)
        {
            await EnsureAuthenticatedAsync();

            var body = new { appointmentSecureId };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token!.AccessToken);
            var response = await _httpClient.PostAsync($"{_baseUrl}/sdk/documents/{documentType}", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new SintezySDKException($"Erro ao gerar documento: {responseContent}", (int)response.StatusCode);
            }

            return JsonSerializer.Deserialize<Document>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;
        }

        /// <summary>
        /// Lista documentos de um agendamento.
        /// </summary>
        public async Task<List<DocumentListItem>> ListDocumentsAsync(string appointmentSecureId)
        {
            await EnsureAuthenticatedAsync();

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token!.AccessToken);
            var response = await _httpClient.GetAsync($"{_baseUrl}/sdk/appointments/{appointmentSecureId}/documents");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new SintezySDKException($"Erro ao listar documentos: {responseContent}", (int)response.StatusCode);
            }

            return JsonSerializer.Deserialize<List<DocumentListItem>>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }

    /// <summary>
    /// Token de autenticação.
    /// </summary>
    public class AuthToken
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        private readonly DateTime _createdAt = DateTime.UtcNow;

        public bool IsExpired => DateTime.UtcNow >= _createdAt.AddSeconds(ExpiresIn - 60);
    }

    /// <summary>
    /// Dados do agendamento.
    /// </summary>
    public class Appointment
    {
        public string SecureId { get; set; } = "";
        public string Status { get; set; } = "";
        public string UserName { get; set; } = "";
        public string UserPhone { get; set; } = "";
        public string PortalUrl { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Dados do documento.
    /// </summary>
    public class Document
    {
        public string SecureId { get; set; } = "";
        public string Type { get; set; } = "";
        public JsonElement Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Item resumido de documento para listagem.
    /// </summary>
    public class DocumentListItem
    {
        public string SecureId { get; set; } = "";
        public string Type { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Parâmetros para criação de agendamento.
    /// </summary>
    public class CreateAppointmentParams
    {
        public string LayoutId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string UserPhone { get; set; } = "";
        public Layout? Layout { get; set; }

        public static CreateAppointmentParamsBuilder Builder() => new CreateAppointmentParamsBuilder();
    }

    public class CreateAppointmentParamsBuilder
    {
        private readonly CreateAppointmentParams _params = new CreateAppointmentParams();

        public CreateAppointmentParamsBuilder WithLayoutId(string layoutId)
        {
            _params.LayoutId = layoutId;
            return this;
        }

        public CreateAppointmentParamsBuilder WithUserName(string userName)
        {
            _params.UserName = userName;
            return this;
        }

        public CreateAppointmentParamsBuilder WithUserPhone(string userPhone)
        {
            _params.UserPhone = userPhone;
            return this;
        }

        public CreateAppointmentParamsBuilder WithLayout(Layout layout)
        {
            _params.Layout = layout;
            return this;
        }

        public CreateAppointmentParams Build() => _params;
    }

    /// <summary>
    /// Layout com campos dinâmicos.
    /// </summary>
    public class Layout
    {
        public Dictionary<string, object> Fields { get; } = new Dictionary<string, object>();

        public static LayoutBuilder Builder() => new LayoutBuilder();
    }

    public class LayoutBuilder
    {
        private readonly Layout _layout = new Layout();

        public LayoutBuilder WithField(string key, object value)
        {
            _layout.Fields[key] = value;
            return this;
        }

        public LayoutBuilder WithFieldsJson(string json)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            if (dict != null)
            {
                foreach (var kvp in dict)
                {
                    _layout.Fields[kvp.Key] = kvp.Value;
                }
            }
            return this;
        }

        public Layout Build() => _layout;
    }

    /// <summary>
    /// Exceção específica do SDK.
    /// </summary>
    public class SintezySDKException : Exception
    {
        public int StatusCode { get; }

        public SintezySDKException(string message, int statusCode = 0) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
