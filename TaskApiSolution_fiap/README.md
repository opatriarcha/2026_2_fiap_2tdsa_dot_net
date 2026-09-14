# TaskApi — Minimal API com testes unitários e de integração

API de gerenciamento de tarefas construída com **Minimal APIs** do ASP.NET Core (.NET 8).

## Estrutura

```
TaskApiSolution/
├── TaskApi/                     # API (projeto principal)
│   ├── Models/TaskItem.cs       # Entidade + DTOs
│   ├── Repositories/            # Abstração de persistência (em memória)
│   ├── Services/                # Regras de negócio (validações)
│   └── Program.cs               # Endpoints minimal API
├── TaskApi.UnitTests/           # xUnit + Moq — testa Service e Repository isoladamente
└── TaskApi.IntegrationTests/    # xUnit + WebApplicationFactory — testa a API ponta a ponta via HTTP
```

## Por que essa separação em camadas?

- **Repository**: guarda os dados (aqui, em memória via `ConcurrentDictionary`).
- **Service**: contém as regras de negócio (ex.: título obrigatório, tamanho máximo) — é o que os **testes unitários** exercitam, usando um repositório *mockado* (Moq), sem tocar em HTTP ou I/O real.
- **Endpoints (Program.cs)**: apenas traduzem requisições HTTP em chamadas ao Service — é o que os **testes de integração** exercitam, subindo a aplicação inteira em memória com `WebApplicationFactory<Program>` e fazendo requisições HTTP reais contra ela.

## Endpoints

| Método | Rota          | Descrição                     |
|--------|---------------|--------------------------------|
| GET    | /tasks        | Lista todas as tarefas         |
| GET    | /tasks/{id}   | Obtém uma tarefa por id        |
| POST   | /tasks        | Cria uma tarefa                |
| PUT    | /tasks/{id}   | Atualiza uma tarefa            |
| DELETE | /tasks/{id}   | Remove uma tarefa              |
| GET    | /health       | Health check                   |

## Como rodar

Pré-requisito: [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
# Restaurar dependências (baixa os pacotes NuGet)
dotnet restore

# Rodar a API
dotnet run --project TaskApi
# Swagger disponível em https://localhost:xxxx/swagger (ambiente Development)

# Rodar TODOS os testes (unitários + integração)
dotnet test

# Rodar só os unitários
dotnet test TaskApi.UnitTests

# Rodar só os de integração
dotnet test TaskApi.IntegrationTests

# Rodar com relatório de cobertura (opcional)
dotnet test --collect:"XPlat Code Coverage"
```

## Observações

- O repositório é registrado como **Singleton**, então nos testes de integração cada classe de teste cria sua própria `WebApplicationFactory` (no construtor, descartada no `Dispose`) para não vazar estado (tarefas criadas) entre os testes.
- Não usei banco de dados real de propósito, para manter o exemplo focado na estrutura de testes. Trocar `InMemoryTaskRepository` por uma implementação com EF Core/Postgres, por exemplo, não exigiria mudar nenhum teste do `Service` (eles dependem só da interface `ITaskRepository`).
