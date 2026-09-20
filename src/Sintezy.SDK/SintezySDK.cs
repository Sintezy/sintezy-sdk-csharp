using System;
using System.Collections.Generic;
using System.Linq;
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
    ///
    /// <example>
    /// <code>
    /// using var sdk = new SintezySDK("client-id", "client-secret");
    ///
    /// var appointment = await sdk.CreateAppointmentAsync(
    ///     CreateAppointmentParams.Builder()
    ///         .WithUserEmail("medico@clinica.com")
    ///         .WithUserName("Dr. João Silva")
    ///         .WithLayout(Layout.Builder()
    ///             .WithField("Queixa Principal", "inserir aqui...")
    ///             .WithField("Conduta", "inserir aqui...")
    ///             .Build())
    ///         .Build());
    ///
    /// Console.WriteLine(appointment.PortalUrl);
    /// </code>
    /// </example>
    /// </summary>
    public class SintezySDK : IDisposable
    {
        /// <summary>
        /// Tipos servidos pelos modelos padrão da Sintezy. Qualquer outro valor
        /// em documentType é o nome de um documento seu, e exige o prompt.
        /// </summary>
        public static readonly string[] CatalogDocumentTypes =
        {
            "document",
            "anamnese_summary",
            "clinic_summary",
            "referral",
            "exames_call",
            "prescription",
            "certificate",
            "inss_report"
        };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _baseUrl;
        private readonly HttpClient _httpClient;
        private AuthToken? _token;

        public SintezySDK(string clientId, string clientSecret, string? baseUrl = null)
        {
            _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
            _baseUrl = (baseUrl ?? "https://api.sintezy.com").TrimEnd('/');
            _httpClient = new HttpClient();
        }

        // ============================================================
        // AUTENTICAÇÃO
        // ============================================================

        /// <summary>Autentica via OAuth 2.0 Client Credentials.</summary>
        public async Task AuthenticateAsync()
        {
            var body = new Dictionary<string, object>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret
            };

            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/oauth/token", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw ToException(responseContent, (int)response.StatusCode, "Falha na autenticação");
            }

            _token = JsonSerializer.Deserialize<AuthToken>(responseContent, JsonOptions);
        }

        /// <summary>Autentica se ainda não há token válido.</summary>
        public async Task EnsureAuthenticatedAsync()
        {
            if (_token == null || _token.IsExpired)
            {
                await AuthenticateAsync();
            }
        }

        public bool IsAuthenticated => _token != null && !_token.IsExpired;

        // ============================================================
        // CONSULTAS
        // ============================================================

        /// <summary>Cria a consulta e devolve a URL do portal de gravação.</summary>
        public async Task<Appointment> CreateAppointmentAsync(CreateAppointmentParams parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            if (string.IsNullOrWhiteSpace(parameters.UserEmail))
                throw new SintezySDKException("userEmail é obrigatório");
            if (string.IsNullOrWhiteSpace(parameters.UserName))
                throw new SintezySDKException("userName é obrigatório");
            if (parameters.Layout == null || parameters.Layout.Fields.Count == 0)
                throw new SintezySDKException("layout.fields é obrigatório: informe ao menos um campo da anamnese");

            var body = new Dictionary<string, object?>
            {
                ["userEmail"] = parameters.UserEmail,
                ["userName"] = parameters.UserName,
                ["layout"] = new Dictionary<string, object> { ["fields"] = parameters.Layout.Fields }
            };
            AddIfPresent(body, "userPhone", parameters.UserPhone);
            AddIfPresent(body, "userOccupation", parameters.UserOccupation);
            AddIfPresent(body, "userOccupationDoc", parameters.UserOccupationDoc);
            AddIfPresent(body, "title", parameters.Title);
            AddIfPresent(body, "type", parameters.Type);
            AddIfPresent(body, "modality", parameters.Modality);
            AddIfPresent(body, "notes", parameters.Notes);
            AddIfPresent(body, "context", parameters.Context);
            AddIfPresent(body, "redirectUrl", parameters.RedirectUrl);
            if (parameters.Metadata != null) body["metadata"] = parameters.Metadata;

            return await RequestAsync<Appointment>(HttpMethod.Post, "/sdk/appointments", body);
        }

        /// <summary>Busca uma consulta pelo secureId.</summary>
        public async Task<Appointment> GetAppointmentAsync(string appointmentSecureId)
        {
            return await RequestAsync<Appointment>(
                HttpMethod.Get, $"/sdk/appointments/{Uri.EscapeDataString(appointmentSecureId)}");
        }

        /// <summary>Exclui a consulta (soft delete).</summary>
        public async Task<DeleteResult> DeleteAppointmentAsync(string appointmentSecureId)
        {
            return await RequestAsync<DeleteResult>(
                HttpMethod.Delete, $"/sdk/appointments/{Uri.EscapeDataString(appointmentSecureId)}");
        }

        /// <summary>Transcrição da consulta, quando a gravação já terminou.</summary>
        public async Task<TranscriptionResult> GetTranscriptionAsync(string appointmentSecureId)
        {
            return await RequestAsync<TranscriptionResult>(
                HttpMethod.Get, $"/sdk/appointments/{Uri.EscapeDataString(appointmentSecureId)}/transcription");
        }

        /// <summary>
        /// Status da assinatura de um médico. Disponível apenas para API Keys
        /// do tipo unauthenticated (reseller).
        /// </summary>
        public async Task<SubscriptionStatus> GetSubscriptionStatusAsync(string email)
        {
            return await RequestAsync<SubscriptionStatus>(
                HttpMethod.Get, $"/sdk/subscription-status?email={Uri.EscapeDataString(email)}");
        }

        // ============================================================
        // DOCUMENTOS
        // ============================================================

        /// <summary>
        /// Gera um documento de um tipo do catálogo, com o prompt padrão da
        /// Sintezy. A consulta precisa estar finalizada.
        /// </summary>
        public Task<Document> GenerateDocumentAsync(string appointmentSecureId, string documentType)
        {
            return GenerateDocumentAsync(
                appointmentSecureId,
                new GenerateDocumentParams { DocumentType = documentType });
        }

        /// <summary>
        /// Gera um documento da consulta, que precisa estar finalizada.
        ///
        /// Combinações aceitas:
        ///  - DocumentType do catálogo, sozinho: usa o prompt padrão.
        ///  - DocumentType do catálogo + prompt: mesmo tipo, com o SEU prompt.
        ///    Continua sendo `clinic_summary` e é buscado por esse tipo.
        ///  - DocumentType com nome próprio + prompt: documento fora do catálogo.
        ///  - Só o prompt: idem, gravado com o nome `custom`.
        /// </summary>
        public async Task<Document> GenerateDocumentAsync(
            string appointmentSecureId,
            GenerateDocumentParams parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            var hasPrompt = parameters.Contextualization != null || parameters.Format != null;
            if (hasPrompt && (string.IsNullOrWhiteSpace(parameters.Contextualization) ||
                              string.IsNullOrWhiteSpace(parameters.Format)))
            {
                throw new SintezySDKException("contextualization e format são obrigatórios juntos");
            }
            if (string.IsNullOrWhiteSpace(parameters.DocumentType) && !hasPrompt)
            {
                throw new SintezySDKException("informe um documentType ou o par contextualization + format");
            }
            if (!string.IsNullOrWhiteSpace(parameters.DocumentType) &&
                !CatalogDocumentTypes.Contains(parameters.DocumentType) && !hasPrompt)
            {
                throw new SintezySDKException(
                    $"\"{parameters.DocumentType}\" não é um tipo do catálogo ({string.Join(", ", CatalogDocumentTypes)}), " +
                    "então é o nome do seu documento e exige contextualization + format");
            }

            var body = new Dictionary<string, object?>();
            AddIfPresent(body, "documentType", parameters.DocumentType);
            if (hasPrompt)
            {
                body["contextualization"] = parameters.Contextualization;
                body["format"] = parameters.Format;
            }

            return await RequestAsync<Document>(
                HttpMethod.Post,
                $"/sdk/appointments/{Uri.EscapeDataString(appointmentSecureId)}/documents",
                body);
        }

        /// <summary>
        /// Busca um documento já gerado.
        /// </summary>
        /// <param name="documentType">
        /// Tipo do catálogo, ou o nome que você usou ao gerar (`custom` quando
        /// você não informou nenhum).
        /// </param>
        public async Task<Document> GetDocumentAsync(string appointmentSecureId, string documentType)
        {
            return await RequestAsync<Document>(
                HttpMethod.Get,
                $"/sdk/appointments/{Uri.EscapeDataString(appointmentSecureId)}/documents/{Uri.EscapeDataString(documentType)}");
        }

        /// <summary>Lista os documentos da consulta e quais já foram gerados.</summary>
        public async Task<List<DocumentListItem>> ListDocumentsAsync(string appointmentSecureId)
        {
            return await RequestAsync<List<DocumentListItem>>(
                HttpMethod.Get, $"/sdk/appointments/{Uri.EscapeDataString(appointmentSecureId)}/documents");
        }

        // ============================================================
        // HELPERS INTERNOS
        // ============================================================

        private static void AddIfPresent(IDictionary<string, object?> body, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) body[key] = value;
        }

        /// <summary>
        /// Traduz o corpo de erro da API. `message` pode vir string ou array
        /// (erros de validação do Nest).
        /// </summary>
        private static SintezySDKException ToException(string responseContent, int statusCode, string fallback)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseContent);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var name in new[] { "message", "error" })
                    {
                        if (!doc.RootElement.TryGetProperty(name, out var prop)) continue;
                        if (prop.ValueKind == JsonValueKind.String)
                        {
                            return new SintezySDKException(prop.GetString() ?? fallback, statusCode);
                        }
                        if (prop.ValueKind == JsonValueKind.Array)
                        {
                            var parts = prop.EnumerateArray().Select(e => e.ToString());
                            return new SintezySDKException(string.Join("; ", parts), statusCode);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Resposta não-JSON: cai no fallback.
            }

            return new SintezySDKException($"{fallback}: {responseContent}", statusCode);
        }

        private async Task<T> RequestAsync<T>(HttpMethod method, string path, object? body = null)
        {
            await EnsureAuthenticatedAsync();

            using var request = new HttpRequestMessage(method, $"{_baseUrl}{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token!.AccessToken);
            if (body != null)
            {
                // Serializa pelo tipo em TEMPO DE EXECUÇÃO. Com o genérico, o
                // System.Text.Json usaria o tipo declarado (object) e enviaria
                // um corpo vazio.
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body, body.GetType(), JsonOptions),
                    Encoding.UTF8,
                    "application/json");
            }

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw ToException(responseContent, (int)response.StatusCode, $"Erro em {method} {path}");
            }

            return JsonSerializer.Deserialize<T>(responseContent, JsonOptions)!;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>Token de autenticação.</summary>
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

    /// <summary>Dados da consulta.</summary>
    public class Appointment
    {
        public string SecureId { get; set; } = "";
        public string Status { get; set; } = "";
        public string? Title { get; set; }
        /// <summary>URL do portal de gravação, para abrir em popup ou iframe.</summary>
        public string PortalUrl { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Documento gerado.</summary>
    public class Document
    {
        public string SecureId { get; set; } = "";
        /// <summary>O tipo com que ficou gravado — use-o no GetDocumentAsync.</summary>
        public string Type { get; set; } = "";
        public JsonElement Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>Item da listagem de documentos.</summary>
    public class DocumentListItem
    {
        public string Type { get; set; } = "";
        public bool Exists { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    /// <summary>Resultado da exclusão de uma consulta.</summary>
    public class DeleteResult
    {
        public string Message { get; set; } = "";
        public bool Deleted { get; set; }
    }

    /// <summary>Transcrição de uma consulta.</summary>
    public class TranscriptionResult
    {
        public string SecureId { get; set; } = "";
        public string? Transcription { get; set; }
        public int? RecordedTimeSeconds { get; set; }
        public string Status { get; set; } = "";
    }

    /// <summary>Status da assinatura de um email.</summary>
    public class SubscriptionStatus
    {
        public string Email { get; set; } = "";
        public bool HasSubscription { get; set; }
        public string? Status { get; set; }
        public string? PlanType { get; set; }
        public DateTime? EndDate { get; set; }
        public string? CheckoutUrl { get; set; }
    }

    /// <summary>Corpo de GenerateDocumentAsync.</summary>
    public class GenerateDocumentParams
    {
        /// <summary>Tipo do catálogo, ou o nome do seu documento.</summary>
        public string? DocumentType { get; set; }
        /// <summary>Objetivo, tom, regras e informações obrigatórias.</summary>
        public string? Contextualization { get; set; }
        /// <summary>Como o texto deve aparecer: seções, quebras, assinatura.</summary>
        public string? Format { get; set; }

        public static GenerateDocumentParamsBuilder Builder() => new GenerateDocumentParamsBuilder();
    }

    public class GenerateDocumentParamsBuilder
    {
        private readonly GenerateDocumentParams _params = new GenerateDocumentParams();

        public GenerateDocumentParamsBuilder WithDocumentType(string documentType)
        {
            _params.DocumentType = documentType;
            return this;
        }

        public GenerateDocumentParamsBuilder WithPrompt(string contextualization, string format)
        {
            _params.Contextualization = contextualization;
            _params.Format = format;
            return this;
        }

        public GenerateDocumentParams Build() => _params;
    }

    /// <summary>Parâmetros para criação de consulta.</summary>
    public class CreateAppointmentParams
    {
        public string UserEmail { get; set; } = "";
        public string UserName { get; set; } = "";
        public Layout? Layout { get; set; }
        public string? UserPhone { get; set; }
        public string? UserOccupation { get; set; }
        public string? UserOccupationDoc { get; set; }
        public string? Title { get; set; }
        /// <summary>NORMAL ou RETORNO.</summary>
        public string? Type { get; set; }
        /// <summary>PRESENCIAL ou ONLINE.</summary>
        public string? Modality { get; set; }
        /// <summary>Observações pré-consulta.</summary>
        public string? Notes { get; set; }
        /// <summary>Histórico do paciente — a IA usa na geração.</summary>
        public string? Context { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        /// <summary>Para onde o portal redireciona ao finalizar.</summary>
        public string? RedirectUrl { get; set; }

        public static CreateAppointmentParamsBuilder Builder() => new CreateAppointmentParamsBuilder();
    }

    public class CreateAppointmentParamsBuilder
    {
        private readonly CreateAppointmentParams _params = new CreateAppointmentParams();

        public CreateAppointmentParamsBuilder WithUserEmail(string userEmail)
        {
            _params.UserEmail = userEmail;
            return this;
        }

        public CreateAppointmentParamsBuilder WithUserName(string userName)
        {
            _params.UserName = userName;
            return this;
        }

        public CreateAppointmentParamsBuilder WithLayout(Layout layout)
        {
            _params.Layout = layout;
            return this;
        }

        public CreateAppointmentParamsBuilder WithUserPhone(string userPhone)
        {
            _params.UserPhone = userPhone;
            return this;
        }

        public CreateAppointmentParamsBuilder WithUserOccupation(string userOccupation)
        {
            _params.UserOccupation = userOccupation;
            return this;
        }

        public CreateAppointmentParamsBuilder WithUserOccupationDoc(string userOccupationDoc)
        {
            _params.UserOccupationDoc = userOccupationDoc;
            return this;
        }

        public CreateAppointmentParamsBuilder WithTitle(string title)
        {
            _params.Title = title;
            return this;
        }

        public CreateAppointmentParamsBuilder WithType(string type)
        {
            _params.Type = type;
            return this;
        }

        public CreateAppointmentParamsBuilder WithModality(string modality)
        {
            _params.Modality = modality;
            return this;
        }

        public CreateAppointmentParamsBuilder WithNotes(string notes)
        {
            _params.Notes = notes;
            return this;
        }

        public CreateAppointmentParamsBuilder WithContext(string context)
        {
            _params.Context = context;
            return this;
        }

        public CreateAppointmentParamsBuilder WithMetadata(Dictionary<string, object> metadata)
        {
            _params.Metadata = metadata;
            return this;
        }

        public CreateAppointmentParamsBuilder WithRedirectUrl(string redirectUrl)
        {
            _params.RedirectUrl = redirectUrl;
            return this;
        }

        public CreateAppointmentParams Build() => _params;
    }

    /// <summary>Campo do layout da anamnese: o Content é a instrução para a IA.</summary>
    public class LayoutField
    {
        public string Name { get; set; } = "";
        public string? Content { get; set; }
        public int? Position { get; set; }
    }

    /// <summary>Estrutura da anamnese: um campo por seção do prontuário.</summary>
    public class Layout
    {
        public List<LayoutField> Fields { get; } = new List<LayoutField>();

        public static LayoutBuilder Builder() => new LayoutBuilder();
    }

    public class LayoutBuilder
    {
        private readonly Layout _layout = new Layout();

        /// <summary>Adiciona um campo; a posição segue a ordem de inclusão.</summary>
        public LayoutBuilder WithField(string name, string? content = null, int? position = null)
        {
            _layout.Fields.Add(new LayoutField
            {
                Name = name,
                Content = content,
                Position = position ?? _layout.Fields.Count
            });
            return this;
        }

        public Layout Build() => _layout;
    }

    /// <summary>Exceção específica do SDK.</summary>
    public class SintezySDKException : Exception
    {
        public int StatusCode { get; }

        public SintezySDKException(string message, int statusCode = 0) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
