# Sintezy SDK C#

SDK C# oficial para integração com a API Sintezy.

## Requisitos

- .NET 6.0+

## Instalação

### NuGet Package Manager

```bash
Install-Package Sintezy.SDK
```

### .NET CLI

```bash
dotnet add package Sintezy.SDK
```

### PackageReference

```xml
<PackageReference Include="Sintezy.SDK" Version="0.1.0" />
```

## Uso Rápido

```csharp
using Sintezy.SDK;

// Inicializa o SDK
using var sdk = new SintezySDK(
    clientId: "seu-client-id",
    clientSecret: "seu-client-secret"
);

// Cria o layout
var layout = Layout.Builder()
    .WithField("patientName", "João Silva")
    .WithField("cpf", "123.456.789-00")
    .WithField("birthDate", "01/01/1990")
    .WithField("phone", "(11) 99999-9999")
    .WithField("symptoms", "Dor de cabeça e febre")
    .Build();

// Cria os parâmetros do agendamento
var parameters = CreateAppointmentParams.Builder()
    .WithLayoutId("seu-layout-id")
    .WithUserName("João Silva")
    .WithUserPhone("+5511999999999")
    .WithLayout(layout)
    .Build();

// Cria o agendamento
var appointment = await sdk.CreateAppointmentAsync(parameters);

Console.WriteLine($"Agendamento criado!");
Console.WriteLine($"URL do Portal: {appointment.PortalUrl}");
Console.WriteLine($"Secure ID: {appointment.SecureId}");
```

## API Reference

### SintezySDK

```csharp
// Construtor
using var sdk = new SintezySDK(
    clientId: "seu-client-id",
    clientSecret: "seu-client-secret",
    baseUrl: "https://api.sintezy.com" // opcional
);
```

### Métodos Assíncronos

#### `CreateAppointmentAsync(CreateAppointmentParams parameters)`

Cria um novo agendamento.

```csharp
var appointment = await sdk.CreateAppointmentAsync(parameters);
```

**Retorno (Appointment):**
```csharp
appointment.SecureId      // ID seguro do agendamento
appointment.PortalUrl     // URL do portal do paciente
appointment.Status        // Status atual
appointment.UserName      // Nome do usuário
appointment.UserPhone     // Telefone do usuário
appointment.CreatedAt     // Data de criação
```

#### `GetAppointmentAsync(string secureId)`

Busca um agendamento pelo ID.

```csharp
var appointment = await sdk.GetAppointmentAsync("secure-id");
```

#### `ListAppointmentsAsync()`

Lista todos os agendamentos.

```csharp
var appointments = await sdk.ListAppointmentsAsync();
```

#### `GenerateDocumentAsync(string appointmentSecureId, string documentType)`

Gera um documento para o agendamento.

```csharp
var document = await sdk.GenerateDocumentAsync("secure-id", "prescription");
```

#### `ListDocumentsAsync(string appointmentSecureId)`

Lista documentos de um agendamento.

```csharp
var documents = await sdk.ListDocumentsAsync("secure-id");
```

### CreateAppointmentParams (Builder Pattern)

```csharp
var parameters = CreateAppointmentParams.Builder()
    .WithLayoutId("layout-id")           // obrigatório
    .WithUserName("Nome do Usuário")     // obrigatório
    .WithUserPhone("+5511999999999")     // obrigatório
    .WithLayout(layout)                  // obrigatório
    .Build();
```

### Layout (Builder Pattern)

```csharp
var layout = Layout.Builder()
    .WithField("campo1", "valor1")
    .WithField("campo2", "valor2")
    .WithFieldsJson("{\"campo1\": \"valor1\"}") // alternativa com JSON
    .Build();
```

## Tratamento de Erros

```csharp
try
{
    var appointment = await sdk.CreateAppointmentAsync(parameters);
}
catch (SintezySDKException ex)
{
    Console.WriteLine($"Erro da API: {ex.Message}");
    Console.WriteLine($"Status Code: {ex.StatusCode}");
}
catch (Exception ex)
{
    Console.WriteLine($"Erro inesperado: {ex.Message}");
}
```

## Exemplo Completo com ASP.NET Core

```csharp
using Microsoft.AspNetCore.Mvc;
using Sintezy.SDK;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly SintezySDK _sdk;

    public AppointmentsController(IConfiguration config)
    {
        _sdk = new SintezySDK(
            config["Sintezy:ClientId"]!,
            config["Sintezy:ClientSecret"]!
        );
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest request)
    {
        var layout = Layout.Builder()
            .WithField("patientName", request.PatientName)
            .WithField("symptoms", request.Symptoms)
            .Build();

        var parameters = CreateAppointmentParams.Builder()
            .WithLayoutId(request.LayoutId)
            .WithUserName(request.PatientName)
            .WithUserPhone(request.Phone)
            .WithLayout(layout)
            .Build();

        var appointment = await _sdk.CreateAppointmentAsync(parameters);

        return Ok(new
        {
            appointment.SecureId,
            appointment.PortalUrl
        });
    }
}
```

## Configuração no appsettings.json

```json
{
  "Sintezy": {
    "ClientId": "seu-client-id",
    "ClientSecret": "seu-client-secret"
  }
}
```

## Licença

MIT License - veja o arquivo [LICENSE](LICENSE) para detalhes.
