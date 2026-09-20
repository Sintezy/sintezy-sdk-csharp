# Sintezy SDK C#

SDK C# oficial para integração com a API Sintezy.

## Requisitos

- .NET 6.0+

## Instalação

### .NET CLI

```bash
dotnet add package Sintezy.SDK
```

### NuGet Package Manager

```bash
Install-Package Sintezy.SDK
```

### PackageReference

```xml
<PackageReference Include="Sintezy.SDK" Version="0.2.0" />
```

## Uso Rápido

```csharp
using Sintezy.SDK;

using var sdk = new SintezySDK(
    clientId: "seu-client-id",
    clientSecret: "seu-client-secret"
);

// 1. O layout descreve a ESTRUTURA DA ANAMNESE: um campo por seção do seu
//    prontuário. O segundo argumento é a instrução para a IA preencher aquela
//    seção — não é o valor do campo.
var layout = Layout.Builder()
    .WithField("Queixa Principal", "inserir aqui a queixa principal do paciente")
    .WithField("História da Doença Atual", "inserir aqui a evolução dos sintomas")
    .WithField("Exame Físico", "inserir aqui os achados e sinais vitais")
    .WithField("Diagnóstico", "inserir aqui as hipóteses diagnósticas")
    .WithField("Conduta", "inserir aqui prescrições, exames e orientações")
    .Build();

// 2. Cria a consulta
var parameters = CreateAppointmentParams.Builder()
    .WithUserEmail("medico@clinica.com")
    .WithUserName("Dr. João Silva")
    .WithLayout(layout)
    .Build();

var appointment = await sdk.CreateAppointmentAsync(parameters);

// 3. Abra a URL do portal para o médico gravar a consulta
Console.WriteLine(appointment.PortalUrl);

// 4. Depois de finalizada, leia a anamnese e gere outros documentos
var anamnese = await sdk.GetDocumentAsync(appointment.SecureId, "document");
var receita = await sdk.GenerateDocumentAsync(appointment.SecureId, "prescription");
```

## API Reference

### Construtor

```csharp
using var sdk = new SintezySDK(
    clientId: "seu-client-id",
    clientSecret: "seu-client-secret",
    baseUrl: "https://api.sintezy.com" // opcional
);
```

A autenticação é automática: todo método garante um token válido antes da
chamada.

### Consultas

| Método | Descrição |
|--------|-----------|
| `CreateAppointmentAsync(parameters)` | Cria a consulta e devolve a URL do portal |
| `GetAppointmentAsync(secureId)` | Busca uma consulta |
| `DeleteAppointmentAsync(secureId)` | Exclui uma consulta (soft delete) |
| `GetTranscriptionAsync(secureId)` | Transcrição da consulta, após a gravação |
| `GetSubscriptionStatusAsync(email)` | Status da assinatura (API keys reseller) |

**Retorno de `Appointment`:**

```csharp
appointment.SecureId    // ID da consulta, usado em todos os outros métodos
appointment.PortalUrl   // URL do portal de gravação
appointment.Status      // INICIADA, FINALIZADA, ...
appointment.Title       // Título, quando houver
appointment.CreatedAt
```

### Documentos

| Método | Descrição |
|--------|-----------|
| `GenerateDocumentAsync(secureId, documentType)` | Gera um documento do catálogo |
| `GenerateDocumentAsync(secureId, parameters)` | Gera com o seu prompt |
| `GetDocumentAsync(secureId, documentType)` | Busca um documento já gerado |
| `ListDocumentsAsync(secureId)` | Lista os documentos e quais já existem |

**Retorno de `Document`:**

```csharp
document.Type       // O tipo com que ficou gravado — use no GetDocumentAsync
document.Content    // JsonElement: { document: { title, content } }
document.CreatedAt
```

## Tipos de Documento

| Tipo | Descrição |
|------|-----------|
| `document` | Prontuário/Documento principal (gerado ao finalizar a consulta) |
| `anamnese_summary` | Resumo de anamnese |
| `clinic_summary` | Resumo clínico |
| `referral` | Encaminhamento |
| `exames_call` | Solicitação de exames |
| `prescription` | Receita médica |
| `certificate` | Atestado médico |
| `inss_report` | Laudo INSS |

## Documentos com o seu prompt

Além dos tipos acima, você pode escrever o próprio prompt do documento, com os
mesmos dois textos que o médico preenche no portal da Sintezy:

- **`Contextualization`**: objetivo do documento, tom esperado, regras e
  informações obrigatórias.
- **`Format`**: como o texto deve aparecer, com seções, quebras de linha,
  título e assinatura.

Os dois são sempre obrigatórios juntos. Nada do que você envia fica cadastrado
na Sintezy: reenvie o prompt a cada geração.

```csharp
// Tipo do catálogo com o SEU prompt.
// Continua sendo clinic_summary e é buscado por esse tipo.
await sdk.GenerateDocumentAsync(secureId, GenerateDocumentParams.Builder()
    .WithDocumentType("clinic_summary")
    .WithPrompt(
        "Explique a consulta ao paciente em linguagem simples...",
        "RESUMO DA CONSULTA\n\nOlá, [NOME]...")
    .Build());

// Documento que não é de nenhum tipo do catálogo: o DocumentType vira o NOME
// que você dá a ele, e a busca depois é por esse nome.
await sdk.GenerateDocumentAsync(secureId, GenerateDocumentParams.Builder()
    .WithDocumentType("carta_alta")
    .WithPrompt("...", "...")
    .Build());

var carta = await sdk.GetDocumentAsync(secureId, "carta_alta");

// Sem DocumentType, o documento é gravado com o nome `custom`.
await sdk.GenerateDocumentAsync(secureId, GenerateDocumentParams.Builder()
    .WithPrompt("...", "...")
    .Build());
```

Regras que valem a pena saber:

- Um documento por nome, por consulta. Regerar com o mesmo `DocumentType`
  substitui o anterior; nomes diferentes convivem.
- O nome aceita `a-z`, `0-9`, `_` e `-`, até 64 caracteres.
- A anamnese principal (`document`) segue o layout da consulta e não aceita
  prompt próprio.
- A consulta precisa estar finalizada.
- Use sempre `document.Type` da resposta para buscar depois.

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
```

A SDK também valida antes de chamar a API: layout vazio, prompt pela metade ou
um nome fora do catálogo sem prompt lançam `SintezySDKException` sem gastar
uma requisição.

## Exemplo com ASP.NET Core

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
            .WithField("Queixa Principal", "inserir aqui a queixa principal")
            .WithField("Conduta", "inserir aqui prescrições e orientações")
            .Build();

        var parameters = CreateAppointmentParams.Builder()
            .WithUserEmail(request.DoctorEmail)
            .WithUserName(request.DoctorName)
            .WithLayout(layout)
            // Histórico do paciente: a IA usa ao gerar os documentos
            .WithContext(request.PatientHistory)
            .Build();

        var appointment = await _sdk.CreateAppointmentAsync(parameters);

        return Ok(new { appointment.SecureId, appointment.PortalUrl });
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
