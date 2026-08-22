# Plano: Detecção do Old Input System e Robustez de Input no RewiredHelper

Status: **PLANEJAMENTO — não implementado.**
Origem: pedido do usuário em 2026-07-16, no projeto `NordStormSolitaire`, para investigar como o Rewired se relaciona com o Unity Input System (novo) e desenhar uma forma do `RewiredInputManager` detectar o uso do Input System antigo e oferecer migração.

---

## 1. Contexto e motivação

A Unity está depreciando o **Input Manager antigo** (`UnityEngine.Input`, também chamado de "Old Input System") em favor do pacote **Input System** (`com.unity.inputsystem`, "New Input System"). Isso é controlado em:

```
Project Settings → Player → Configuration → Active Input Handling
```

com três valores possíveis (`PlayerSettings.GetActiveInputHandler()` / `SetActiveInputHandler(int)`):

| Valor | Significado |
|---|---|
| `0` | Input Manager (Old) — modo atual do `NordStormSolitaire` (`ProjectSettings.asset:1197 → activeInputHandler: 0`) |
| `1` | Both |
| `2` | Input System Package (New) |

Se um dia a Unity remover/forçar a desativação do modo `Old` (ou se o usuário mudar para `New` isolado sem preparação), **qualquer chamada direta a `UnityEngine.Input.*` para de funcionar silenciosamente** — sem erro de compilação, sem exceção, apenas retornando valores neutros (`false`, `0`, listas vazias). Isso é o tipo de bug que só aparece em produção, num build específico, e é difícil de depurar.

## 2. O que a documentação oficial do Rewired diz

Fonte: https://guavaman.com/projects/rewired/docs/UnityInputSystem.html (consultada nesta sessão via WebFetch)

Pontos-chave:

1. **Rewired suporta o Input System novo como fonte opcional** desde a versão 1.1.63.0, mas com limitações importantes.
2. Em praticamente todas as plataformas, **o suporte a controles físicos (joystick/gamepad) do Rewired é nativo** — não depende de `UnityEngine.Input` nem do pacote Input System. Isso significa que joystick/gamepad **não é afetado** por nada deste plano.
3. Para **teclado, mouse e touchscreen**, o Rewired por padrão usa a API legada da Unity. Para trocar isso pelo pacote Input System, é preciso configurar **"Preferred Unity Input Source" → "Input System"** dentro do próprio **Rewired Input Manager** (asset de configuração do Rewired, não é o `PlayerSettings` da Unity).
4. Essa troca de fonte **exige também** que `Active Input Handling` esteja em `New` ou `Both` no Player Settings — as duas configurações trabalham juntas; mudar só uma não é suficiente.
5. **Quando configurado para usar o Input System, o Rewired deixa de depender de `UnityEngine.Input`, mas passa a depender do pacote Input System em si** — ou seja, troca um acoplamento por outro, não elimina o acoplamento.
6. **Recomendação explícita e atual da própria Rewired**: *"Due to numerous issues found in Unity Input System across many platforms, it is currently recommended to use the old Unity Input Manager, not Unity Input System as the Preferred Unity Input Source for best results."*
7. A doc lista bugs extensos e específicos por plataforma no pacote Input System (versões 1.12.0–1.19.0), incluindo: falha de reconexão de controle, hat switch incorreto, vibração não funcionando, e input não pausando corretamente em background — em Windows, Android, iOS, PS4 e PS5. Esses bugs são da Unity, não do Rewired, e não podem ser corrigidos por nós.

### Conclusão prática desta seção

**Não faz sentido, hoje, migrar o "Preferred Unity Input Source" do Rewired para "Input System"** — seria trocar um sistema estável por um com bugs documentados e ativos, contrariando a própria recomendação do fabricante. A ação correta é diferente do que foi cogitado inicialmente na conversa: não é "migrar tudo pro novo", é **preparar o projeto para não quebrar quando/se o Old for removido, sem trocar de backend agora**.

## 3. Duas dependências distintas em jogo (não confundir)

Este é o ponto mais importante do plano — a conversa inicial misturou os dois:

### 3.1. Dependência do **Rewired** (indireta, já gerenciada pelo próprio Rewired)
Controlada pelo "Preferred Unity Input Source" dentro do Rewired Input Manager. Hoje deve continuar em **"Unity" (legado)**, conforme recomendação oficial da seção 2. Não requer nenhuma ação nossa — é uma configuração que já existe no asset do Rewired e não precisamos automatizar sua troca (e não devemos incentivar trocá-la, dado os bugs documentados).

### 3.2. Dependência do **RewiredHelper (nosso pacote)** — esta é a parte que realmente precisa de ação
O `RewiredInputManager.cs` e o `RewiredCustomController_AndroidRemote.cs`, dentro do próprio `UnityRewiredHelper`, chamam `UnityEngine.Input` **diretamente**, por fora de qualquer API do Rewired. Essas chamadas são um acoplamento nosso, não do Rewired, e **não são afetadas nem resolvidas** pela configuração do "Preferred Unity Input Source" — elas quebram sempre que `Active Input Handling` deixa de incluir `Old`, independentemente de como o Rewired está configurado.

Pontos exatos mapeados nesta sessão (via grep em `K:\Games\Open Source\UnityRewiredHelper`):

**`Runtime/RewiredInputManager.cs`:**
| Linha | Chamada | Uso |
|---|---|---|
| 131, 136 | `Input.touchCount`, `Input.touches[0].phase` | Propriedades estáticas `anyButton`/`anyButtonNow` |
| 301 | `Input.GetKeyDown(KeyCode.Escape)` | `HandleEscapeButtons()` |
| 320 | `Input.GetKeyDown(KeyCode.Return)` | `HandleEscapeButtons()` |
| 432, 435 | `Input.GetAxis("Mouse X"/"Mouse Y")` | Detecção de movimento de mouse em `HandleInputSystem()` |
| 459-460 | `Input.GetAxis("Mouse X"/"Mouse Y")` | Fallback de troca Joystick→PC por movimento de mouse |
| 461 | `Input.GetMouseButtonDown(0/1/2)` | Mesmo fallback |
| 462 | `Input.anyKeyDown` | Mesmo fallback |
| 626-629 | `Input.GetKey(KeyCode.Up/Down/Left/RightArrow)` | `CanActivateAndroidCursor()` |

**`Runtime/RewiredCustomController_AndroidRemote.cs`:**
| Linha | Chamada | Uso |
|---|---|---|
| 89-96 | `Input.GetKey(...)` para setas, JoystickButton0, Escape, Menu, KeypadEnter/Return | Simulação de controle via teclado remoto Android |

Nenhum outro arquivo do pacote usa `UnityEngine.Input` diretamente (confirmado por grep em todo `K:\Games\Open Source\UnityRewiredHelper`).

## 4. O que o `NordStormSolitaire` (jogo) também usa, fora do RewiredHelper

Fora do escopo do pacote, mas relevante para o diagnóstico do editor mostrar o quadro completo:

- `Assets/_Game/Scripts/Main.cs`
- `Assets/_Game/Scripts/Level/Level.cs`
- `Assets/_Game/Scripts/Utils/DialogControl.cs`
- `Assets/_Game/Scripts/Credits.cs`
- `Assets/_Game/Scripts/LevelEditor/LevelEditor.cs`
- `Assets/_Game/Scripts/PublisherSplash.cs`

Esses arquivos usam `Input.*` e **não serão tocados por este plano** — são código do jogo, não do pacote reutilizável. Ver seção 8 (fora de escopo) sobre por que isso é aceitável.

## 5. Estratégia proposta

### 5.1. Não recomendar troca de `Active Input Handling` para `New` isolado
Deve permanecer desencorajado enquanto (a) os bugs da seção 2 não forem resolvidos pela Unity/Rewired e (b) os scripts do jogo (seção 4) não forem migrados. `New` isolado quebraria tanto os scripts do jogo quanto qualquer chamada residual a `UnityEngine.Input` no RewiredHelper que não tenha sido refatorada.

### 5.2. Recomendar `Both` como estado-alvo seguro
`Both` mantém 100% de compatibilidade com o comportamento atual (Old continua ativo) e evita qualquer aviso/erro de "Input System package instalado mas não habilitado" que a Unity mostra em alguns contextos (ex: ao adicionar pacotes que dependem do Input System, como certas integrações de UI Toolkit). É uma mudança de baixo risco e reversível a qualquer momento.

### 5.3. Refatorar as chamadas do RewiredHelper (seção 3.2) para APIs do Rewired
Objetivo: essas chamadas passam a usar `Player.GetButtonDown(...)`, `ReInput.controllers.Mouse`, `ReInput.controllers.Keyboard`, `ReInput.touch` — que são **agnósticas ao backend** (funcionam igual com Preferred Unity Input Source = Unity ou = Input System, e com Active Input Handling = Old, New ou Both). Isso não é "migrar pro New" — é parar de depender de qualquer configuração de Active Input Handling, ponto.

Mapeamento proposto chamada-a-chamada:

| Chamada atual | Substituição proposta | Observação |
|---|---|---|
| `Input.touchCount` / `Input.touches[0]` | Já existe `ReInput.touch` usado em paralelo no mesmo arquivo (`HandleTouchInput()`) — consolidar tudo nele, remover o uso de `Input.touches` das propriedades `anyButton`/`anyButtonNow`. | Elimina duplicidade de fonte de touch, não só a dependência. |
| `Input.GetKeyDown(KeyCode.Escape)` / `Input.GetKeyDown(KeyCode.Return)` | Ações Rewired dedicadas (ex.: `UICancel` / `UISubmit`) mapeadas no Input Manager do Rewired para Escape/Return, lidas via `Player.GetButtonDown("UICancel")` etc. | Mais correto que hoje: respeita rebind do jogador, hoje a tecla é fixa mesmo se o jogador remapear Escape. Requer criar as duas Actions no `.rewiredInputManager` asset (mudança de configuração do Rewired, não só de código). |
| `Input.GetAxis("Mouse X"/"Mouse Y")` (4 ocorrências) | `ReInput.controllers.Mouse.screenPositionDelta` (ou eixo equivalente exposto pela classe `Rewired.Controller` do tipo Mouse) | Validar API exata na versão do Rewired usada pelo projeto antes de implementar — nome do membro pode variar entre versões do SDK. |
| `Input.GetMouseButtonDown(0/1/2)` | `ReInput.controllers.Mouse.GetButtonDown(0/1/2)` | Mapeamento direto, sem necessidade de Action nova. |
| `Input.anyKeyDown` | `ReInput.controllers.Keyboard.anyKeyPressed` (ou `Player.GetAnyButtonDown()`, a definir qual expressa melhor a intenção original) | Intenção original é "houve *qualquer* input de teclado" — checar qual API do Rewired reproduz isso sem incluir mouse/joystick. |
| `Input.GetKey(KeyCode.Up/Down/Left/RightArrow)` em `CanActivateAndroidCursor()` | `ReInput.controllers.Keyboard.GetKey(KeyboardKeyCode.UpArrow)` etc. | Direto, mesma semântica. |
| `Input.GetKey(...)` em `RewiredCustomController_AndroidRemote.cs` (8 ocorrências) | Mesmo padrão: `ReInput.controllers.Keyboard.GetKey(...)` | Esse arquivo já é uma ponte para o sistema Rewired (`SetButtonValue`), então faz sentido também ler do lado Rewired. |

**Nota de risco:** a API exata do Rewired para mouse (`screenPositionDelta` vs outro nome) precisa ser confirmada na versão instalada antes da implementação — não foi verificada nesta sessão de planejamento, só a existência do padrão geral via `ReInput.controllers.Mouse`.

### 5.4. Diagnóstico no editor (`RewiredInputManagerEditor` + `DefaultSetupGenerator`)

Dois cards novos no bloco "🛠️ SETUP DIAGNOSTIC & STATUS CHECKER" (mesmo padrão visual de `DrawCheckResult` já usado no arquivo), **sem confundir os dois eixos da seção 3**:

**Card A — "Active Input Handling"**
- Lê `UnityEditor.PlayerSettings.GetActiveInputHandler()` via reflection ou API pública (checar se é pública na versão do Editor usada; se não, usar `SerializedObject` sobre o asset `ProjectSettings/ProjectSettings.asset`, mesmo padrão já usado no arquivo para inspecionar configs).
- `0` (Old isolado) → aviso: "Active Input Handling está isolado em Old — sem fallback caso a Unity remova o suporte legado. Recomendado: Both." Botão **"Definir como Both"** chama `DefaultSetupGenerator.SetActiveInputHandlingToBoth()`.
- `1` (Both) → ✅ ok.
- `2` (New isolado) → ⚠️ erro/alerta forte: "New isolado detectado — todas as chamadas diretas a UnityEngine.Input no RewiredHelper (ver Card B) e nos scripts do jogo vão falhar silenciosamente." Sem botão de correção automática aqui (reverter para Both é a ação seguidilha, mas não deve ser forçado sem o usuário entender o motivo).

**Card B — "RewiredHelper usa UnityEngine.Input diretamente"**
- Estático (não depende de scan de projeto) enquanto a Fase 6.2 não for implementada — mostra a lista fixa dos pontos da seção 3.2 mapeados nesta versão do pacote, com um link/nota "corrigido a partir da versão X.Y.Z" assim que a Fase 6.2 for feita.
- Antes da Fase 6.2: card informativo (cor âmbar, não vermelho — não é um erro de setup do usuário, é uma dívida técnica do pacote).
- Depois da Fase 6.2: card muda para ✅ automaticamente (não há mais chamadas a `UnityEngine.Input` no pacote — o card pode inclusive ser removido do editor nessa versão, já que deixa de haver algo a diagnosticar).

**Card C (opcional, informativo apenas) — "Scripts do jogo usando UnityEngine.Input"**
- Roda um `Grep`-like scan (via `System.IO` + regex, ou `AssetDatabase.FindAssets("t:Script")` + leitura de conteúdo) por `Input\.` em `Assets/**/*.cs`, **excluindo** `Packages/` e o próprio RewiredHelper.
- Lista os arquivos encontrados como hint informativo (cor cinza/dim, sem botão de correção — está fora do escopo do pacote automatizar edição de código do jogo).
- Esse card é opcional e pode ficar para uma iteração posterior; não é bloqueante para o valor central do plano.

### 5.5. Novo método em `DefaultSetupGenerator.cs`

```
SetActiveInputHandlingToBoth()
```
- Usa `PlayerSettings.SetActiveInputHandler(1)` (Both) via reflection se o método não for público na versão do Editor alvo, ou a API pública equivalente se disponível.
- Não instala nem exige o pacote `com.unity.inputsystem` — `Both` funciona mesmo sem o pacote instalado (a Unity só exige o pacote presente para o modo `New`); confirmar esse comportamento durante a implementação antes de assumir.
- Após mudar o handler, a Unity pode pedir reload de domínio — tratar como as outras ações do arquivo (`Undo.RegisterCreatedObjectUndo` já é o padrão para reversibilidade; para uma ProjectSetting, `Undo.RecordObject` não se aplica da mesma forma — avaliar se vale usar `SerializedObject` sobre o asset de ProjectSettings para ganhar Undo, ou aceitar que é uma ação não anulável via Ctrl+Z, só reversível manualmente).

## 6. Fases de execução (quando for implementar)

1. **Fase 1 — Diagnóstico apenas (sem refactor ainda)**: Card A + Card B (estático) no editor, incluindo o botão "Definir como Both". Não altera nenhum código runtime. Baixo risco, testável isoladamente.
2. **Fase 2 — Card C opcional**: scan dos scripts do jogo, puramente informativo.
3. **Fase 3 — Confirmar API exata do Rewired para mouse delta** na versão instalada (`ReInput.controllers.Mouse.*`), antes de codificar a Fase 4 — evitar assumir nome de membro errado.
4. **Fase 4 — Refactor runtime** (seção 5.3), arquivo por arquivo, com teste manual em Play Mode após cada arquivo:
   - `RewiredInputManager.cs`
   - `RewiredCustomController_AndroidRemote.cs`
5. **Fase 5 — Criar as Actions `UICancel`/`UISubmit`** no Input Manager do Rewired (asset de configuração), se ainda não existirem, e re-testar Escape/Return.
6. **Fase 6 — Atualizar Card B** para refletir que o pacote não depende mais de `UnityEngine.Input` (ou remover o card).
7. **Fase 7 — Testes de regressão manuais** (seção 7) com as 3 combinações de Active Input Handling.
8. **Fase 8 — Bump de versão** do `com.wagenheimer.rewiredhelper` (SemVer minor, por ser mudança de comportamento interno sem quebra de API pública) + sync do `packages-lock.json` no `NordStormSolitaire` + `CHANGELOG.md`.

## 7. Plano de teste manual (Play Mode, por combinação de Active Input Handling)

Para cada valor (`Old`, `Both`, `New`), verificar em Play Mode:
- [ ] Cursor customizado aparece e segue o mouse.
- [ ] Cursor customizado aparece e segue o joystick (Player Mouse).
- [ ] Escape fecha o modal/dialog ativo (teclado e botão Back do controle).
- [ ] Return confirma o botão OK ativo (teclado e controle).
- [ ] Troca automática entre PC/Joystick ao mover o mouse enquanto joystick está ativo.
- [ ] Touch (se aplicável na plataforma de teste) esconde o cursor e não interfere no fluxo acima.
- [ ] Android Remote (`RewiredCustomController_AndroidRemote`) — setas, OK, Back, Menu, Enter — se houver ambiente de teste disponível.

Antes da Fase 4 (refactor), esperado: `Old` e `Both` passam, `New` isolado falha em cursor/Escape/Return (comportamento atual, documentando o bug antes de corrigir). Depois da Fase 4: as três combinações devem passar igualmente — essa é a definição de "pronto" do plano.

## 8. Explicitamente fora de escopo

- **Migrar o "Preferred Unity Input Source" do Rewired para Input System** — desaconselhado pela própria documentação do Rewired (seção 2), não faz parte deste plano.
- **Editar automaticamente os 6 scripts do jogo** listados na seção 4 — são código específico do `NordStormSolitaire`, não do pacote reutilizável `UnityRewiredHelper`. Ficam apenas como item informativo (Card C, opcional).
- **Forçar `Active Input Handling = New`** em qualquer fluxo automatizado — só `Both` é oferecido como ação com botão; `New` fica só como alerta informativo caso já esteja configurado assim externamente.

## 9. Riscos e pontos em aberto para validar antes de codificar

1. Nome exato do membro de delta de mouse na API `Rewired.Controller`/`Mouse` da versão do SDK usada pelo projeto — não confirmado nesta sessão, só o padrão geral de que existe via `ReInput.controllers.Mouse`.
2. Se `PlayerSettings.SetActiveInputHandler` é público na versão do Unity Editor do projeto, ou se será necessário reflection (como já é feito em outros pontos do `RewiredInputManagerEditor`, ex. `FindTypeByName`).
3. Se `Both` funciona sem o pacote `com.unity.inputsystem` instalado, ou se a Unity exige o pacote presente mesmo para o modo `Both` — confirmar antes de prometer "não precisa instalar nada" no texto do card.
4. Se as novas Actions `UICancel`/`UISubmit` (Fase 5) colidem com nomes de Actions já existentes no asset de Input Manager do Rewired usado pelo `NordStormSolitaire` — checar antes de criar.
