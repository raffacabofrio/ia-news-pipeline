# IA News Pipeline

> Pipeline de ponta a ponta que recebe a URL de um artigo público, gera uma nova versão do conteúdo com IA generativa e publica automaticamente em um site WordPress — desafio técnico do Portal Tela, **entregue em 1 dia de um prazo de 3**, com processo de engenharia documentado e auditável neste próprio repositório.

<!-- TODO(S4.2): adicionar badge quando o repositório público estiver estabilizado -->

---

## Sumário executivo

| | Status | Detalhe |
|---|---|---|
| 🟢 **Entregue** | Implementado no repositório | Pipeline completo: serviço .NET (accept assíncrono + fila SQS com DLQ + worker) → plugin WordPress (webhook autenticado, idempotente) → tema Bootstrap (build Vite/SASS, foco em `single.php`). Workflow de CI versionado, testes automatizados do serviço e ambiente local com um comando. |
| 🟡 **Riscos conhecidos** | Documentados | Extração de conteúdo é "boa o suficiente" para páginas de blog/notícia — casos extremos mapeados, não perseguidos. Endpoint de status sem auth (POC). Lista completa em [Fora do escopo](#fora-do-escopo--e-o-caminho-de-produção). |
| 🔴 **Não fazer ainda** | Deliberado | Nada aqui está pronto para produção sem os passos da seção [Caminho de produção](#caminho-de-produção). POC é POC — e dizer isso com clareza faz parte da entrega. |
| ▶️ **Próximo passo como produto** | Visão | De POC a ferramenta editorial: fila de curadoria (rascunho + aprovação humana em vez de publicação direta), métricas de custo por post, A/B de títulos. Estimativa e priorização mediante conversa com a redação. |

## A história deste repositório

Este projeto foi construído em **1 dia útil** — e o *como* é parte da entrega.

O método foi **Spec-Driven Development** com o framework [BMAD](https://docs.bmad-method.org/): antes de qualquer linha de código, três artefatos foram produzidos e congelados, e eles estão versionados aqui:

1. [`prd.md`](_bmad-output/planning-artifacts/prd.md) — requisitos com critérios de aceite **comportamentais** (o quê, nunca o como)
2. [`architecture.md`](_bmad-output/planning-artifacts/architecture.md) — decisões com trade-offs registrados e o **contrato JSON congelado** (§5), que é a única interface entre os componentes
3. [`epics-stories.md`](_bmad-output/planning-artifacts/epics-stories.md) — o trabalho fatiado em stories dimensionadas para sessões isoladas de agentes de IA

A implementação rodou como **pipeline multiagente com contexto isolado**: cada componente (`service/`, `wp-plugin/`, `wp-theme/`) foi construído por um agente que recebeu *apenas* a sua story e o contrato — nenhum agente leu o raciocínio de outro. O **QA cego** faz parte do fechamento planejado (`S4.3`): um revisor isolado valida os critérios de aceite contra a stack rodando, sem acesso à justificativa de quem implementou. Isolamento é o que impede o "confia em mim" de substituir verificação.

O histórico de commits é o registro auditável da execução em um único dia: stories e readiness foram fechadas às `16:08` (`8f9c15c` logo depois materializa o ambiente one-command), as três frentes paralelas começaram por volta de `20:00` (`cde008b`, `69e6c91`, `041a818`) e os últimos fechamentos técnicos desta leva ocorreram entre `21:05` e `21:21` (`2f8db7a`, `e5efed8`, `024960a`).

### Modelos de IA usados estrategicamente

O mesmo princípio de engenharia vale para o custo de IA: **modelo certo na etapa certa**.

| Etapa | Ambiguidade | Modelo | Por quê |
|---|---|---|---|
| Estratégia, PRD, debate de arquitetura | Alta | Modelo de fronteira (Claude Fable) | Onde as decisões erradas custam o dia inteiro; julgamento vale o preço |
| Implementação das stories | ≈ Zero (spec congelada, ACs comportamentais) | Modelo intermediário (Claude Sonnet) | Executar contrato bem escrito não exige fronteira; custa uma fração |
| Geração de conteúdo (o produto em si) | Baixa e repetitiva | Modelo pequeno da OpenAI, tokens limitados | Custo por post é COGS — entra no preço do produto, não pode ser cheque em branco |

O upstream bem feito é o que *permite* baratear o downstream: especificar tem custo baixo e valor alto. Vibe coding faz o caminho inverso — economiza na spec e paga em retrabalho com o modelo caro.

## Arquitetura

```
 POST /api/generate-post ─► API (.NET) ─► fila SQS ─► Worker ─► OpenAI
 GET  /api/jobs/{id}          │        (ElasticMQ     │
                              ▼         local)        ▼ webhook assinado (HMAC)
                        jobs (MySQL)            WordPress ◄─ plugin receptor
                                                    │         (idempotente)
                                                    ▼
                                              tema Bootstrap (single.php)
```

Detalhes, contrato e decisões completas: [`architecture.md`](_bmad-output/planning-artifacts/architecture.md).

## Decisões de stack — e suas defesas

### Por que o serviço é .NET (num ecossistema PHP/WordPress)?

A resposta honesta primeiro: **o prazo era o requisito não-funcional mais duro do projeto, então escolhi a ferramenta em que sou mais rápido e mais seguro.** Autoimpus 1 dia de entrega; cada hora de fluência vale mais que qualquer cortesia de stack. Essa é uma decisão de engenharia de verdade — recursos finitos alocados contra o risco dominante — e eu a defenderia em qualquer revisão de arquitetura.

Mas ela só se sustenta porque o *fit técnico* é real:

- O serviço é um **orquestrador de I/O com requisito duro de resiliência** (fila, retry, backoff, worker de longa duração). O .NET tem primitivas maduras e nativas para exatamente isso: `BackgroundService` para workers hospedados, `HttpClientFactory` com políticas de resiliência, DI e configuração de primeira classe, e SDK oficial da AWS — nada aqui é gambiarra de conforto.
- Performance e footprint de container são excelentes para o perfil da carga (I/O-bound, longa duração).
- Se a resposta fosse "PHP porque o resto é PHP", ela teria que sobreviver à pergunta seguinte: *o PHP era a melhor ferramenta para um worker assíncrono resiliente, ou era a mais próxima?*

O resultado é uma arquitetura **poliglota por necessidade, não por vaidade**: .NET onde a resiliência mora, PHP onde o WordPress mora (plugin — a ferramenta certa é a nativa da plataforma), JavaScript/SASS onde o frontend mora (tema, buildado com Vite). Três linguagens, cada uma escolhida pelo compromisso que resolve. Tecnologia boa é a que resolve o problema — dogma de stack é o que se evita.

### Por que fila SQS (e como ela roda local)?

Resiliência aqui não é promessa de README — é requisito implementado (NFR1 do PRD): requisição aceita **nunca se perde** em falha transitória; retry **nunca duplica** post; falha permanente é **observável** (DLQ + estado do job com motivo).

O desenho é o clássico: accept imediato (`202` + `job_id`) → fila com dead-letter queue → worker. A fila fala a **API do SQS** com endpoint configurável:

- **Local / avaliação:** container [ElasticMQ](https://github.com/softwaremill/elasticmq) no compose — API SQS-compatível, incluindo redrive policy. Zero conta AWS para rodar.
- **Produção:** troca a URL do endpoint no `.env` e é **SQS real, com zero mudança de código**.

Retry vem da mecânica natural do SQS (visibility timeout + `maxReceiveCount: 5` → DLQ). Falhas são **classificadas**: transitórias (site fora do ar, rate limit da OpenAI, WordPress indisponível) re-entregam; permanentes (URL inválida, 404, página que não é artigo) falham rápido com motivo — retry sem classificação não é resiliência, é teimosia.

### Exactly-once: onde mora de verdade

Entrega exactly-once não existe na rede — o que existe é **at-least-once + receptor idempotente**. O serviço garante a entrega (retry) e carrega um `job_id`; o plugin WordPress, dono do estado, verifica `_pipeline_job_id` no post meta antes de inserir. Replay vira no-op com `200 duplicate: true`. Cada responsabilidade no componente que tem a informação para exercê-la.

**Drill de demonstração** (roteiro em [Testando](#testando)): derrube o container do WordPress, dispare uma URL, observe o retry na fila, suba o WordPress — o post é publicado sozinho, exatamente uma vez.

### Segurança dos webhooks: HMAC, não senha viajante

Os dois endpoints são autenticados com **assinatura HMAC-SHA256** (padrão Stripe/GitHub): o segredo nunca trafega — viaja só a assinatura de `timestamp + corpo`. Isso dá três propriedades que um shared secret em header não dá: segredo não exposto em logs/trânsito, **tampering detectado** (um byte alterado invalida a assinatura) e **replay bloqueado** (janela de ±300s). A coleção Postman inclui o script que calcula a assinatura automaticamente.

## Como rodar

```bash
git clone https://github.com/raffacabofrio/ia-news-pipeline.git
cd ia-news-pipeline
cp .env.example .env   # preencha OPENAI_API_KEY e PIPELINE_SHARED_SECRET
docker compose up
```

O compose sobe `mysql`, `elasticmq`, `service`, `worker`, `wordpress` e o bootstrap `wp-init`, **com o WordPress já instalado e plugin/tema ativados** (container one-shot com WP-CLI — sem wizard manual). O arquivo `.env` é recomendado para preencher os dois segredos reais da POC; os demais valores já têm defaults locais no `docker-compose.yml`.

| Serviço | URL |
|---|---|
| WordPress | `http://localhost:8080` |
| API do serviço | `http://localhost:8081` |

## Testando

1. Suba a stack com `docker compose up` e confirme que o bootstrap terminou com `wp-init exited with code 0` (isso é sucesso, não erro).
2. Gere uma assinatura HMAC-SHA256 sobre `timestamp.payload` com o mesmo `PIPELINE_SHARED_SECRET` do `.env`, então dispare `POST /api/generate-post` para `http://localhost:8081/api/generate-post` com body `{"url":"https://example.com/article"}`.
3. Use o `job_id` retornado no `202 Accepted` para consultar `GET http://localhost:8081/api/jobs/{id}` até o estado chegar a `published` ou `failed`.
4. Abra o WordPress em `http://localhost:8080` e confirme o post publicado no tema customizado.
5. Drill de resiliência: `docker compose stop wordpress` → dispare uma URL → observe o job ficar pendente de publicação → `docker compose start wordpress` → o worker deve concluir a entrega sem duplicar o post.

Exemplo mínimo do contrato de entrada:

```http
POST /api/generate-post
Content-Type: application/json
X-Pipeline-Timestamp: <unix-seconds>
X-Pipeline-Signature: sha256=<hmac(timestamp + "." + body)>

{"url":"https://example.com/article"}
```

Exemplo mínimo de resposta de aceite:

```json
{
  "job_id": "6f9e1b9b-7f9e-4c4b-a08d-4e9c9a7e18a0",
  "state": "queued",
  "status_url": "/api/jobs/6f9e1b9b-7f9e-4c4b-a08d-4e9c9a7e18a0"
}
```

A coleção Postman em [`postman/`](postman/) automatiza essa assinatura: importe `ia-news-pipeline.postman_collection.json` e `ia-news-pipeline.postman_environment.json`, preencha `PIPELINE_SHARED_SECRET` e rode as três requisições prontas (happy path, assinatura inválida, polling de job) sem calcular nada à mão.

## Fora do escopo — e o caminho de produção

Cada item abaixo foi **deliberadamente não construído** — complexidade antecipada é dívida disfarçada de diligência. Uma linha sobre como cada um entraria em produção:

| Item | Caminho de produção |
|---|---|
| Múltiplos providers de IA | Interface de geração + implementação por provider; OpenAI escolhida por pragmatismo (chave em mãos, prazo) |
| Painel admin | Fila de curadoria como feature de produto (rascunho + aprovação), não como tela de sistema |
| Cache | Cache de extração por URL (hash) se o volume justificar |
| Rate limiting | API Gateway na frente do serviço; limites por consumer |
| Auth no `GET /jobs/{id}` | Mesmo esquema HMAC dos demais endpoints |

## Caminho de produção

O desenho local → AWS, peça a peça, com custo em mente:

- **Fila:** ElasticMQ → **SQS** (troca de endpoint, zero código). Alarme no CloudWatch sobre profundidade da DLQ.
- **Compute:** containers → **ECS Fargate**. Kubernetes seria complexidade antecipada para esta carga — ECS resolve com uma fração da superfície operacional.
- **IA:** OpenAI → avaliar **Bedrock** (modelos gerenciados na mesma conta, IAM nativo, sem chave de terceiro circulando). Decisão por **custo por post**: tokens médios × preço do modelo × volume editorial — número que deve aparecer num dashboard, não numa fatura surpresa.
- **Segredos:** `.env` → **Secrets Manager/SSM**, rotação automática.
- **Observabilidade:** logs estruturados (todo log carrega `job_id`) → CloudWatch Logs Insights; a consulta "conta a vida de um job" já sai de graça do formato atual.

## Qualidade

- **Testes automatizados do serviço** cobrem contrato de aceite da API (`202`, `401`, `400`, `404`, falha ao enfileirar), assinatura HMAC, montagem do payload do webhook, retry em falha transitória, idempotência (`duplicate: true`) e falhas permanentes do pipeline (`invalid_url`, `source_not_found`, `source_not_article`, `401/422` do receptor).
- **CI versionado** em [`.github/workflows/dotnet-test.yml`](.github/workflows/dotnet-test.yml): `restore`, `build` e `dotnet test` da solution em todo push.
- **QA cego** permanece como fechamento de `S4.3`; o relatório final planejado ficará em [`_bmad-output/implementation-artifacts/qa-report.md`](_bmad-output/implementation-artifacts/).

## Estrutura do repositório

```
service/            # Serviço .NET: API + worker + testes
wp-plugin/          # Plugin WordPress: receptor do webhook
wp-theme/           # Tema Bootstrap/SASS (Vite)
docker/             # elasticmq.conf, wp-init, schema pipeline
.github/            # Workflow de CI para restore/build/test
docs/               # Enunciado do desafio
_bmad-output/       # Artefatos do método: PRD, arquitetura, stories, QA
```

---

*Raffaello Damgaard · [LinkedIn](https://www.linkedin.com/in/raffacabofrio/) · Desafio técnico Portal Tela, julho/2026.*
