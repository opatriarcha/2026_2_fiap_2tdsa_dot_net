# TaskMongoApi

API de gerenciamento de tarefas em **ASP.NET Core Minimal API (.NET 8)** com **MongoDB** rodando em Docker.

## Estrutura

```
taskMongoApi/
├── docker-compose.yml          # MongoDB 7 + Mongo Express
└── TaskMongoApi/
    ├── Configuration/  MongoDbSettings
    ├── Data/           DatabaseSeeder (dados de exemplo na subida)
    ├── Models/         TaskItem, enums, DTOs
    ├── Repositories/   ITaskRepository + MongoTaskRepository (queries, índices, agregações)
    ├── Services/       TaskService (validação e regras)
    ├── Endpoints/      TaskEndpoints (rotas)
    └── Program.cs
```

## Como rodar

```bash
# 1. Subir o MongoDB (Docker Desktop ou `colima start` antes)
docker-compose up -d        # ou: docker compose up -d

# 2. Rodar a API
dotnet run --project TaskMongoApi
```

- Swagger: http://localhost:5024/swagger
- Mongo Express: http://localhost:8081
- Mongo: `mongodb://admin:admin123@localhost:27017/?authSource=admin`, base `TaskDb`

### Seed

Ao subir, a API popula a coleção `tasks` com 12 tarefas de exemplo (`Data/DatabaseSeeder.cs`), cobrindo todos os
status e prioridades, tarefas vencidas, vencendo na semana, sem prazo e sem responsável.

- Roda em qualquer ambiente, **exceto `Testing`** (usado pelos testes de integração).
- Só insere se a coleção estiver vazia, então reiniciar a API não duplica dados.
- Para recriar do zero: `docker-compose down -v && docker-compose up -d` e suba a API de novo.

## Testes de integração

```bash
docker-compose up -d          # os testes usam o MongoDB do compose
dotnet test TaskMongoApi.sln
```

O projeto `TaskMongoApi.IntegrationTests` usa xUnit e `WebApplicationFactory<Program>`. Cada classe de teste
cria um banco próprio (`TaskDb_test_<guid>`) e o apaga no final, então o `TaskDb` não é afetado e as classes rodam
em paralelo. Para usar outro Mongo, defina `MongoDb__ConnectionString`.

| Classe | O que cobre |
|---|---|
| `TaskCrudEndpointsTests` | POST/GET/PUT/PATCH/DELETE, validações e 404 |
| `TaskSearchEndpointsTests` | `/search` (filtros, paginação, ordenação, erros), `/text`, `/overdue`, `/due-soon` |
| `TaskStatsEndpointsTests` | `/stats/summary`, `/by-status`, `/by-assignee`, `/tags` |

Buscas e estatísticas rodam sobre uma massa fixa de 6 tarefas (`SeededTaskApiFactory`), com valores esperados exatos.

## Endpoints

### CRUD
| Método | Rota | Descrição |
|---|---|---|
| GET | `/tasks` | Lista todas |
| GET | `/tasks/{id}` | Busca por id (ObjectId) |
| POST | `/tasks` | Cria |
| PUT | `/tasks/{id}` | Atualiza tudo |
| PATCH | `/tasks/{id}/status` | Altera só o status (preenche `completedAt` ao concluir) |
| DELETE | `/tasks/{id}` | Remove |

### Buscas aprofundadas
| Rota | Descrição |
|---|---|
| `GET /tasks/search` | Filtros combinados: `title` (contém, sem case), `status` e `priority` (múltiplos), `tags` + `matchAllTags`, `assignee`, `dueFrom/dueTo`, `createdFrom/createdTo`; ordenação `sortBy` (`createdAt`, `updatedAt`, `dueDate`, `title`, `status`) + `desc`; paginação `page`/`pageSize` |
| `GET /tasks/text?q=...&limit=20` | Full-text (`$text`) em título (peso 3) e descrição, ordenado por relevância |
| `GET /tasks/overdue` | Vencidas e ainda abertas (Pending/InProgress) |
| `GET /tasks/due-soon?days=7` | Abertas que vencem nos próximos N dias |

### Estatísticas (aggregation pipeline)
| Rota | Descrição |
|---|---|
| `GET /tasks/stats/summary` | Total, vencidas, vencendo na semana, % concluído, por status e prioridade (`$facet`) |
| `GET /tasks/stats/by-status` | Contagem por status (`$group`) |
| `GET /tasks/stats/by-assignee` | Por responsável: total, concluídas, pendentes, em andamento, vencidas (`$group` + `$cond`) |
| `GET /tasks/stats/tags?limit=10` | Tags mais usadas (`$unwind` + `$group`) |

## Exemplos

```bash
curl -X POST http://localhost:5024/tasks -H "Content-Type: application/json" -d '{
  "title": "Estudar aggregation pipeline",
  "description": "Ler docs de $facet e $unwind",
  "priority": "High",
  "tags": ["mongodb", "estudo"],
  "assignee": "ana",
  "dueDate": "2026-10-30T00:00:00Z"
}'

curl "http://localhost:5024/tasks/search?status=Pending&status=InProgress&tags=api&sortBy=dueDate&desc=false"
curl "http://localhost:5024/tasks/text?q=testes"
curl -X PATCH http://localhost:5024/tasks/<id>/status -H "Content-Type: application/json" -d '{"status":"Done"}'
```

Valores aceitos — `status`: `Pending`, `InProgress`, `Done`, `Cancelled`; `priority`: `Low`, `Medium`, `High`, `Critical`.
