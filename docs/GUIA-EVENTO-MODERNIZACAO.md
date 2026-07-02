# 🚀 Copa do Mundo Azure — Guia do Evento TFTEC (FIFA 2026 Tickets — modernização VM → PaaS)

> ⚽ **Segundo tempo!** No guia anterior ([`GUIA-EVENTO-VMS.md`](GUIA-EVENTO-VMS.md)) você subiu a aplicação **FIFA 2026 Tickets** em **3 Máquinas Virtuais** — com IIS, iisnode, SQL Server e proxy reverso feitos com as suas mãos. Aqui você vai pegar **essa mesma aplicação** e **modernizá-la** para uma arquitetura **PaaS** no Azure: **Web Apps** no lugar do IIS e **Azure SQL Database** no lugar do SQL Server em VM.
>
> 🥅 **Para todos os níveis.** Cada passo é explicado, com o **caminho visual pelo Portal do Azure** sempre que possível. A meta não é só "fazer funcionar" — é **entender o que muda** quando você troca "infra que você opera" por "serviço gerenciado".

> 🚧 **Documento vivo.** Itens marcados com _⚠️ a confirmar_ serão fixados conforme o evento se aproxima. A arquitetura, as ferramentas e os passos já valem.

> 🧩 **Pré-requisito deste guia:** ter concluído o [`GUIA-EVENTO-VMS.md`](GUIA-EVENTO-VMS.md) — a aplicação **precisa estar rodando nas 3 VMs** (estado pós-Fase 8: `vm-fend` pública como jump host, `vm-bend` e `vm-data` privadas). É **dessa** topologia que vamos partir.

> 🎯 **A jornada é o produto.** A migração é feita **uma camada por vez** (backend → frontend → banco), no padrão **blue/green**: o ambiente novo nasce **ao lado** do antigo, você testa, e só então **vira a chave**. Em cada fase o app continua no ar — você nunca fica sem um ambiente funcional.

---

## 📋 Índice

1. [Sobre esta etapa](#-1-sobre-esta-etapa)
2. [Objetivos do evento](#-2-objetivos-do-evento)
3. [Ferramentas e serviços que vamos usar](#-3-ferramentas-e-serviços-que-vamos-usar)
4. [Arquitetura: antes e depois](#-4-arquitetura-antes-e-depois)
5. [A jornada do aluno](#-5-a-jornada-do-aluno)
   - [Fase 0 — Pré-requisitos](#fase-0--pré-requisitos)
   - [Fase 1 — Desenho do estado-alvo + taxonomia PaaS](#fase-1--desenho-do-estado-alvo--taxonomia-paas)
   - [Fase 2 — Assessment sem appliance (o "porquê")](#fase-2--assessment-sem-appliance-o-porquê)
   - [Fase 3 — Migrar o Backend (API) → Web App](#fase-3--migrar-o-backend-api--web-app)
   - [Fase 4 — Migrar o Frontend → Web App](#fase-4--migrar-o-frontend--web-app)
   - [Fase 5 — Migrar o Banco → Azure SQL Database](#fase-5--migrar-o-banco--azure-sql-database)
   - [Fase 6 — Smoke test ponta a ponta (100% PaaS)](#fase-6--smoke-test-ponta-a-ponta-100-paas)
   - [Fase 7 — Decomissionar as VMs + comparação VM × PaaS](#fase-7--decomissionar-as-vms--comparação-vm--paas)
   - [Fase 8 — Rede privada: Private Endpoints + VNet Integration](#fase-8--rede-privada-private-endpoints--vnet-integration)
   - [Fase 9 — Troubleshooting](#fase-9--troubleshooting)
6. [Tabela de variáveis e segredos](#-6-tabela-de-variáveis-e-segredos)
7. [Evolução (o "próximo nível" do PaaS)](#️-7-evolução-o-próximo-nível-do-paas)

---

## 🚀 1. Sobre esta etapa

No guia das VMs você montou a aplicação **3 camadas clássica** operando **tudo na mão**: instalou IIS, Node, iisnode, SQL Server, configurou NSG, proxy reverso e jump host. Funcionou — e ensinou **operação de infraestrutura real**.

Agora vem a pergunta que todo time faz depois: **"e se eu não quisesse mais cuidar dessas VMs?"** É isso que esta etapa demonstra — a **modernização** para serviços gerenciados (PaaS):

- 🖥️➡️☁️ **IIS na VM → Azure Web App**: você para de instalar IIS, aplicar patch de Windows, abrir porta de firewall. O Azure mantém o host; você só publica o app.
- 🗄️➡️☁️ **SQL Server na VM → Azure SQL Database**: backups automáticos, alta disponibilidade nativa e patching gerenciado — sem você administrar o SGBD.
- 🧰 **Ferramentas assistidas**: em vez de refazer tudo, usamos ferramentas **oficiais de migração** que automatizam a maior parte do trabalho — você avalia, migra e valida.

> 💡 **Por que isso importa?** É a transição que mais aparece no mercado: empresas saindo de "lift-and-shift em VM" para PaaS, atrás de **menos custo operacional** e **mais escala/resiliência**. Saber **conduzir essa migração** — com avaliação, ferramenta certa e plano de cutover — é o diferencial desta etapa.

---

## 🎯 2. Objetivos do evento

Ao final, você terá feito **com as suas próprias mãos**:

| # | Você vai aprender a... |
|---|---|
| 1 | **Desenhar o estado-alvo PaaS** e uma taxonomia de nomes consistente com a fase VM |
| 2 | **Avaliar a migração sem appliance**: TCO/Pricing Calculator + os relatórios de *readiness* embutidos nas próprias ferramentas |
| 3 | Migrar um site IIS para **Azure Web App** usando o **App Service Migration Assistant** |
| 4 | Configurar **App Settings**, **VNet Integration** (permanente — boa prática) e **CORS** num Web App |
| 5 | Resolver a pegadinha do **proxy reverso no App Service** (`applicationHost.xdt`) |
| 6 | Migrar um banco SQL Server para **Azure SQL Database** com o **Azure Database Migration Service (DMS)** — migração gerenciada offline + **Self-hosted Integration Runtime** |
| 7 | Executar um **cutover blue/green** com domínio próprio, **reaproveitando o certificado das VMs** (importado num Key Vault) — e conhecer a opção de **certificado gerenciado** |
| 8 | **Comparar VM × PaaS** com números (custo, patch, escala, deploy) e desligar as VMs |

> 🧠 **Filosofia:** **Portal-first** + **ferramentas assistidas**. CLI/PowerShell só onde necessário (ex.: atribuir uma role a um service principal, ou desligar tudo no final). Você sai sabendo **qual ferramenta usar para cada tipo de carga** e **por quê**.

> ⏱️ **O que esperar de tempo:** ~1h30 a 2h. A maior parte é a ferramenta trabalhando (empacotar/publicar o site, mover os dados) — seu trabalho é configurar e validar.

---

## ☁️ 3. Ferramentas e serviços que vamos usar

Dois grupos: **ferramentas de migração** (rodam na sua máquina/VMs, são gratuitas) e **recursos Azure de destino** (o ambiente PaaS novo).

### 🧰 Ferramentas de migração (gratuitas)

| Ferramenta | Para que serve | Onde roda | Custo |
|---|---|---|---|
| 🧮 **Azure Pricing / TCO Calculator** | Comparar custo VM × PaaS (o "porquê") | Navegador | Grátis |
| 📦 **App Service Migration Assistant** | Avalia um site IIS e o publica num Web App | Instalado na VM de origem (`vm-bend`, `vm-fend`) | Grátis |
| 🧪 **Azure Database Migration Service (DMS)** + **Self-hosted Integration Runtime** | Migra o banco da `vm-data` para o Azure SQL (offline, gerenciado) | DMS no Portal; SHIR instalado na `vm-data` | Grátis (DMS offline) |

> 🛰️ **E o Azure Migrate?** Ele é ótimo, mas o *discovery* completo exige instalar um **appliance** (uma VM/coletor) na rede — desproporcional para 2 VMs de workshop. As duas ferramentas acima são **standalone** e já trazem o *assessment* embutido, então **não usamos o appliance** aqui. Para o slide de "quanto eu economizo", a **Pricing/TCO Calculator** (sem appliance) é suficiente.

### 🎯 Recursos Azure de destino (o ambiente PaaS)

Tudo num **novo Resource Group** PaaS, em **Central India** (mesma região da app), seguindo a taxonomia da Fase 1:

| Serviço Azure | Nome (taxonomia) | Para que serve | Camada / Custo |
|---|---|---|---|
| 📕 **App Service Plan** | `asp-prd-tk-cin-001` | Host compartilhado dos 2 Web Apps (Windows, **B1**) | B1 ~$13/mês |
| 🌐 **Web App backend (API)** | `app-prd-tk-bend-cin-001` | Substitui a `vm-bend` (Node + iisnode gerenciados) | incluso no plano |
| 🌐 **Web App frontend** | `app-prd-tk-fend-cin-001` | Substitui a `vm-fend` (SPA + proxy reverso) | incluso no plano |
| 🗄️ **Azure SQL — servidor lógico** | `sql-prd-tk-cin-001` | "Endereço" do banco gerenciado (`*.database.windows.net`) | grátis (cobra o DB) |
| 🗄️ **Azure SQL Database** | `FIFA2026Tickets` | Substitui o SQL Server da `vm-data` (**Basic**) | Basic ~$5/mês |
| 🔁 **Azure Database Migration Service** | `dms-prd-tk-cin-001` | Migração gerenciada do banco (offline), via **SHIR** na `vm-data` | grátis (offline) |

> 💰 **Custo total real do PaaS:** ~**$18/mês** (B1 + SQL Basic) se ficar ligado 24/7 — e diferente da VM, **não há o que "desligar"**; você **apaga o Resource Group** ao final do evento e o custo zera. Prorateado para um dia de evento, são **centavos**. Bem dentro do crédito da conta trial.

> 🌍 **Nomes globais!** `app-...` (Web App) e `sql-...` (servidor lógico) viram **endereços públicos** (`.azurewebsites.net` / `.database.windows.net`), então o nome é **único no mundo**. Se o Portal disser *"already taken"*, acrescente suas iniciais: ex.: `app-prd-tk-bend-cin-rss-001`. **Anote o nome final** que você usou.

> 🔐 **Sobre segredos:** neste guia as credenciais do banco ficam em **App Settings** do Web App (melhor que o `.env` na VM, mas ainda em texto na config). _Evolução de produção:_ **Azure Key Vault + Managed Identity** — veja a §7.

---

## 🗺️ 4. Arquitetura: antes e depois

### Antes — o que você construiu no guia das VMs (estado pós-hardening)

```
                 Internet
                    │  80/443
                    ▼
        ┌───────────────────────┐   RDP (jump host)
        │  vm-fend  (pública)   │◀───────────── ───┐
        │  IIS + ARR (proxy)    │                  │
        └───────────┬───────────┘                  │
              /api/* │ (porta 80, VNet)            │
                    ▼                              │
        ┌───────────────────────┐                  │
        │  vm-bend  (privada)   │                  │
        │  IIS + iisnode + Node │                  │
        └───────────┬───────────┘                  │
              1433   │  (peering global)           │
                    ▼                              │
        ┌───────────────────────┐                  │
        │  vm-data  (privada)   │──────────────────┘
        │  SQL Server 2022      │
        └───────────────────────┘
```

### Depois — o estado-alvo 100% PaaS

```
                 Internet
                    │  443 (HTTPS gerenciado)
                    ▼
   ┌───────────────────────────────────┐
   │  app-prd-tk-fend  (Web App)       │   SPA + proxy /api/* (ARR via applicationHost.xdt)
   └───────────────┬───────────────────┘
            /api/*  │  HTTPS
                    ▼
   ┌───────────────────────────────────┐
   │  app-prd-tk-bend  (Web App)       │   API Node (iisnode gerenciado)
   └───────────────┬───────────────────┘
            1433    │  TLS, endpoint público + firewall
                    ▼
   ┌───────────────────────────────────┐
   │  Azure SQL Database               │   FIFA2026Tickets @ sql-prd-tk-cin-001
   │  (gerenciado: backup/HA/patch)    │
   └───────────────────────────────────┘
```

> 🔁 **Fluxo igual, infra diferente.** A aplicação **não muda**: o front continua chamando `/api` na mesma origem e o backend continua falando SQL na 1433. O que sai de cena são as **3 VMs, o NSG, o peering e o jump host** — substituídos por serviços gerenciados.

**Princípios de design (e o que isso ensina):**

- 🟦🟩 **Blue/green, uma camada por vez.** Migramos **backend → frontend → banco**. Cada fase cria o recurso PaaS **ao lado** da VM, testa pelo endereço `*.azurewebsites.net`, e só depois reaponta. Você nunca derruba o ambiente antigo antes do novo provar que funciona.
- 🗃️ **Banco por último (e de propósito).** Migrar **compute** (back/front) é "republicar o mesmo código num host novo". Migrar **dados** é diferente — tem schema, volume e uma **janela de corte**. Deixamos por último para o ambiente antigo continuar sendo a fonte da verdade até o momento exato do cutover.
- 🔌 **VNet Integration é permanente (boa prática).** O **backend Web App** alcança a rede privada via **VNet Integration + peering** — primeiro o IP privado da `vm-data`, e depois o **Private Endpoint** do Azure SQL. Em vez de expor o banco na internet, o tráfego fica **dentro da VNet** do começo ao fim: modernizamos o banco (Fase 5), validamos pelo endpoint público temporariamente, e então **trancamos com Private Endpoint** (Fase 8). A integração **não é removida** — ela é o caminho de saída do app para a rede privada.
- 🔁 **Proxy reverso, agora gerenciado.** O mesmo `web.config` do front roda no Web App (é IIS por baixo), mas o "Enable proxy" do ARR — que na VM era um checkbox — no App Service vira um **`applicationHost.xdt`**. Mesma ideia, mecanismo PaaS.

---

## 🧭 5. A jornada do aluno

| Fase | Etapa | Tempo aprox. |
|---|---|---|
| **Fase 0** | Pré-requisitos (app nas VMs no ar + ferramentas instaladas) | 15 min |
| **Fase 1** | Desenho do estado-alvo PaaS + taxonomia | 10 min |
| **Fase 2** | Assessment sem appliance (TCO + readiness das ferramentas) | 15 min |
| **Fase 3** | Migrar **Backend** (API) → `app-prd-tk-bend` (App Service Migration Assistant) | 25 min |
| **Fase 4** | Migrar **Frontend** → `app-prd-tk-fend` (+ `applicationHost.xdt`) | 20 min |
| **Fase 5** | Migrar **Banco** → Azure SQL Database (Azure DMS, offline) | 45 min |
| **Fase 6** | Smoke test ponta a ponta (100% PaaS) | 10 min |
| **Fase 7** | Decomissionar as VMs + comparação VM × PaaS | 10 min |
| **Fase 8** | Rede privada: Private Endpoints + VNet Integration (só o front público) | 60 min |
| **Fase 9** | Troubleshooting | livre |

> 🧠 **Total esperado:** ~1h30–2h de mão na massa (+ ~1h se incluir a **Fase 8** de rede privada). Reserve **2h30–3h30** na primeira execução.

> 🔒 **Fase 8 — rede privada (boa prática recomendada).** As Fases 0–7 entregam o app **100% em PaaS** (com endpoints públicos protegidos por firewall). A **Fase 8** fecha a porta da internet para API e banco via **Private Endpoint** — é o que separa "PaaS que funciona" de "PaaS de produção" e o **caminho recomendado** depois de modernizar o banco (mantemos a VNet Integration justamente para isso). Você pode validar nas Fases 0–7 e emendar a Fase 8 em seguida.

---

### Fase 0 — Pré-requisitos

- [ ] **App rodando nas 3 VMs** (estado final do [`GUIA-EVENTO-VMS.md`](GUIA-EVENTO-VMS.md), pós-Fase 8). Confirme: `http(s)://<seu-domínio>` (ou `http://IP_FRONT`) abre e o login funciona.
- [ ] **As 3 VMs ligadas** (se você fez `deallocate`, suba de novo: `az vm start ...`). Você vai precisar delas como **origem** da migração.
- [ ] **Conta Azure ativa** — a mesma do guia anterior.
- [ ] **Bloco de notas** com o que você anotou na fase VM: `IP_DB` (privado da `vm-data`), `adminsql`/`Partiunuvem@2026`, `JWT_SECRET`, e o seu **domínio** (se fez a Fase 6 das VMs).
- [ ] **Ferramentas de migração** (baixe agora, instale nas fases indicadas):
  - **(Opcional) App Service Migration Assistant** — [appmigration.microsoft.com](https://appmigration.microsoft.com/). Só para ver o *assessment* (a publicação é por **zip deploy**, Fases 3.2/4.2). Se for usar, instala na `vm-bend`/`vm-fend`.
  - **Azure CLI** (se for publicar pela **Opção A**, na própria VM) — `winget install -e --id Microsoft.AzureCLI` ou [aka.ms/installazurecliwindows](https://aka.ms/installazurecliwindows). _Quem usar o **Cloud Shell** (Opção B) não precisa instalar nada._
  - **Self-hosted Integration Runtime (SHIR)** — **baixado dentro da Fase 5** (link no próprio DMS) e instalado na `vm-data` para o **Azure DMS** migrar o banco. _O Azure Data Studio foi **aposentado em 28/02/2026** — não use mais; o **SSMS** ([aka.ms/ssms](https://aka.ms/ssms)) fica como ferramenta **opcional** de administração SQL._
- [ ] **(Opcional) Projeto do Azure Migrate (em branco)** — só necessário se você for rodar o **App Service Migration Assistant** para ver o *assessment*. Portal → busca **Azure Migrate** → **Create project** → **Resource group:** `rg-prd-tik-paas-cin-001` · **Project name:** `migr-prd-tk-cin-001` · **Geography:** a mais próxima → **Create**.

> ℹ️ **Por que é opcional agora:** neste guia a **publicação é feita por zip deploy** (Fases 3.2/4.2), que **não usa** o assistant nem o projeto. O projeto do Azure Migrate só entra **se você quiser rodar o assistant pelo valor didático do assessment** — e, nesse caso, ele é **pré-requisito**: a versão atual do assistant exige um projeto na assinatura (mesmo para app único) e **trava sem ele** na tela "Azure Migrate Hub". O projeto fica **em branco** — sem appliance, sem discovery; é só o "guarda-chuva" do assessment. Como a API é **Node**, o assistant **só assessa, não publica** (ver Fase 3.2) — por isso o deploy real é sempre o zip.

**Alerta de orçamento:** Portal → **Cost Management → Budgets → + Add** → **$20/mês**, alerta em 80% e 100% → seu e-mail. (O PaaS é barato, mas o hábito é bom.)

> 🌐 **Importante — mantenha a `vm-fend` ligada até a Fase 5.** Ela é o seu **jump host** para acessar a `vm-data` (privada). Você só desliga/apaga **todas** as VMs na Fase 7, depois que o banco migrar.

> ✅ **Pronto quando:** o app abre pelas VMs, as 3 VMs estão **Running**, e você tem os dois instaladores baixados.

---

### Fase 1 — Desenho do estado-alvo + taxonomia PaaS

> 🧠 **Mesma disciplina da fase VM: planta antes de tijolo.** Antes de criar recurso, fechamos **nomes** e **ordem de migração**. O instrutor apresenta o estado-alvo (o diagrama "depois" da §4) e debatemos as decisões.

#### 1.1 Taxonomia dos recursos PaaS

Mesmo padrão `<tipo>-<ambiente>-<carga>-<região>-<instância>` da fase VM. **Use estes nomes** (ajustando os globais com suas iniciais se necessário):

| Recurso | Nome | Região | Observação |
|---|---|---|---|
| Resource Group (PaaS) | `rg-prd-tik-paas-cin-001` | Central India | **separado** do `rg-prd-tik-cin-001` (VMs) — facilita apagar as VMs depois |
| App Service Plan | `asp-prd-tk-cin-001` | Central India | Windows, **B1** |
| Web App backend | `app-prd-tk-bend-cin-001` | Central India | **nome global** |
| Web App frontend | `app-prd-tk-fend-cin-001` | Central India | **nome global** |
| Azure SQL (servidor lógico) | `sql-prd-tk-cin-001` | Central India | **nome global** |
| Azure SQL Database | `FIFA2026Tickets` | — | mesmo nome do banco da VM |
| Database Migration Service | `dms-prd-tk-cin-001` | Central India | motor gerenciado da migração do banco (Fase 5); usa o **SHIR** na `vm-data` |
| Projeto Azure Migrate (em branco) | `migr-prd-tk-cin-001` | Geography mais próxima | **opcional** — só se rodar o assistant para o assessment; sem appliance/discovery. Publicação real é por zip deploy |
| Key Vault | `kv-prd-tk-cin-001` | Central India | **nome global**; guarda o certificado das VMs (.pfx) p/ o domínio customizado (Fase 4.5) |

#### 1.2 Ordem da migração (e por quê)

```
1. Backend (API)   vm-bend  ──▶ app-prd-tk-bend     (front ainda na VM aponta para o Web App novo)
2. Frontend        vm-fend  ──▶ app-prd-tk-fend     (back e front já em PaaS; banco ainda na VM)
3. Banco           vm-data  ──▶ Azure SQL Database  (tudo em PaaS; as 3 VMs podem cair)
```

- **Uma camada por vez** → se algo quebrar, você sabe exatamente onde.
- **Banco por último** → o dado fica autoritativo na VM até o corte final (menor risco).
- **Cada fase deixa o app funcional** → você pode parar em qualquer ponto.

> 💬 **Momento de debate:** alternativas válidas existem (migrar o banco primeiro mudaria a ordem da rede privada, por exemplo). Adotamos **back → front → db** por ser o padrão incremental mais usado em produção e por manter o dado na origem até o fim. Discuta o trade-off com o instrutor.

> ✅ **Pronto quando:** você tem a tabela de nomes fechada e entende **por que** migramos nesta ordem.

---

### Fase 2 — Assessment sem appliance (o "porquê")

> 🧠 **Antes de migrar, justifique.** Em projeto real, a migração começa com um **assessment**: quanto custa hoje, quanto custaria em PaaS, e o app é **compatível**? Fazemos isso **sem o appliance do Azure Migrate** — com a calculadora de custo e os relatórios que as próprias ferramentas já geram.

#### 2.1 TCO / Pricing Calculator (custo VM × PaaS)

1. Abra a **[Pricing Calculator](https://azure.microsoft.com/pricing/calculator/)**.
2. **Cenário VM (hoje):** adicione **3× Virtual Machines** B2s (Windows) → veja o total (~$90/mês 24/7).
3. **Cenário PaaS (alvo):** adicione **1× App Service** (B1) + **1× Azure SQL Database** (Basic) → total (~$18/mês).
4. 📋 **Anote os dois números.** Essa diferença (e o fato de o PaaS **não exigir patch/operação**) é o seu **slide de motivação**.

> 💡 **Custo não é tudo.** Mesmo quando o preço é parecido, o PaaS remove **trabalho operacional** (patch de Windows, backup, hardening do IIS, alta disponibilidade). Isso é "custo total de propriedade" (TCO) — geralmente o argumento mais forte.

#### 2.2 Readiness do app (App Service Migration Assistant)

O *assessment* de compatibilidade do site vem **embutido** na ferramenta — você vai vê-lo no começo da Fase 3 (o assistant roda um **readiness check** antes de publicar). Não há passo separado aqui; é só saber que **a avaliação acontece dentro da ferramenta**.

#### 2.3 Readiness do banco

Para o destino **Azure SQL Database**, o assessment de compatibilidade é feito pelo **Azure Migrate** (SQL assessment): aponta **incompatibilidades**, recomenda **tier/sizing** e estima custo — é o passo profissional **antes** de migrar (Fase 5). Para este schema simples não há bloqueios esperados; em ambiente grande, é onde você valida a compatibilidade antes de mover os dados.

> ✅ **Pronto quando:** você tem os dois números de custo anotados e entende que os relatórios de compatibilidade do app e do banco virão **dentro** das ferramentas (Fases 3 e 5).

---

### Fase 3 — Migrar o Backend (API) → Web App

> 🎯 **Objetivo:** substituir a `vm-bend` por `app-prd-tk-bend-cin-001`, mantendo front e banco onde estão. Ao final, o front (ainda na VM) passa a chamar o **Web App** novo.

#### 3.1 Criar o App Service Plan + o Web App backend (Portal)

1. Portal → busca **App Services** → **+ Create** → **Web App**.
2. **Resource group:** `rg-prd-tik-paas-cin-001` (crie agora, na própria tela) · **Name:** `app-prd-tk-bend-cin-001`
3. **Publish:** **Code** · **Runtime stack:** **Node 24 LTS** · **OS:** **Windows** ← (Windows mantém o **iisnode** e o `web.config` da sua API, igual à VM)
4. **Region:** **Central India** · **App Service Plan:** crie `asp-prd-tk-cin-001` · **Pricing plan:** **Basic B1**
5. **Review + create** → **Create**.

> 💡 **Por que Windows + Node?** A sua API roda em **iisnode** (IIS hospedando Node). O App Service **Windows** usa exatamente esse mecanismo por baixo — então o `web.config` da API e a estrutura `src/` funcionam **sem reescrever nada**. (No Linux a API rodaria em Node "puro", mais moderno, mas exigiria remover o `web.config`/iisnode — fica como evolução.)

#### 3.2 Publicar a aplicação (zip deploy) — deixar o app no ar primeiro

> 🎯 **Estratégia:** publicamos o código **logo após criar o Web App**, antes das demais configurações. Primeiro confirmamos que o app **sobe e responde** (`/api/health`); só então adicionamos HTTPS (3.3), a rede até o banco (3.4) e as variáveis (3.5). Vantagem didática: se o `/api/health` responde, o **runtime Node está OK** — qualquer problema daí pra frente é **configuração**, não publicação.

> 🧩 **Por que não publicar pelo App Service Migration Assistant?** O assistant **avalia** qualquer site IIS e envia o *assessment* para o projeto do Azure Migrate (a tela **"Azure Migrate Hub"** → *Sending data complete*), mas a etapa de **publicar o conteúdo é só para .NET**. Como a API é **Node**, o assistant **vai até o assessment e para** — não aparece botão de finalizar/`Migrate`. Então: deixe o assistant rodar o assessment se quiser (é didático e popula o projeto do Azure Migrate), mas **quem publica o Node é o zip deploy** abaixo. Este é o método da **"linha A"** — mesmo resultado que o assistant daria para um app .NET.

**Passo 1 — Empacotar o app (na `vm-bend`, via RDP pelo jump host `vm-fend`):**

```powershell
cd C:\inetpub\wwwroot
Compress-Archive -Path .\fifa2026-api\* -DestinationPath .\fifa2026-api.zip -Force
```

> ⚠️ O `\fifa2026-api\*` (com `\*`) é proposital: garante que `web.config`, `src/` e `node_modules` fiquem na **raiz** do zip — **não** dentro de uma subpasta `fifa2026-api/`. Se aninhar, o App Service não acha o `web.config` e a API não sobe.

**Passo 2 — Publicar.** Escolha **uma** das duas formas:

**▶️ Opção A — Azure Cloud Shell** (o aluno **não instala nada** na VM). Abra o **Cloud Shell** ([shell.azure.com](https://shell.azure.com) ou o ícone `>_` no Portal) — ele já vem **autenticado** e com `az`/Az PowerShell prontos (não precisa de `az login`/tenant).

1. Na **`vm-bend`**, gere o `fifa2026-api.zip` (Passo 1 acima).
2. No **Cloud Shell**: botão **Upload/Download files → Upload** → selecione o `fifa2026-api.zip` (ele cai no seu diretório home do Cloud Shell).
3. No **Cloud Shell** (Bash):
   ```bash
   az account set --subscription "<SUBSCRIPTION_ID_ou_NOME>"
   az webapp deploy -g rg-prd-tik-paas-cin-001 -n app-prd-tk-bend-cin-001 --src-path ./fifa2026-api.zip --type zip
   ```

**▶️ Opção B — Azure CLI na própria VM** (evita o upload, pois o zip já está local). Requer o Azure CLI na `vm-bend` — se não tiver: `winget install -e --id Microsoft.AzureCLI` (ou o MSI em [aka.ms/installazurecliwindows](https://aka.ms/installazurecliwindows)), e feche/reabra o PowerShell.

```powershell
az login --tenant <TENANT_ID>                       # se o navegador da VM travar: az login --use-device-code --tenant <TENANT_ID>
az account set --subscription "<SUBSCRIPTION_ID_ou_NOME>"
az webapp deploy -g rg-prd-tik-paas-cin-001 -n app-prd-tk-bend-cin-001 --src-path .\fifa2026-api.zip --type zip
```

> 💡 **Qual opção escolher?** O **Cloud Shell (A)** dispensa instalar Azure CLI/módulo na VM e já vem logado — porém exige **subir o zip** primeiro (ele inclui `node_modules`, então tem dezenas de MB; em rede de evento o upload pode demorar). A **CLI na VM (B)** evita o upload (o zip já está local), mas exige instalar o Azure CLI uma vez. Para turma grande sem querer instalar nada, **A**; para quem já tem a CLI ou prioriza velocidade, **B**.

> 📋 **Descobrir tenant e subscription:** `az account show --query "{tenant:tenantId, subscription:id, nome:name}" -o table`. No Portal: **Microsoft Entra ID** mostra o *Tenant ID*; **Subscriptions** mostra o *Subscription ID*.

> ⏳ **O deploy retornou `500 - The request timed out`? É NORMAL — não refaça às cegas.** Com `node_modules` (dezenas de milhares de arquivos pequenos), a extração no storage de rede do App Service passa do tempo da **resposta HTTP** — mas **o deploy continua no servidor e conclui com sucesso**. Em vez de repetir o comando, **acompanhe a conclusão**:
> - **Portal → o Web App → `Deployment Center` → aba `Logs`** — o deploy aparece e muda para **`Success`**; ou
> - abra `https://<APP>.scm.azurewebsites.net/api/deployments/latest` — deve terminar com status de sucesso (`"complete": true`).
>
> **Quando concluir, teste a saúde da API:**
> ```powershell
> Invoke-RestMethod "https://app-prd-tk-bend-cin-001.azurewebsites.net/api/health"   # esperado: status = ok
> ```
> Se o `/api/health` responde, **o app está no ar** ✅. (O `/api/health/db` ainda vai falhar **de propósito** — o banco só conecta depois da **VNet** (3.4) e das **App Settings** (3.5). A validação completa é a Fase 3.6.)

> ✅ **Confirmação extra (Portal/Kudu):** Web App com **Runtime = Node 24 / OS = Windows** (Fase 3.1) e o `web.config` presente em **Kudu → `site/wwwroot/`**.

#### 3.3 Habilitar HTTPS Only e TLS mínimo

No `app-prd-tk-bend-cin-001` → **Settings → Configuration** (ou **TLS/SSL settings**):
- **HTTPS Only:** **On**
- **Minimum TLS Version:** **1.2** · **FTP state:** **Disabled**

#### 3.4 VNet Integration — para o Web App alcançar a `vm-data` (ainda na VM)

O banco ainda está na `vm-data` (IP privado, outra região). Para o Web App falar com ele, ative **VNet Integration**.

1. Primeiro, crie uma **subnet dedicada** na VNet de app (o App Service exige uma subnet só dele): Portal → `vnet-prd-inf-cin-001` → **Subnets** → **+ Subnet** → **Name:** `snet-prd-inf-appsvc-cin-001` · **Range:** `10.20.3.0/24` · **Delegation:** **Microsoft.Web/serverFarms** → **Save**.
2. No `app-prd-tk-bend-cin-001` → **Settings → Networking** → **Outbound traffic → VNet integration** → **Add** → escolha `vnet-prd-inf-cin-001` / `snet-prd-inf-appsvc-cin-001`.
3. Ainda em Networking, garanta **Route All / Outbound internet traffic** habilitado para que o tráfego vá pela VNet (e alcance a outra região via **peering**).

> 💡 **Por que isso é necessário (por enquanto)?** Azure SQL terá endpoint público, mas o **SQL Server na VM não** — ele só responde no IP privado. A VNet Integration "pluga" o Web App na sua rede; o **peering global** (que você criou na fase VM) leva o pacote até a `vm-data` em Australia East. A NSG do banco já libera `1433` da faixa `10.20.0.0/16`, então **não precisa mexer no NSG**. A VNet Integration **permanece** (boa prática): após modernizar o banco (Fase 5), o Azure SQL passa a ser alcançado por **Private Endpoint** (Fase 8) — sem expor o banco na internet.

#### 3.5 App Settings + Connection String do banco

Aqui separamos as variáveis **não-banco** (App Settings) do **banco** (Connection String, numa aba própria — mais correto e mascarado no Portal).

**(a) App Settings** — `app-prd-tk-bend-cin-001` → **Settings → Environment variables → App settings** → **+ Add** (uma por uma) → **Apply**:

| Nome | Valor |
|---|---|
| `JWT_SECRET` | `trocar_por_uma_string_longa_aleatoria` (mesmo estilo do `.env` — string longa com underscores; pode reusar a do `.env`) |
| `JWT_EXPIRES_IN` | `7d` |
| `FRONTEND_URL` | `*` (ajustamos para a URL do front na Fase 4) |
| `WEBSITE_NODE_DEFAULT_VERSION` | `~24` |

> ⚠️ **Não existe `PORT=80` aqui.** No App Service **quem define a porta é a plataforma** — o iisnode injeta a porta certa e sua API já lê `process.env.PORT`. Por isso **não** adicione `PORT` nem `HOST`.

> 🚨 **`JWT_EXPIRES_IN` PRECISA de unidade** (`7d`, `24h`, `60m`) — **nunca** um número puro como `7`. O `jsonwebtoken` trata string numérica sem unidade como **milissegundos** (`7` = `7ms`), então o token **nasce expirado** (`exp == iat`) e **toda rota autenticada passa a dar 401** — login, perfil, compra — enquanto o **cadastro** (que só assina, não valida) continua funcionando e **mascara** o problema. Sintoma típico: "autenticação quebrou só nas telas logadas". Se acontecer, confira **primeiro** este valor (decodifique o token e veja se `iat == exp`).

**(b) Connection String do banco** — em vez de `DB_SERVER`/`DB_USER`/`DB_PASSWORD` em App settings, o banco vai na aba **Connection strings**:

1. `app-prd-tk-bend-cin-001` → **Settings → Environment variables → Connection strings** → **+ Add**.
2. **Name:** `DefaultConnection` · **Type:** **SQLServer** _(é SQL Server na VM; na Fase 5, ao migrar para Azure SQL, muda para **SQLAzure**)_.
3. **Value** (string **pronta** — troque só `<IP_DB>` pelo IP privado da `vm-data`):
   ```text
   Server=<IP_DB>,1433;Database=FIFA2026Tickets;User Id=adminsql;Password=Partiunuvem@2026;Encrypt=true;TrustServerCertificate=true
   ```
4. **Apply** (reinicia sozinho).

> ✅ **Não precisa mexer no código.** O pacote já vem preparado para ler a Connection String do App Service (o `database.js` prioriza a `DefaultConnection` e cai nas variáveis `DB_*` só como fallback). Basta cadastrá-la acima.

> 💡 **Mudou App Setting/Connection String? O Web App reinicia sozinho** — não precisa de `iisreset`. Com a **VNet (3.4)** + a **Connection String** salvas, o `/api/health/db` passa a conectar.

#### 3.6 Testar o backend novo (isolado, pelo endereço do Web App)

Do seu computador:

```powershell
$BEND = "https://app-prd-tk-bend-cin-001.azurewebsites.net"
Invoke-RestMethod "$BEND/api/health"          # OK
Invoke-RestMethod "$BEND/api/health/db"        # deve mostrar connected:true
(Invoke-RestMethod "$BEND/api/matches").matches.Count   # 104
```

> 💡 **O `/api/health/db` é seu melhor amigo aqui.** Se vier `ETIMEOUT/ESOCKET` → a VNet Integration/peering não está roteando até a `vm-data` (reveja 3.4). Se vier `ELOGIN` → confira o `User Id`/`Password` na **Connection String** `DefaultConnection`. **Mudou App Setting/Connection String? O Web App reinicia sozinho** (diferente do iisnode na VM, que exigia `iisreset`).

#### 3.7 Reapontar o front (ainda na VM) para o Web App backend

A `vm-fend` ainda serve o site e faz proxy `/api/*` para a `vm-bend`. Troque o destino para o Web App:

1. **RDP na `vm-fend`** → edite o `web.config` do front:
   ```powershell
   cd C:\inetpub\wwwroot\fifa2026-web
   (Get-Content web.config) -replace 'http://<IP_BACK>','https://app-prd-tk-bend-cin-001.azurewebsites.net' | Set-Content web.config
   ```
   _(troque `<IP_BACK>` pelo IP privado que estava lá, ex.: `http://10.20.2.4`.)_
2. `iisreset` na `vm-fend`.

> ✅ **Pronto quando:** abrindo o app pela `vm-fend` (`http://IP_FRONT`), tudo funciona **mas o `/api` agora é servido pelo Web App**. Confirme: o `/api/health` responde mesmo com a **`vm-bend` desligada** — pode fazer `az vm deallocate -g rg-prd-tik-cin-001 -n vm-prd-tk-bend-cin-001` e testar de novo. 🎉 Uma VM a menos.

---

### Fase 4 — Migrar o Frontend → Web App

> 🎯 **Objetivo:** substituir a `vm-fend` por `app-prd-tk-fend-cin-001`. O front é estático + um `web.config` que faz **proxy reverso** de `/api/*` para o backend. A pegadinha desta fase é **habilitar o proxy no App Service**.

#### 4.1 Criar o Web App frontend (Portal)

1. Portal → **App Services** → **+ Create** → **Web App**.
2. **Resource group:** `rg-prd-tik-paas-cin-001` · **Name:** `app-prd-tk-fend-cin-001`
3. **Publish:** **Code** · **Runtime:** **Node 24 LTS** · **OS:** **Windows** · **Plan:** o mesmo `asp-prd-tk-cin-001` (o B1 hospeda os dois apps).
4. **Create** → depois ligue **HTTPS Only** + **TLS 1.2** (como em 3.2).

> 💡 **Mesmo plano, dois apps.** O App Service Plan é o "servidor"; cada Web App é um "site" nele. B1 acomoda os dois tranquilamente — você não paga a mais por isso.

#### 4.2 Publicar o conteúdo (zip deploy)

> 🧩 **Mesmo método do backend (linha A).** O frontend é **estático** (HTML/JS/CSS + `web.config`), não um framework Node — então, diferente do backend, o assistant **até conseguiria** publicá-lo. Mas, por **consistência** (e para não depender do comportamento da ferramenta), usamos o **mesmo zip deploy** da Fase 3.2 aqui — trocando só a pasta e o nome do Web App.

**Passo 1 — Empacotar (na `vm-fend`, via RDP):**

```powershell
cd C:\inetpub\wwwroot
Compress-Archive -Path .\fifa2026-web\* -DestinationPath .\fifa2026-web.zip -Force
```

> ⚠️ Igual ao backend: o `\*` mantém o `web.config` e os arquivos estáticos na **raiz** do zip.

**Passo 2 — Publicar** (escolha **A** ou **B**, exatamente como na Fase 3.2):

**▶️ Opção A — Azure Cloud Shell** (sem instalar nada): gere o zip na `vm-fend`, **Upload** no Cloud Shell, e:
```bash
az account set --subscription "<SUBSCRIPTION_ID_ou_NOME>"
az webapp deploy -g rg-prd-tik-paas-cin-001 -n app-prd-tk-fend-cin-001 --src-path ./fifa2026-web.zip --type zip
```

**▶️ Opção B — Azure CLI na `vm-fend`:**
```powershell
az login --tenant <TENANT_ID>                       # ou: az login --use-device-code --tenant <TENANT_ID>
az account set --subscription "<SUBSCRIPTION_ID_ou_NOME>"
az webapp deploy -g rg-prd-tik-paas-cin-001 -n app-prd-tk-fend-cin-001 --src-path .\fifa2026-web.zip --type zip
```

> 💡 O front é **bem mais leve** que o backend (não tem `node_modules` — é só o build estático), então no Cloud Shell o **upload é rápido**. Aqui a **Opção A (Cloud Shell)** costuma ser a mais cômoda.

> ⏭️ **Ainda falta o proxy `/api`.** Publicar o estático não basta: o `/api/*` só passa a funcionar depois do **`applicationHost.xdt`** da Fase 4.4. Não pule.

#### 4.3 Confirmar o destino do proxy no `web.config`

O `web.config` do front precisa apontar o `/api/*` para o **backend Web App** (não mais para o IP da VM). Se você já fez isso na Fase 3.7, o conteúdo publicado já vem certo. Confirme via **Kudu**: abra `https://app-prd-tk-fend-cin-001.scm.azurewebsites.net/DebugConsole` → navegue até `site/wwwroot/web.config` → a regra **Rewrite** deve mostrar `https://app-prd-tk-bend-cin-001.azurewebsites.net/...`.

#### 4.4 ⭐ Habilitar o proxy reverso (a pegadinha do App Service)

Na VM, você marcou **"Enable proxy"** no ARR (um checkbox). **No App Service não existe esse checkbox** — você habilita o proxy do ARR com um arquivo de transformação **`applicationHost.xdt`**.

1. No **Kudu** (`...scm.azurewebsites.net/DebugConsole`) navegue até a pasta **`site`** (ou seja, `D:\home\site\` — **um nível acima** de `wwwroot`).
2. Crie um arquivo **`applicationHost.xdt`** com este conteúdo:
   ```xml
   <?xml version="1.0"?>
   <configuration xmlns:xdt="http://schemas.microsoft.com/XML-Document-Transform">
     <system.webServer>
       <proxy xdt:Transform="InsertIfMissing" enabled="true"
              preserveHostHeader="false"
              reverseRewriteHostInResponseHeaders="false" />
     </system.webServer>
   </configuration>
   ```
3. **Reinicie** o Web App (Portal → `app-prd-tk-fend-cin-001` → **Restart**).

> ⚠️ **Sem o `applicationHost.xdt`, o `/api/*` retorna 404/502.** Esse é o **erro nº 1** desta fase — o equivalente PaaS de esquecer o "Enable proxy" no ARR da VM. O arquivo vai em **`site/`**, **não** em `site/wwwroot/`.

> 💡 **Por que um XDT e não editar o ApplicationHost.config?** No App Service você **não tem acesso** ao `ApplicationHost.config` do servidor (é gerenciado). O `applicationHost.xdt` é o jeito **suportado** de aplicar uma transformação nele só para o seu site, na inicialização.

#### 4.5 Domínio customizado no front (reaproveitando o certificado das VMs via Key Vault)

> 🎯 **Antes de fixar o CORS**, deixamos o front acessível pela **mesma URL** que você já usava nas VMs, **reaproveitando o certificado** emitido lá — **sem gerar um novo**. O cert é importado para um **Key Vault** e o Web App o referencia.

1. **Exportar o cert da etapa das VMs como `.pfx`** (com a chave privada + senha), de onde ele foi instalado/gerado na fase VM. Ex. na VM: `certlm.msc` → **Personal → Certificates** → o cert do seu domínio → **All Tasks → Export** → **Yes, export the private key** → formato **.pfx** → defina uma senha.
2. **Criar o Key Vault** (se ainda não tiver): Portal → busca **Key vaults** → **+ Create** → **Resource group:** `rg-prd-tik-paas-cin-001` · **Name:** `kv-prd-tk-cin-001` (**nome global** — se aparecer "já em uso", acrescente suas iniciais, ex.: `kv-prd-tk-rss-cin-001`) · **Region:** **Central India** · **Pricing tier:** **Standard** → na aba **Access configuration**, deixe **Azure role-based access control (RBAC)** → **Review + create** → **Create**.
3. ⚠️ **Dar a VOCÊ permissão para gerenciar o vault.** Com **RBAC**, o criador **não** recebe acesso de *data-plane* automaticamente — sem este passo, o import do passo 4 falha com *"...does not have certificates import permission"*. No vault → **Access control (IAM)** → **+ Add → Add role assignment** → role **Key Vault Administrator** (ou, mais restrito, **Key Vault Certificates Officer**) → **Members:** a **sua conta** → **Review + assign**. Aguarde ~1 min para propagar.
4. **Importar o `.pfx` no Key Vault:** o vault → **Objects → Certificates → Generate/Import → Import** → dê um nome (ex.: `cert-tftec-dominio`), faça **upload do `.pfx`** e informe a **senha** definida no passo 1.
5. ⚠️ **Dar acesso de leitura ao vault para o App Service** (é o que destrava o import). A feature **"Import certificate from Key Vault" NÃO usa a managed identity do seu app** — ela usa o **service principal de primeira-parte da plataforma, "Microsoft Azure App Service"** (appId `abfa0a7c-a6b6-4736-8310-5855508787cd`). No **Key Vault → Access control (IAM) → + Add → Add role assignment** → role **Key Vault Secrets User** → aba **Members** → em **"Assign access to"** escolha **"User, group, or service principal"** (⚠️ **não** "Managed identity" — esse SP não é uma MI) → **Select members** → procure **`Microsoft Azure App Service`** (se não achar pelo nome, **cole o appId** acima) → **Review + assign**. Aguarde **~2 min** para propagar.
   > Se o SP **não aparecer** no seletor (alguns tenants ocultam principals de primeira-parte), atribua por CLI: `az role assignment create --role "Key Vault Secrets User" --assignee abfa0a7c-a6b6-4736-8310-5855508787cd --scope <ID-do-Key-Vault>` (se o `--assignee` não resolver, use `--assignee-object-id $(az ad sp show --id abfa0a7c-a6b6-4736-8310-5855508787cd --query id -o tsv) --assignee-principal-type ServicePrincipal`).
   > Se o vault estivesse em modo **Access policy** (em vez de RBAC), seria uma access policy de **Get** em **Secrets** (+ **Certificates**) para esse mesmo principal.
6. **Importar o certificado no Web App a partir do Key Vault:** `app-prd-tk-fend-cin-001` → **Settings → Certificates → Bring your own certificates (.pfx) → Import from Key Vault** → selecione o vault e o certificado → **Add**. _(Funciona após o principal "Microsoft Azure App Service" ter acesso ao vault — passo 5. Se der "The service does not have access", a role ainda não propagou ou foi atribuída ao principal errado.)_
7. **Apontar o DNS** do domínio para o Web App: na sua zona DNS, **CNAME** `www` → `app-prd-tk-fend-cin-001.azurewebsites.net` e o **TXT** `asuid.www` = o *Custom Domain Verification ID* (Portal → o Web App → **Custom domains** mostra o ID).
8. **Adicionar o domínio customizado:** **Custom domains → + Add custom domain** → `www.<seu-domínio>` → **Validate** → **Add**.
9. **Binding TLS:** no domínio recém-adicionado → **Add binding** → selecione o **certificado importado do Key Vault** → **SNI SSL**.

> 🔐 **Quem lê o cert no import?** Não é a managed identity do seu app — é o **service principal de plataforma "Microsoft Azure App Service"** (passo 5). Por isso a role **Key Vault Secrets User** é atribuída a **esse** principal, e não ao Web App.

> ⚠️ **Timing do cutover de DNS:** ao mudar o CNAME para o Web App, o domínio **deixa de apontar para a `vm-fend`**. Garanta que o front no Web App já está **publicado (4.2)** e com o **proxy ativo (4.4)** antes de cortar o DNS — assim o usuário não fica sem app nem sem cadeado.

> 🔁 **Reaproveita o cert, não gera outro.** O **mesmo certificado das VMs** continua valendo (mesmo domínio, mesmo cadeado). Quando expirar, você renova no Key Vault (ou migra para App Service Managed Certificate como evolução).

#### 4.6 Ajustar o CORS / `FRONTEND_URL` do backend (definitivo)

Agora que o front tem o **domínio definitivo**, libere-o no backend:

- `app-prd-tk-bend-cin-001` → **App settings** → `FRONTEND_URL` = `https://www.<seu-domínio>` → **Apply** (reinicia sozinho).

> 🚨 **NÃO coloque barra `/` no final** (nem caminho) — é o erro mais comum aqui. O navegador manda o `Origin` como `https://seu-dominio` (**sem** `/`), e o backend compara a **string exata**: `https://seu-dominio/` **≠** `https://seu-dominio` → o CORS bloqueia e **cadastro/login falham só no navegador** (por `curl`/Postman funciona, porque não mandam `Origin` — o que confunde o diagnóstico). Use `https://tickets.tfteccloudlabs.cloud`, **nunca** `https://tickets.tfteccloudlabs.cloud/`.

> 💡 Enquanto o domínio não estava pronto, dava para deixar `FRONTEND_URL=*` (libera tudo). Agora fixamos no **domínio real** — mais seguro. **Vários domínios?** Separe por vírgula, **todos sem `/`**: `https://dominio-a,https://dominio-b`.

#### 4.7 Testar o front novo

Do seu computador, abra **`https://www.<seu-domínio>`** (com cadeado válido):
- [ ] A home carrega (jogos/estádios)
- [ ] Login `admin@fifa2026.com` / `admin123`
- [ ] Lista 104 jogos
- [ ] Compra de ingresso até o QR code
- [ ] Editar perfil salva (exercita `PUT` pelo proxy)

> ✅ **Pronto quando:** o app inteiro responde pelo **domínio customizado** (cert das VMs via Key Vault), com `/api` via proxy. Agora a `vm-fend` é descartável — mas **não a desligue ainda** (jump host para a Fase 5). 🎉

---

### Fase 5 — Migrar o Banco → Azure SQL Database

> 🎯 **Objetivo:** substituir o SQL Server da `vm-data` por um **Azure SQL Database**, migrando schema + dados com o **Azure Database Migration Service (DMS)** — a ferramenta **gerenciada** que a Microsoft recomenda para SQL Server → Azure SQL DB. É o caminho **profissional**: migração assistida, com monitoramento e (em produção) assessment de compatibilidade.

> ⚠️ **Mudança de ferramenta (2026):** o **Azure Data Studio foi aposentado (28/02/2026)** e a *Azure SQL Migration extension* foi descontinuada. O substituto para **migrar** é o **Azure DMS** (Portal/CLI); para o **assessment de compatibilidade** em escala, o **Azure Migrate** (SQL assessment). _(Para um banco pequeno, o **import de `.bacpac`** ainda é um atalho válido — porém **sem assessment**; fica como nota no fim da fase.)_

> 🟦🟩 **Modo offline:** para **Azure SQL Database**, o DMS só faz migração **offline** (online não é suportado para esse destino). O **downtime começa quando a migração inicia**. Como o banco é **pequeno** (104 jogos, ~3 MB) e o compute (front+API) **já está no ar**, o corte é rápido — padrão **blue/green**.

#### 5.1 Provisionar o Azure SQL Database (Portal)

1. Portal → busca **SQL databases** → **+ Create**.
2. **Resource group:** `rg-prd-tik-paas-cin-001` · **Database name:** `FIFA2026Tickets`
3. **Server:** **Create new** → **Server name:** `sql-prd-tk-cin-001` (global!) · **Location:** **Central India** · **Authentication:** **Use SQL authentication** · **Admin login:** `adminsql` · **Password:** crie forte e 📋 **anote** (rótulo: *Azure SQL admin*).
4. **Compute + storage:** **Configure** → **Basic** (~$5/mês, suficiente para o workshop).
5. **Networking** (aba): **Connectivity method:** **Public endpoint** · **Allow Azure services... :** **Yes** · **Add current client IP:** **Yes** → **Review + create** → **Create**.

> 💡 **"Allow Azure services" liga o quê?** Cria uma regra de firewall (`0.0.0.0`) que deixa **outros serviços Azure** (como o seu Web App backend) conectarem ao banco pelo endpoint público. O **"current client IP"** libera o **seu** IP para a migração rodar.

#### 5.2 Pré-requisitos do DMS

1. **Registrar o resource provider:** Portal → sua **Subscription** → **Resource providers** → procure **`Microsoft.DataMigration`** → **Register** (se ainda não estiver).
2. **Permissões da sua conta:** **Contributor** no Azure SQL DB de destino + **Reader** no resource group + **Owner/Contributor** na subscription (necessário para **criar** o DMS).
3. **Login de origem (`vm-data`):** o login usado pelo DMS precisa ser no mínimo **`db_datareader`** no banco de origem — o `adminsql` já é admin, então cobre.

#### 5.3 Criar o DMS e registrar o Integration Runtime na `vm-data`

> Como o SQL de origem está numa **VM privada**, o DMS alcança a origem por um **Self-hosted Integration Runtime (SHIR)** instalado numa VM **na mesma rede** — ele conecta no SQL pelo **IP privado** (`10.30.1.4`) e sai por **443** para o Azure (não expõe o banco). É o mesmo papel que o IR tinha no fluxo antigo do ADS.

1. Portal → **Azure Database Migration Service** → **+ Create**.
2. **Select migration scenario:** Source server type = **SQL Server**, Target = **Azure SQL Database** → tipo **Database Migration Service** → **Select**.
3. **Create:** subscription · **Resource group** `rg-prd-tik-paas-cin-001` · **Name** `dms-prd-tk-cin-001` · **Location** **Central India** → **Review + create** → **Create**.
4. No DMS → **Settings → Integration runtime → Configure integration runtime** → **Download and install integration runtime** ([aka.ms/sql-migration-shir-download](https://aka.ms/sql-migration-shir-download)). _(SHIR **≥ 5.37** para migrar schema — o instalador atual atende.)_
5. **RDP na `vm-data`** → instale o SHIR → o **Microsoft Integration Runtime Configuration Manager** abre sozinho.
6. Volte ao DMS → copie **uma** das **Authentication keys** → cole no Configuration Manager → **Register**. Quando aparecer o **✅ verde**, feche. Em ~1-2 min o **nó** aparece no DMS (**Settings → Integration runtime**).

#### 5.4 Rodar a migração offline (wizard)

1. No DMS → **Overview → New migration**.
2. **Select new migration scenario:** Source **SQL Server**, Target **Azure SQL Database**, mode **Offline** → **Select**.
3. **Connect to source SQL Server:** **Server name** `10.30.1.4` (IP privado da VM do SQL) · **Authentication** SQL · login `adminsql` / `Partiunuvem@2026` · marque **Trust server certificate** → **Next**.
4. **Select databases for migration:** marque **`FIFA2026Tickets`** → **Next**.
5. **Connect to target Azure SQL Database:** **Server** `sql-prd-tk-cin-001.database.windows.net` · login `adminsql` / a senha → **Next**.
   > 💡 Se a conexão ao destino falhar, libere no **firewall do Azure SQL** o IP de saída da `vm-data` (Networking → firewall rules), ou confirme **"Allow Azure services"** (Fase 5.1).
6. **Map source and target databases:** mapeie `FIFA2026Tickets` (origem) → `FIFA2026Tickets` (destino).
7. ⚠️ **Marque "Migrate Missing schema".** Como o destino está **vazio**, o DMS precisa criar **schema + tabelas + índices + views + procs…** antes de copiar os dados. _(Sem tabelas no destino e sem marcar isso, o wizard **não deixa avançar**.)_
8. **Select all tables** (ou filtre) → **Next: Database migration summary**.
9. Revise → **Start migration**. ⏱️ **O downtime offline começa aqui.**

#### 5.5 Monitorar a migração

1. DMS → **Overview → Monitor migrations** → aba **Migrations** (botão **Refresh** atualiza o status).
2. Os status passam por: *Creating → Preparing for copy → Copying → Copy finished → Rebuilding indexes → **Succeeded***. Clique no nome da origem para ver o detalhe **por tabela**.
3. Para ~3 MB, conclui em poucos minutos. _(Nota: o DMS **pula tabelas vazias** — elas podem não aparecer na lista.)_

> ✅ **Migração terminou com `Succeeded` em todas as tabelas? Então PULE a §5.5.1 inteira e vá direto para a §5.6.** A seção abaixo é **só para quem deu erro**.

#### 5.5.1 ⚠️ CONTORNO — só se a migração FALHOU ou TRAVOU

> 🛑🛑🛑 **LEIA ANTES DE FAZER QUALQUER COISA AQUI.**
> Esta seção é uma **solução de contorno (plano B)**. Ela **NÃO faz parte do fluxo normal**.
> **Se a sua migração da §5.5 terminou com `Succeeded`, NÃO execute nada desta seção — vá para a §5.6.**
> Executar estes passos com a migração já bem-sucedida vai **APAGAR o banco que você acabou de migrar** e te obrigar a fazer tudo de novo. **Só siga aqui se viu erro/travamento.**

**Quando usar:** alguma tabela ficou em **Failed/Canceled**, o status empacou em `Copying`/`Rebuilding indexes` e não anda, ou faltou uma tabela específica de verdade.

##### Passo 1 — Confirmar se travou MESMO (acompanhe por tabela, não pelo app)

A validação correta é **na própria tela do DMS, tabela por tabela** — não pelo app (testar só `/api/matches` engana, pois a tabela de jogos pode ter vindo e outra ter falhado).

1. Portal → abra o **`dms-prd-tk-cin-001`** → **Overview → Monitor migrations** → aba **Migrations**.
2. Clique no **nome da origem** (`FIFA2026Tickets`) → abre o **detalhe por tabela**.
3. Olhe **tabela por tabela**:
   - **Todas `Succeeded`** → migrou completo. **Saia daqui e vá para a §5.6.**
   - Alguma **`Failed`/`Canceled`**, ou status geral parado em `Copying`/`Rebuilding` → migração **incompleta**, continue abaixo.

> ⚠️ **Não confunda "tabela ausente" com "tabela com falha".** O DMS **não lista tabelas vazias** (ele as pula de propósito). Uma tabela que não aparece na lista pode ser só uma tabela **sem linhas** na origem — isso é **normal**, não é erro. Falha é a tabela aparecer **com status de erro**, não a ausência dela.

##### Passo 2 — O que reaproveita (não refaça a fase inteira)

| Recurso | Reaproveita? |
|---|---|
| DMS (`dms-prd-tk-cin-001`) | ✅ Sim |
| SHIR na `vm-data` (nó verde) | ✅ Sim |
| Servidor Azure SQL `sql-prd-tk-cin-001` | ✅ Sim |
| **Database de destino (com dados parciais)** | ❌ **Tem que apagar** |

Você só vai **re-disparar a migração**. O único bloqueio é o database destino "meio cheio" — o DMS não roda por cima de tabelas que já existem (erro de schema duplicado / chave primária).

##### Passo 3 — Apagar o DATABASE de destino (o passo que mais esquecem)

> 🛑🛑 **ATENÇÃO — onde NÃO mexer:**
> - **NÃO é na VM.** O SQL Server da `vm-data` (origem, IP `10.30.1.4`) **não deve ser alterado em nada** — ele continua sendo a fonte da verdade até o cutover. **Nada de apagar tabela, banco ou serviço na VM.**
> - Esta operação é **exclusivamente no DESTINO**, ou seja, no **Azure SQL Database** (`sql-prd-tk-cin-001`).
> - E mesmo no destino: apague o **DATABASE** `FIFA2026Tickets`, **NÃO o servidor** `sql-prd-tk-cin-001`. O servidor fica (mantém firewall, "Allow Azure services" e admin); só o banco dentro dele é apagado. Apagar o servidor por engano vira retrabalho.
>
> 📌 **Resumindo:** você só toca no **database de destino no Azure SQL**. **VM = intocada. Servidor Azure SQL = intocado. Só o database de destino é apagado.**

1. Portal → busque **`sql-prd-tk-cin-001`** → abra o **SQL server** (ícone de servidor, **não** o de database).
2. Menu lateral → **SQL databases**.
3. Clique no database **`FIFA2026Tickets`**.
4. No topo → **🗑️ Delete**.
5. Confirme: **marque a caixa** (e, se pedir, **digite o nome** `FIFA2026Tickets`) → **Delete**.
6. Aguarde sumir da lista **SQL databases** (~30 s).

**O próximo passo depende do caminho escolhido — e a diferença derruba aluno:**

| Caminho | O database destino precisa... |
|---|---|
| **A) Re-rodar o DMS** | ...existir **VAZIO** → depois de apagar, **crie de novo** um database vazio com o mesmo nome |
| **B) Importar o `.bacpac`** | ...**NÃO existir** → o Import **cria** o banco a partir do arquivo. Se já existir, o import **FALHA** |

##### Passo 4 (Caminho A) — Re-rodar o DMS

1. Recrie o database **vazio**: servidor `sql-prd-tk-cin-001` → **+ Create database** → **Database name** `FIFA2026Tickets` → mesmo servidor → mantenha o tier padrão → **Create**.
2. DMS → **Overview → New migration** e refaça o wizard da §5.4:
   - Source **SQL Server** / Target **Azure SQL Database** / mode **Offline**.
   - Origem `10.30.1.4`, login `adminsql`, marque **Trust server certificate**.
   - Destino `sql-prd-tk-cin-001.database.windows.net`, login `adminsql`.
   - ⚠️ **Marque "Migrate Missing schema"** (o destino está vazio de novo → obrigatório).
   - **Select all tables** → **Start migration**.
3. Volte ao **Monitor migrations** e acompanhe **tabela por tabela** até **todas** darem **Succeeded**.

##### Passo 5 (Caminho B) — Contorno com `.bacpac` (se o DMS insistir em travar)

Banco minúsculo (~3 MB) → rápido e confiável. Dispensa DMS/SHIR.

1. **Apague o database destino** (Passo 3) e **NÃO recrie** — o import cria o banco sozinho.
2. Portal → servidor `sql-prd-tk-cin-001` → **Import database**.
3. **Select backup** → aponte o `FIFA2026Tickets.bacpac` → **Database name** `FIFA2026Tickets` → confirme o tier → **OK**.
4. Acompanhe em **Import/Export history** do servidor até concluir → valide.

> Trade-off: o `.bacpac` não exercita o assessment de compatibilidade — mas destrava na hora.

##### Os 3 erros que mais derrubam o aluno

1. Achar que a tabela "sumida" falhou, quando o DMS **só pulou tabela vazia** → confira o **detalhe por tabela**, não a ausência.
2. **Esquecer de apagar o database destino** antes de re-rodar → conflito de schema/PK.
3. No `.bacpac`, **deixar o database criado** → import falha porque o banco já existe.

> 🧩 **Assessment de compatibilidade (produção/escala).** Em ambiente grande, **antes** da migração você roda o **Azure Migrate** (SQL assessment): descobre o estado, aponta **incompatibilidades**, recomenda **tier/sizing** e estima custo — é o passo que separa "migrou" de "migrou com segurança". Para este schema simples não há bloqueios.

> 🩹 **Atalho para DB pequeno (sem assessment):** se o tempo apertar, dá para importar o **`.bacpac`** direto no Portal (servidor `sql-prd-tk-cin-001` → **Import database**, apontando o `FIFA2026Tickets.bacpac`). É offline também, dispensa DMS/SHIR — mas **não valida compatibilidade**.

#### 5.6 Reapontar o backend para o Azure SQL (mantendo a VNet Integration)

Com os dados no Azure SQL, atualize o backend para o novo banco (a `vm-data` já pode ser desligada). A **VNet Integration permanece** — ela será o caminho de saída até o Private Endpoint na Fase 8:

1. `app-prd-tk-bend-cin-001` → **Connection strings** → edite `DefaultConnection`, mude o **Type** para **SQLAzure** e troque o **Value** pela string do Azure SQL:
   ```text
   Server=sql-prd-tk-cin-001.database.windows.net,1433;Database=FIFA2026Tickets;User Id=adminsql;Password=<senha-do-Azure-SQL-admin>;Encrypt=true;TrustServerCertificate=false
   ```
   **Apply** (reinicia sozinho).
   > 💡 Note o `TrustServerCertificate=false`: o Azure SQL tem **certificado válido** (diferente do SQL Server da VM, que era self-signed → `true`).
2. **NÃO remova a VNet Integration.** Para o teste desta fase, o Azure SQL é alcançado pelo **endpoint público + firewall** (Fase 5.1 — "Allow Azure services"). Em seguida, como **boa prática**, a **Fase 8** adiciona um **Private Endpoint** ao Azure SQL e **desliga o público** — e a VNet Integration (que fica) é justamente o caminho de saída do Web App até esse endpoint privado.

#### 5.7 Validar

```powershell
$BEND = "https://app-prd-tk-bend-cin-001.azurewebsites.net"
Invoke-RestMethod "$BEND/api/health/db"        # connected:true, agora apontando para .database.windows.net
(Invoke-RestMethod "$BEND/api/matches").matches.Count   # 104
```

> ✅ **Pronto quando:** `/api/health/db` conecta no `*.database.windows.net` e o app funciona **100% em PaaS**, com a `vm-data` **desligada**. As 3 VMs agora são história. 🎉🎉🎉
>
> ▶️ **Próximo passo (boa prática):** agora que o banco modernizou e foi validado, **tranque o acesso com Private Endpoint** (Fase 8) — o Azure SQL deixa de responder pela internet e passa a ser alcançado **só pela VNet** (via a VNet Integration que mantivemos).

---

### Fase 6 — Smoke test ponta a ponta (100% PaaS)

Teste do **seu computador**, com a internet real, na URL final (domínio ou `*.azurewebsites.net`).

#### 6.1 No navegador

- [ ] A **home** carrega (104 jogos)
- [ ] **Login** `admin@fifa2026.com` / `admin123`
- [ ] **Cadastre** um usuário novo → **login**
- [ ] **Compre um ingresso** → recebe o ingresso premium com **QR code**
- [ ] **Página de validação** do ingresso → "válido"
- [ ] **Painel admin** (vendas/usuários) abre

#### 6.2 PowerShell — validação automatizada

```powershell
$APP = "https://www.<seu-domínio>"   # ou https://app-prd-tk-fend-cin-001.azurewebsites.net

Invoke-WebRequest $APP -UseBasicParsing | Select-Object StatusCode      # 200
Invoke-RestMethod "$APP/api/health"                                      # OK (via proxy do front)
$body = @{ email='admin@fifa2026.com'; password='admin123' } | ConvertTo-Json
$r = Invoke-RestMethod "$APP/api/auth/login" -Method POST -ContentType 'application/json' -Body $body
$h = @{ Authorization = "Bearer $($r.token)" }
(Invoke-RestMethod "$APP/api/matches" -Headers $h).matches.Count          # 104
```

> 🏁 **Conseguiu?** Você migrou uma aplicação 3 camadas de **3 VMs** para **PaaS puro** — Web Apps + Azure SQL — com ferramentas assistidas, blue/green e cutover. **Muito bem!** 🎉

---

### Fase 7 — Decomissionar as VMs + comparação VM × PaaS

> 🧹 **Agora sim: apaga as VMs.** Com tudo validado em PaaS, o ambiente VM não serve mais a nada — é só custo.

#### 7.1 Apagar o Resource Group das VMs

Pelo **Azure Cloud Shell**:
```bash
az group delete --name rg-prd-tik-cin-001 --yes --no-wait
```

Isso apaga, em bloco: 3 VMs + 3 discos + 3 NICs + 2 NSGs + **2 VNets (com o peering)** + IPs públicos — em ambas as regiões. **O `rg-prd-tik-paas-cin-001` (PaaS) permanece** rodando o app.

> ⚠️ **Confirme o PaaS no ar ANTES de apagar.** Refaça o smoke test da Fase 6. Só apague as VMs depois que o app responder 100% por PaaS — esse é o ponto de não-retorno do blue/green.

#### 7.2 (Fim do evento) Apagar também o PaaS

Quando não precisar mais de nada:
```bash
az group delete --name rg-prd-tik-paas-cin-001 --yes --no-wait
```
Apaga os 2 Web Apps + o plano + o Azure SQL (+ o DMS, se você tiver criado). **Custo zero a partir daqui.**

#### 7.3 A lição: VM × PaaS lado a lado

| Dimensão | Cenário VM (guia anterior) | Cenário PaaS (este guia) |
|---|---|---|
| 🖥️ **Compute** | 3 VMs B2s que você opera | App Service Plan B1 gerenciado |
| 🩹 **Patch de OS** | **Seu problema** (Windows Update) | Plataforma faz por você |
| 🌐 **TLS/HTTPS** | Emitir + renovar à mão (90 dias) | **Certificado gerenciado**, renovação automática |
| 🔁 **Proxy reverso** | ARR "Enable proxy" (checkbox) | `applicationHost.xdt` (1 arquivo) |
| 🗄️ **Banco** | SQL Server que você instala/opera | Azure SQL: backup/HA/patch nativos |
| 🚀 **Deploy** | RDP + copiar arquivos + `iisreset` | Publicar (assistant/zip); reinício automático |
| 📈 **Escala** | Redimensionar a VM (downtime) | **Scale up/out** com clique |
| 💰 **Custo (24/7)** | ~$90/mês | ~$18/mês |
| 🧅 **Segurança** | NSG + jump host + você fecha tudo | Endpoint gerenciado + firewall → **rede privada na Fase 8** (Private Endpoints) |

> 🧠 **A grande sacada:** PaaS não é "melhor" em tudo de forma absoluta — VM dá **controle total** (e responsabilidade total). PaaS troca controle por **menos trabalho operacional**. Saber **quando usar cada um** é o que esta dupla de guias ensina.

> ✅ **Pronto quando:** o RG das VMs foi apagado, o app continua no ar em PaaS, e você consegue **explicar** as diferenças da tabela acima.

---

### Fase 8 — Rede privada: Private Endpoints + VNet Integration

> 🎯 **O passo final de produção.** Até aqui, front, API e banco têm **endpoints públicos** (com firewall, mas expostos). Agora você **fecha a porta**: só o **frontend** continua na internet; **API e banco** passam a viver **dentro da VNet**, alcançáveis só por **IP privado**. E o melhor — **sem tocar em uma linha de código da aplicação**.

> 🧩 **Quando fazer:** depois de validar o app 100% em PaaS (Fases 6-7). Adiciona ~$15/mês enquanto no ar (2 Private Endpoints + DNS) — **sem** plano extra (front e API no **mesmo plano**, compartilhando a VNet Integration). Provisiona, demonstra e derruba (Fase 7.2).

#### 8.1 O conceito-chave: inbound × outbound

Quase todo erro aqui vem de confundir as duas peças:

| Recurso | Direção | Para que serve | Efeito colateral |
|---|---|---|---|
| 🔌 **Private Endpoint** | **Inbound** (entrada) | IP privado para **receber** conexões | **Desliga o acesso público** do recurso |
| 🌐 **VNet Integration** | **Outbound** (saída) | Permite o Web App **alcançar** a VNet | Não muda como o app é acessado |

Em cada salto privado você precisa dos **dois**, em pontas opostas:

```
Front  ──[VNet Integration: saída]──▶  [Private Endpoint da API: entrada]  ──▶  API
API    ──[VNet Integration: saída]──▶  [Private Endpoint do SQL: entrada]  ──▶  SQL
```

> 💡 **Analogia:** o **Private Endpoint** é a *porta privada* (só abre pra dentro). A **VNet Integration** é o *crachá* que deixa o app **entrar no condomínio** (a VNet) para chegar até essa porta. E a **Private DNS Zone** (`privatelink.azurewebsites.net` / `privatelink.database.windows.net`) é o que faz o **FQDN público resolver para o IP privado** — sem ela, nada funciona (erro nº 1).

> 🧠 **Por que zero código?** O app continua pedindo os mesmos nomes (`app-...-bend.azurewebsites.net`, `sql-...database.windows.net`); o Private DNS só muda **para onde** eles resolvem. O certificado `*.azurewebsites.net` segue válido (mesmo nome).

#### 8.2 Estado-alvo e recursos

```
              Internet
                 │ 443  (ÚNICA porta pública)
                 ▼
   front (público) ──[VNet integ.]──▶ PE da API (privado) ──▶ API (público OFF)
                                          └─[VNet integ.]──▶ PE do SQL (privado) ──▶ SQL (público OFF)
```

Tudo na VNet que você já tem (`vnet-prd-inf-cin-001`, `10.20.0.0/16`):

| Recurso | Nome | Faixa / Observação |
|---|---|---|
| Subnet de Private Endpoints | `snet-prd-inf-pe-cin-001` | `10.20.5.0/24` · PE network policies **Disabled** |
| Subnet de integração (App Service) | `snet-prd-inf-appsvc-cin-001` | `10.20.3.0/24` · delegada `Microsoft.Web/serverFarms` · **a mesma da Fase 3.4** — front e API (mesmo plano) compartilham |
| Private Endpoint — API | `pe-prd-tk-bend-cin-001` | IP privado da API |
| Private Endpoint — SQL | `pe-prd-tk-sql-cin-001` | IP privado do SQL |
| Private DNS Zones | `privatelink.azurewebsites.net` · `privatelink.database.windows.net` | ligadas à VNet (Portal cria/associa) |

> 🧠 **Regra de ouro da ordem:** **abrir o caminho privado → validar → só então desligar o público.** Nunca tranque uma porta sem ter aberto a outra — é o que garante zero downtime.

> ⚠️ **A validação é de DENTRO da VNet.** Do seu PC (fora da VNet), o FQDN privado **resolve o IP público** → 403/timeout. Por isso valide sempre **de dentro**: pelo **próprio app** (`/api/health`, `/api/health/db` — o app resolve o nome privado de dentro) ou de uma **VM/Bastion na VNet**. Testar o DNS privado do seu micro **não funciona** e não significa que está errado.

#### 8.3 Preparar a rede (subnet de Private Endpoints)

Só falta a subnet dos **Private Endpoints** — a de **integração já existe** (`snet-prd-inf-appsvc`, criada na Fase 3.4) e **é reutilizada** (front e API estão no **mesmo plano** e compartilham essa integração).

Portal → `vnet-prd-inf-cin-001` → **Subnets** → **+ Subnet**:
- **`snet-prd-inf-pe-cin-001`** · `10.20.5.0/24` · **Network policies for private endpoints: Disabled** · **sem** delegação.

> 💡 A subnet de **integração** não pode ser a mesma dos **Private Endpoints** — por isso esta subnet de PE separada. **Não** é preciso criar outra subnet de integração: como front e API estão no mesmo plano, eles **compartilham** a integração existente da Fase 3.4.

#### 8.4 Private Endpoint do Azure SQL (público ainda ligado)

1. `sql-prd-tk-cin-001` → **Security → Networking → Private access** → **+ Create a private endpoint**.
2. **Name:** `pe-prd-tk-sql-cin-001` · sub-resource **sqlServer** · VNet `vnet-prd-inf-cin-001` · Subnet `snet-prd-inf-pe-cin-001`.
3. **Private DNS integration: Yes** → zona `privatelink.database.windows.net` (Portal cria e **liga à VNet**).

> ⏸️ **Não desligue o público do SQL ainda** — valide primeiro que a API alcança o IP privado (8.5). A API **já tem VNet Integration + Route All desde a Fase 3.4**, então não há o que adicionar.

#### 8.5 Validar o SQL privado e desligar o público

> A API **já está integrada à VNet (com Route All) desde a Fase 3.4** (integração permanente) — **nada a adicionar aqui**, só validar e trancar.

1. **Valide a resolução privada ANTES de desligar o público** — no **Kudu da API** (`https://app-prd-tk-bend-cin-001.scm.azurewebsites.net/DebugConsole`):
   - **DNS:** `nameresolver sql-prd-tk-cin-001.database.windows.net` → deve resolver para **IP privado (`10.x`)** (CNAME `...privatelink.database.windows.net`). **IP público** = falta **Route All** (3.4) ou a **zona DNS linkada à VNet**.
   - **Porta:** `tcpping sql-prd-tk-cin-001.database.windows.net:1433` → deve **conectar**.
   - **App:** `Invoke-RestMethod "$BEND/api/health/db"` → **connected** (via IP privado).
   > ⚠️ **Não use `ping`** no App Service — o sandbox **bloqueia ICMP** ("Unable to contact IP driver. General failure." é esperado). Use `nameresolver` e `tcpping`.
2. **Agora sim:** `sql-prd-tk-cin-001` → **Networking → Public access → Disable** → **Save**.
3. **Revalide** `/api/health/db` → continua connected. 🎉 Banco privado.

> 💡 O **host no `DefaultConnection`** **não mudou** — o mesmo FQDN agora resolve privado (VNet integ. + Route All + zona DNS linkada).

#### 8.6 Private Endpoint da API (público ainda ligado)

1. `app-prd-tk-bend-cin-001` → **Networking → Inbound → Private endpoints → + Add** → `pe-prd-tk-bend-cin-001` · sub-resource **sites** · VNet `vnet-prd-inf-cin-001` · Subnet `snet-prd-inf-pe-cin-001`.
2. **Private DNS integration: Yes** → zona `privatelink.azurewebsites.net`. O **Private DNS Zone Group** cria **automaticamente** os registros `A` do **app** *e* do **`scm`** (Kudu) apontando para o IP privado — por isso o **deploy/Kudu também fica privado** (ver 8.8). Continue usando o nome `*.azurewebsites.net` (o certificado é desse nome).

> ⏸️ **Não desligue o público da API ainda** — o front só a alcança privado depois do 8.7.

#### 8.7 Validar o caminho front → API privada e desligar o público

> O front está **no mesmo plano** da API, então **já usa a mesma VNet Integration** (do plano, da Fase 3.4) — **nada a adicionar**. Só confirme o **Route All** em `app-prd-tk-fend-cin-001` → **Networking → VNet integration**.

1. **Valide do Kudu do FRONT** (`https://app-prd-tk-fend-cin-001.scm.azurewebsites.net/DebugConsole`), com o **público da API ainda ligado**:
   - **DNS:** `nameresolver app-prd-tk-bend-cin-001.azurewebsites.net` → deve resolver **IP privado (`10.x`)** (CNAME `...privatelink.azurewebsites.net`).
   - **Porta:** `tcpping app-prd-tk-bend-cin-001.azurewebsites.net:443` → deve **conectar**.
   - **Ponta a ponta:** `Invoke-RestMethod "$APP/api/health"` → **ok** (o front proxiou para a API pelo IP privado; o `web.config` **não mudou**).
3. **Agora sim:** `app-prd-tk-bend-cin-001` → **Networking → Inbound → Public network access → Disabled** → **Save**.
4. **Confirme o bloqueio:** a URL da API **direto** da internet deve dar **403/timeout**; pelo **front** continua **200**.
5. 🔁 **Teste de estabilidade — reinicie os DOIS apps.** Portal → **Restart** no `app-prd-tk-fend-cin-001` **e** no `app-prd-tk-bend-cin-001`. Aguarde **~1-2 min** (propagação da rede privada) e **revalide**: `nameresolver` + `tcpping :443` (Kudu do front) → IP privado/conecta; e o smoke do app (`/api/health`, `/api/health/db`, login). _Isso prova que funciona "a frio" — a falha transitória que aparece **durante** a configuração some depois que o restart estabiliza._

#### 8.8 Validação + considerações operacionais

Smoke final (do seu PC): o app funciona pelo **front público**; a **API e o SQL não respondem pela internet** (403/timeout). 🔒

Ao privatizar, duas tarefas mudam — e isso é **esperado**:

| Tarefa | Agora (privado) |
|---|---|
| 🚀 **Deploy da API** | O **SCM/Kudu** fica privado → deploy de um **runner/VM na VNet**, ou liberar o SCM público à parte, ou reabrir o público temporariamente |
| 🗄️ **Gerir/importar o SQL** | Conectar de uma **VM na VNet** (ou Bastion), ou reabrir o **Public access** do SQL temporariamente + firewall do seu IP |

> 🔐 **Esse atrito é o ponto.** Gestão de recursos privados passa por jump host / Bastion / pipeline na VNet — o mesmo padrão da fase VM, agora no mundo PaaS. Segurança troca conveniência por superfície menor.

> ✅ **Pronto quando:** o app responde pelo **front público**, mas **API e SQL não respondem pela internet** — e você sabe explicar inbound (Private Endpoint) × outbound (VNet Integration).

---

### Fase 9 — Troubleshooting

| Sintoma | Causa provável | O que fazer |
|---|---|---|
| Front no Web App abre, mas `/api/*` dá **404/502** | Falta o `applicationHost.xdt` (proxy do ARR não habilitado) | Crie `site/applicationHost.xdt` (Fase 4.4) **em `site/`, não `wwwroot/`** + **Restart** |
| `/api/*` proxia, mas backend dá **500** | App Settings erradas, ou `web.config`/`node_modules` não vieram no deploy | Veja **Log stream** (Portal → backend → **Monitoring → Log stream**); confirme as App Settings (3.5); republique se faltou conteúdo |
| `/api/health/db` dá **ETIMEOUT/ESOCKET** (Fase 3) | VNet Integration/peering não roteia até a `vm-data` | Confirme a subnet delegada `Microsoft.Web/serverFarms` + **Route All** (3.4); peering das VNets `Connected`; NSG do banco libera `1433` de `10.20.0.0/16` |
| `/api/health/db` dá **ETIMEOUT** (Fase 5, já no Azure SQL) | Firewall do Azure SQL sem "Allow Azure services" | `sql-prd-tk-cin-001` → **Networking** → **Allow Azure services = Yes** |
| `/api/health/db` dá **ELOGIN** | `User Id`/`Password` da Connection String `DefaultConnection` não batem com o destino | Antes da Fase 5: `adminsql`/`Partiunuvem@2026` (VM). Depois: `adminsql`/senha do Azure SQL |
| App Service Migration Assistant **não acha o site** | Rodando fora da VM de origem, ou IIS parado | Instale e rode o assistant **dentro** da `vm-bend`/`vm-fend`; confirme o site no IIS Manager |
| Mudei App Setting e **nada mudou** | Cache de instância | App Settings reiniciam o app, mas force um **Restart** se preciso. (No App Service **não** existe `iisreset`.) |
| **DMS** falha ao conectar no source/target (Fase 5) | SHIR não registrado, ou firewall do Azure SQL bloqueia o IP da `vm-data` | Confirme o **nó do SHIR** ✅ no DMS (Integration runtime); libere o IP de saída da `vm-data` no firewall do Azure SQL; logins `db_datareader` (origem) / `adminsql` (destino) |
| **DMS** — `Next` indisponível ao selecionar tabelas (Fase 5) | Destino vazio e **"Migrate Missing schema"** não marcado | Marque **Migrate Missing schema** (5.4) — sem tabelas no destino, o DMS exige a migração de schema para prosseguir |
| Domínio customizado **não valida** | Registro `asuid` TXT/CNAME não propagou | `Resolve-DnsName asuid.www.<domínio> -Type TXT -Server 8.8.8.8`; aguarde a propagação e revalide |
| **Cadastro/login falham só no navegador** ("não foi possível criar a conta" / 500), mas **`curl` funciona** | **CORS:** `FRONTEND_URL` não bate com o `Origin` do navegador — quase sempre **barra `/` no fim** ou o domínio faltando | Ajuste `FRONTEND_URL` para `https://seu-dominio` **sem `/`** (4.6); `curl` engana porque não manda `Origin` |
| **(Fase 8)** `/api/*` dá **502/timeout** após desligar o público da API | Front sem **Route All**, ou zona `privatelink.azurewebsites.net` não linkada | Confirme VNet Integration do front + `WEBSITE_VNET_ROUTE_ALL=1` + zona linkada à VNet (8.7) |
| **(Fase 8)** `/api/health/db` dá **ETIMEOUT** após desligar o público do SQL | API sem VNet Integration/Route All, ou zona `privatelink.database.windows.net` não linkada | Reveja a **Fase 3.4** (integração/Route All da API) + a zona DNS do PE (8.4); valide pela 8.5 |
| **(Fase 8)** front **não alcança** o PE da API | **Route All** desligado no front, ou zona `privatelink.azurewebsites.net` não linkada à VNet | Ligue **Route All** no front; confirme a Private DNS Zone linkada; valide com `nameresolver` (deve dar IP privado) + `tcpping <api>:443`. _(Estar no mesmo plano que o front **não** impede — não há essa limitação.)_ |
| **(Fase 8)** PE criado mas o nome resolve **IP público** | Consulta feita **de fora** da VNet, ou zona DNS não linkada | Private DNS resolve **de dentro** da VNet; teste via `/api/health` do app, não do seu PC |
| **(Fase 8)** Private Endpoint **não cria** na subnet | Network policies de PE habilitadas | `snet-prd-inf-pe-cin-001` → desabilite *network policies for private endpoints* (8.3) |

> 📚 **Diagnóstico de banco:** o endpoint `/api/health/db` continua sendo o melhor sinal — ele devolve o erro real (`code`) e a config em uso, igual na fase VM. A diferença é que aqui você lê os logs no **Log stream** do Portal, não em arquivo na VM.

---

## 📊 6. Tabela de variáveis e segredos

**Anotações que você carrega da fase VM + as novas do PaaS** (mantenha fora do Git):

| Onde | Nome | Origem / Exemplo |
|---|---|---|
| 🔢 | *IP_DB* | IP privado da `vm-data` (`10.30.1.x`) — usado na **Connection String** do backend **até a Fase 5** |
| 🔐 | *SQL/VM adminsql* | `adminsql` / `Partiunuvem@2026` — origem do banco (VM) |
| 🔐 | *Azure SQL admin* | `adminsql` / *(senha que você criou na Fase 5.1)* — destino do banco (PaaS) |
| 🔐 | *JWT_SECRET* | a mesma string longa da fase VM |
| 🌐 | *Backend Web App* | `https://app-prd-tk-bend-cin-001.azurewebsites.net` |
| 🌐 | *Frontend Web App* | `https://app-prd-tk-fend-cin-001.azurewebsites.net` |
| 🌐 | *Azure SQL FQDN* | `sql-prd-tk-cin-001.database.windows.net` |
| 🌐 | *Domínio final* | `https://www.<seu-domínio>` (Fase 4.5) |
| 🔐 | *Certificado das VMs (.pfx)* | exportado da fase VM → importado no Key Vault `kv-prd-tk-cin-001` (Fase 4.5) |

**Config do `app-prd-tk-bend-cin-001`** (substitui o `.env` da VM):

- **App Settings:** `JWT_SECRET` · `JWT_EXPIRES_IN` · `FRONTEND_URL` · `WEBSITE_NODE_DEFAULT_VERSION`
- **Connection strings:** `DefaultConnection` (Type **SQLServer** na VM → **SQLAzure** na Fase 5) — substitui `DB_SERVER`/`DB_PORT`/`DB_USER`/`DB_PASSWORD`/`DB_NAME`. Requer o ajuste no `database.js` (Fase 3.5b).

> 🔒 **Regra de ouro (continua valendo):** segredo nunca vai para o código nem para o repositório. Aqui eles saíram do `.env` na VM e foram para as **App Settings / Connection strings** do Web App — melhor, mas ainda em texto na configuração. _Próximo nível (§7):_ **Key Vault + Managed Identity**, onde o app lê o segredo sem nunca tê-lo na config.

---

## 🛡️ 7. Evolução (o "próximo nível" do PaaS)

> 🧠 **Tópico de aprendizado — não é passo do workshop.** O que você montou **funciona e ensina a jornada**. Mas, como sempre, o arquiteto pergunta: *"o que falta para produção de verdade?"*

O ambiente PaaS já entrega backups, HA e patch gerenciados. A **rede privada** (Private Endpoints + VNet Integration) já foi incorporada como **Fase 8** desta jornada. Um time de produção ainda adicionaria:

1. **🔐 Azure Key Vault + Managed Identity** — tirar a senha do `DefaultConnection` e o `JWT_SECRET` da config (Connection strings / App Settings). O Web App ganha uma **identidade gerenciada** e lê os segredos do Key Vault via *reference* — sem senha em lugar nenhum visível.
2. **📊 Application Insights** — telemetria de requisições, falhas e performance do app, sem instalar agente. Você "enxerga" o app em produção.
3. **🚦 Front Door + WAF** — um Front Door com WAF na borda do frontend, filtrando ataques antes de chegar no app (a API e o banco já estão privados desde a Fase 8).
4. **🤖 CI/CD com GitHub Actions (OIDC)** — em vez de publicar pelo assistant/zip à mão, um pipeline faz **build + deploy** a cada push, com autenticação sem segredo (OIDC). Com a API privada (Fase 8), o deploy passa a sair de um **runner na VNet**. _(Os workflows já existem no repo — veja `.github/workflows/`.)_

> 🧠 **Lembre do escopo:** estes itens são o **endurecimento e a automação** adicionais — assunto de uma próxima etapa. A jornada **VM → PaaS** (Fases 0–7) + a **rede privada** (Fase 8) você acabou de completar.

---

> 🏁 _Documento vivo — atualizado conforme o evento se aproxima (nomes globais finais, domínio, contagens). **Do gramado para a nuvem: bola rolando!**_ ⚽🏆☁️
