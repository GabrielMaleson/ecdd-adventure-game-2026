# ECDD Adventure Game — Design Document

This file is the living design reference for all collaborators (human and AI). Update it as the design evolves.

---

## REGRA PARA CLAUDE

**Leitura e escrita liberadas no projeto todo** — `.cs`, `.yarn`, cena, prefab, animator, meta, import settings.

- **Unity precisa estar fechado, ou pelo menos sem alterações não salvas**, antes de o Claude editar `.unity` / `.prefab` / `.controller`. O Unity guarda a cena em memória: se salvar por cima depois, a edição do Claude some.
- **Edição de cena/prefab é sempre por YAML na mão.** Antes de mexer, confira que o arquivo está limpo no git, para dar `git checkout` se quebrar.
- **Continua valendo:** mudança em sistema central (movimento, colisão, diálogo, interação) o Claude descreve e pergunta antes — não pelo tipo do arquivo, mas pelo alcance da mudança.

---

## REGRA PARA CLAUDE — TEXTO E DIÁLOGO

**"Corrigir gramática" significa SÓ gramática.** Nunca mexer em estilo, ritmo, voz de personagem ou pontuação opcional. Vale para `.yarn`, roteiro e qualquer fala do jogo.

**É erro, pode corrigir:** construção que não existe no idioma (`sometime now` → `for some time now`), concordância, hífen em posição errada (`ages-old` depois do verbo → `ages old`), palavra trocada.

**NÃO é erro, nunca tocar:**
- vírgula antes de vocativo em diálogo — `messing with you man` está certo; a ausência dá frase corrida, de propósito
- pontuação em texto que representa alguém escrevendo em pânico — o gibberish do Elder Amos não leva vírgula, ninguém pontua enquanto surta num caderno
- travessão único marcando quebra brusca de pensamento — **não precisa fechar**
- `Ok` vs `Okay`, `they would` vs `they'd`, alongamentos (`Naaah`)
- vírgula depois de oração introdutória em fala informal

Na dúvida entre erro e escolha, **não mexer** — perguntar. Ao entregar, listar cada mudança e por que é erro de gramática. Se a justificativa for "fica melhor", "é mais comum" ou "é o padrão do resto do texto", não é gramática: é estilo, e fica de fora.

---

## What Is This Game

2D top-down mystery/horror narrative adventure. Point-and-click interactions, puzzle-solving, investigation. Third person. Target length: **1–2 hours**.

The game is about **characters and relationships**. The mist and the entity are backdrop. Funny moments, connection, and weight must coexist with the hopelessness — all so the final choice means something.

---

## The World

A small isolated village surrounded by mist. The mist has always been there — it's part of the village's identity. During the game it gradually thickens and people start going crazy, then disappear.

---

## The Backstory (backdrop — revealed in pieces through Elder's house and puzzle locations)

The mist is not weather. It is the dying breath of an enormous ancient entity buried beneath the village. The creature is so ancient and vast that its death takes centuries, possibly millennia. The mist the villagers always knew was literally its final exhales.

The MC's younger brother found the source — a tree (the place where the breath comes from). He became the first victim. But his attachment to his brother kept him from dying completely. A fragment of his consciousness survived. He became literally part of the mist — and in doing so, he inadvertently hindered the breath from gaining power.

When the breath grew stronger, he woke up as **The Fragment**.

The dying entity, as it absorbs more consciousness from villagers, begins to intuitively sense it might not have to die — it can be reborn. It is not evil. It is not good. It is just an ancient creature finding out it may survive after all.

This lore is told in fragments (pun intended) — sketches, notes, and environmental clues across the Elder's house and the three puzzle locations.

---

## Characters

### MC (Protagonist)
Grew up in the village. Lost his parents, then his younger brother. Seemingly unaffected by the mist. Player character.

### O Fragmento (The Fragment)
A ghostly creature that reveals itself to the MC early on. Helps him. Connected to the mist. Doesn't know who or what it is at first.

**The Reveal (Act 3):** The Fragment is a fragment of the MC's younger brother's consciousness. His attachment to his brother let him resist the force. He is the only known survivor of the mist — and he is literally part of it.

The possession mechanic reinforces the weight of this revelation: the player has been using their dead brother's powers the whole time.

### O Amigo Próximo (The Close Friend — male NPC)
Knew the MC since childhood. Reliable, warm. The village anchor.

Arc: Sent to one of the three puzzle locations. Begins acting erratic. Is taken by the mist at the third site.

### A Forasteira (The Outsider — female NPC)
Came from outside the village(?). Was already investigating something when the game starts. Curious, pragmatic.

Arc: Instead of going to her assigned location alone, she waits for the MC at his chosen spot — she thinks splitting up is dumb. She witnesses Fragment during a possession puzzle. Is the last NPC standing, taken by the mist near the end.

---

## Game Flow

1. **Village intro** — Player starts in the middle of the village. Life feels normal (people talking, kids playing). Talks to the two NPCs and develops early relationship beats. Briefly meets the Elder (idle NPC, no deep dialogue). Notes the mist getting thicker.

2. **Home → Fragment** — MC goes home and encounters the Fragment for the first time.

3. **Commotion → Elder gone** — Something's wrong outside. The Elder has disappeared.

4. **Elder's house** — MC searches and finds clues pointing to a location that may have answers. First pieces of entity lore here.

5. **First mini-dungeon** — MC goes to the indicated location. First introduction of the possession mechanic. More lore fragments.

6. **Three sites** — Three locations in the village hold important answers (lore + puzzle content). MC tells the two NPCs to investigate; each supposedly goes to a different one while MC takes the third.
   - The Outsider ignores the plan and waits for the MC at his location.
   - Fragment is revealed to her here — possession is required to solve the puzzles, so she sees it.
   - At the Close Friend's location, he's already starting to act strange.
   - Both NPCs stay at the puzzle area until MC finishes (possession is needed, so Fragment can't be hidden).
   - At the third site, the Close Friend is taken by the mist.
   - After the third site, the Outsider starts to crack. She is taken by the mist.

7. **Endgame** — Just MC and the Fragment. They advance to the center of the mist. Final puzzles. The Fragment's true identity is revealed.

8. **Final choice.**

**Mist progression note:** The mist gradually thickens from mid-game onward. Villagers become progressively more erratic, then are absorbed. By the end, only MC and Fragment remain.

---

## Mechanics

### Movement
Top-down, 4-directional. MC walks around the village and dungeon areas.

### Interaction / Investigation
Point-and-click style. Examine objects, talk to NPCs, collect clues.

### Possession
The Fragment can possess the MC to interact with the world in ways the MC alone cannot — primarily manipulating objects at a distance, including heavy ones. Required for several puzzles.

This mechanic carries narrative weight: the player is literally being controlled by/merging with their dead brother.

Design note: This will be challenging to implement well. Boulder puzzle is one sketched use case.

---

## Final Choice

The Fragment — now revealed as the brother — discovers he can take two paths.

The moment is a private, intimate final conversation between the MC and his brother.

**Choice 1 — Sacrifice:** The brother destroys himself, using his own energy to neutralize the force. Everything the mist took is freed. The village is saved. The brother disappears forever.

**Choice 2 — Return:** The brother absorbs the accumulated energy of the village. He is reborn and destroys the force. But the village is sacrificed.

---

## Design Pillars

- **Characters first.** The entity is backdrop. The emotional core is the MC's relationships — with the Fragment (his brother), the Outsider, and the Close Friend.
- **Both sides must matter.** The player must feel attached to the village/NPCs *and* to the Fragment so the final choice has real weight.
- **Tonal balance.** Funny moments and warmth belong here despite the setting. Don't let the horror crowd out the humanity.
- **Lore in pieces.** Entity backstory is told gradually through environmental storytelling — don't front-load it.

---

## Open Design Questions

- Possession mechanic implementation (needs design work)
- Individual puzzle designs for the three sites + endgame
- Boulder puzzle (possession use case — sketch exists)
- NPC names (currently placeholder: Amigo Próximo, Forasteira)
- Elder's house — what specific clues / objects tell the entity story

---

## Sokoban System (implemented — working, `Assets/Scripts/Player/`)

Grid-based crate puzzle. Logic is grid-based; the player moves freely.

**Core idea:** the grid is the single source of truth. `PuzzleGrid` (one per scene, holds the `BelowCliff` tilemap) does all world↔cell conversion — no tile size or grid origin is typed by hand anywhere. Everything that lives on the grid measures from its **visible sprite centre**, not its pivot, so "looks aligned" and "is aligned" never disagree.

**Scripts:**
- `PuzzleGrid` — the lattice (scene object; drag the ground tilemap in once). Also draws the grid gizmo (`Show Grid`: `Always` / `When Grid Selected` / `Never`) and an occupancy debug view (`Show Occupancy`).
- `GridObject` (base) — visible-centre logic + editor Snap. `[DisallowMultipleComponent]`.
- `GridOccupant` (base) — owns a cell in a shared occupancy map. Crate-vs-anything blocking is one dictionary lookup, no physics probe.
- `PushableCrate` — the crate. Push decided by: player TOUCHING (collider distance, for realistic feel) + player on the correct SIDE, by cell row/column (no collider-bounds math). Slides one cell, all-or-nothing.
- `CrateTarget` — the rug/goal. Fires `onAllTargetsCovered` when every target cell holds a crate.
- `GridObstacle` — static blocker on the grid.
- `ObstacleGroup` + `StatueSwitch` — the statue turns obstacle groups 90° clockwise. Which obstacles turn = which `GridObstacle`s are CHILDREN of the `ObstacleGroup` (parent = turns, unparented = static). Group's own object is the pivot cell. Statue activated with **E** while the player is in its trigger. **Not yet tested in Play.**
- `PuzzleUndo` — test-build undo, press **Z**. Reverts the last board action (crate push OR statue rotation) LIFO; a crate push restores both the crate and the player's position (to be themed as the Fragment pulling him back). Put one in the scene. Not yet tested in Play.
- `Pickup` — collectible item (key, lore fragment). `Grab Mode`: `OnTouch` (walk onto it) or `PressE` (in range + E). On pickup it swaps GameObjects (`hide[]` off, `show[]` on) — e.g. grid crypt → non-grid crypt — hides itself, and fires an optional `onCollected` UnityEvent. Needs a trigger Collider2D; player needs the `Player` tag. Not yet tested in Play.
- Editor: `GridObjectEditor` (Snap buttons), `GridDuplicateCleaner` (`Tools → Grid → …`, removes duplicate grid components).

**Reward / completion wiring (no code per puzzle — two decoupled moments):**
- *Puzzle solved → world changes:* wire `CrateTarget.onAllTargetsCovered` in the Inspector (on just ONE target is enough — it fires only when ALL targets are covered). Typically `SetActive(false)` on the bush blocking the key, or opening a door.
- *Pickup → effect:* the key/lore is a `Pickup`; its effect (crypt swap, door, flag) is wired per-instance. The puzzle doesn't know what the reward is, and the reward doesn't know the puzzle — new effect KINDS are added on request, not predicted up front.

**Authoring:** one `PuzzleGrid` in the scene with the tilemap; add the right component to each crate/target/obstacle; position by eye; click **Snap ALL GridObjects in Scene**. Component goes on the prefab, Snap is pressed in the scene (never in Prefab Mode).

**Player movement:** `PlayerController` moves via `Rigidbody2D.MovePosition` in `FixedUpdate` (was writing `transform.position`, which teleported past collision and caused shoving/jitter). Player rigidbody must be **Dynamic** (gravity 0, freeze rotation Z) or MovePosition won't stop on contact.

**Known issues (good-enough for now, revisit later):**
- Occasional stray crate movement — a crate sometimes slides in an odd/diagonal direction.
- Occasional stalls — a push sometimes doesn't register / the crate briefly locks up.
- FIXED (verify in Play): "crate shoved the player" — pushing up jammed the player's collider centre past the crate centre, so a tap back read as a valid opposite push and the kinematic crate slid into the player. `PlayerBehind` now uses the player's feet (transform pivot) + a margin, and `TryPush` refuses to slide onto the player's cell.
- Approach gap: how close the player stops to a crate is set by the two colliders' sizes (prefab data), not by code. Realistic look needs the crate's solid collider to be a thin strip at its base, not a full box. Alternative (not done): stop the player by cell logic in code instead of physics.

---

## Cosmetic Seating ("Encaixe") — statue settling into a portal

**Status: provisional.** Added because the current statue art has its base at the bottom of the sprite, so a statue centred honestly on a portal reads as hovering above it. If the art changes, this whole thing can go. Written to be removable — see *How to rip it out* below.

**The problem it solves:** the grid defines an object's cell as the centre of its ARTWORK (`GridObject.VisualCenter`). That's what makes "looks aligned" and "is aligned" agree everywhere else — but it also means you can't just nudge a sprite to look better, because the nudge moves the object's CELL with it, and then coverage, the pulse, ghost checks and the gate all follow the statue off the tile it's supposed to be on.

**The idea:** declare the lie instead of hiding it. The art moves; `VisualCenter` subtracts the nudge back out; every consumer keeps reading the true cell.

**Where it lives (two files, three members):**
- `GridObject.CosmeticOffset` — `Vector3`, runtime only, never serialized, defaults to zero. `VisualCenter` subtracts it. **Anything that doesn't set it is completely unaffected** — that's the whole safety argument.
- `PushableCrate.portalLandingOffset` — the only knob. Inspector: *Encaixe no portal (só visual)*. `Y` positive = art sits higher than the tile centre.
- `PushableCrate.StepTo` — blends the offset INTO the slide (see below).
- `PushableCrate.SettleIfAuthoredOnPortal` — applies it dry at `Start` for a statue placed on a portal in the editor, which was never pushed and so would otherwise sit wrong until first touched.

**Behaviour:** the landing offset is chosen before the slide (nudged if the destination cell has any `CrateTarget`, zero otherwise), and `CosmeticOffset` lerps on the SAME curve as the position. So the visible path bows diagonally into the portal while the logical path — `VisualCenter` — stays the same straight line to the cell centre it always was. One movement, no second tug. Leaving a portal un-bows on the way out for the same reason. All approach directions end in the same place, so a statue never sits differently depending on how it got there.

**Tuning:** enter Play, push a statue onto a portal, edit `Portal Landing Offset` on the `PushableStatue` prefab and watch. Copy the value out of Play mode when it looks right.

**Two things that will bite whoever touches this next:**
- `RootPositionForCell` works backwards from `VisualCenter`, which has the offset baked in. Any NEW caller of it on a crate that might be parked on a portal must zero the offset, ask, then restore it — `StepTo` does exactly that.
- The offset moves the **collider** with the art. A seated statue's solid body sits `portalLandingOffset` away from the tile it logically occupies. Fine at ~0.25; if a much bigger offset is ever wanted, the collider needs decoupling from the art instead.

**Related but SEPARATE:** `PortalPulse` (portal's VFX goes out ~0.5s after a crate settles on its cell) is its own component and does not depend on Cosmetic Seating. Killing one does not kill the other.

**How to rip it out:** delete `portalLandingOffset`, the offset lines in `StepTo`, and `SettleIfAuthoredOnPortal` from `PushableCrate`; delete `CosmeticOffset` from `GridObject` and restore `VisualCenter` to `visual.bounds.center`. Undo's `RestoreTo(cell, pos, cosmetic)` third parameter is optional and can stay or go. Nothing else in the project reads `CosmeticOffset`.

---

## Y-Sort — quem desenha na frente de quem (implementado, `Assets/Scripts/Rendering/`)

**A regra:** quem está mais para baixo na tela desenha na frente. `Sorting Order = -Y * Precision`.

Antes disso a ordem de desenho era digitada na mão, centenas de Sorting Orders espalhados pela cena — trabalhoso e **impossível de acertar**, porque quem está na frente muda conforme o jogador anda. Uma árvore precisa cobrir o Josh quando ele passa atrás dela e ser coberta quando ele passa na frente; um número fixo só consegue uma das duas.

**Não há componente para adicionar, nem migração para rodar.** `YSortWorld` é uma classe estática que varre a cena em tempo de execução e ordena tudo sozinha. Objeto novo entra sozinho, cena nova funciona sozinha, e **o arquivo da cena nunca é tocado** — nada disso vive em `.unity` ou `.prefab`. (Uma primeira versão era um componente por objeto + ferramenta de migração; foi descartada porque gravaria 325 componentes e ~700 Sorting Orders no arquivo da cena que o Olavo e o Gabriel editam juntos.)

**Arquivos:**
- `YSortWorld` — o motor. Varre, agrupa, calcula, aplica. Não é um `MonoBehaviour`; em Play ele cria um objeto escondido (`HideAndDontSave`) só para ter um `LateUpdate`.
- `YSortSettings` (+ `Assets/Resources/YSortSettings.asset`) — os números. Achado por `Resources.Load`, nada é arrastado para campo nenhum.
- `YSort` — o **ajuste**, não o motor. Componente opcional, só onde o padrão erra.
- `Editor/YSortEditorDriver` — faz valer na Scene View sem entrar em Play.
- `Editor/YSortTools` — `Tools > Y-Sort > Relatório` (não muda nada), `Ajustar âncora da seleção`, `Remontar agora`.

### O bug dos interiores, e como foi resolvido (vale para TODA casa nova)

**Sintoma:** dentro da casa do Elder, os personagens desapareciam atrás do tapete, da escada, do sofá, da estante — de tudo. Fora, na vila, funcionava.

**Causa,** lida do dump do F3 e não deduzida:

```
order=6408  MarcusVisual   (raiz: Marcus)
order=8037  BigRedCarpet   (raiz: ElderHouseInside1stfloor)
order=8037  Stairs         (raiz: ElderHouseInside1stfloor)
order=8037  Chair2, Sofa, Bookshelf, Bible, Table_d, Fireplace…
```

Vinte e um objetos com **a mesma ordem** e **a mesma raiz**. O `RootOf` sobe enquanto o pai tiver sprite/collider/animator — e **o chão da casa é um sprite**. Cada móvel subiu até ele, e o cômodo inteiro virou UM objeto: uma âncora só (o pé da casa, lá embaixo), uma ordem só, desenhada por cima de quem anda dentro.

É a ideia de GRUPO funcionando ao contrário. Certa para uma árvore — sombra, tronco e copa são partes de um objeto. Errada para um cômodo, cujos filhos são objetos independentes por onde o jogador anda.

**A correção — `maxObjectHeight` (padrão 6 unidades):** um pai só conta como "o mesmo objeto" se tiver **até essa altura**. Árvore, móvel e personagem têm poucos metros; um cômodo tem dezenas. Acima disso, o objeto deixa de ser unidade: não é ordenado (fica intacto, embaixo) e **cada filho passa a ser ordenado por si**.

O discriminador é físico — altura medida do sprite — igual ao do collider. Não é palpite por nome.

**Numa casa nova, se acontecer de novo:** entra em Play, aperta **F3** dentro dela e olha a coluna `raiz`. Se vários objetos diferentes mostrarem a MESMA raiz, é este bug — o sprite do chão está grande e mesmo assim abaixo de `maxObjectHeight`, ou há um contêiner intermediário com sprite. Baixar `maxObjectHeight` resolve; o limite é não ficar menor que a altura da maior árvore.

**Duas ideias que sustentam tudo:**

- **GRUPO.** Um objeto quase nunca é um sprite só — a árvore tem três (sombra, tronco, copa) e o Josh tem dois (Top e Bottom). Ordenar cada sprite pelo próprio Y destruiria o objeto: a copa tem Y maior que o tronco, então seria desenhada *atrás* dele. Então o Y de **um** ponto decide a ordem do grupo todo, e dentro do grupo cada sprite mantém a ordem relativa com que a arte foi montada. É por isso que os Sorting Orders que já existem não são jogados fora: eles **viram** essa ordem relativa (normalizados pelo menor, então `1,2,3` e `11,12,13` são a mesma coisa). A conta é idempotente — remontar depois que o sistema já escreveu recupera exatamente os mesmos valores relativos.
- **ÂNCORA.** O Y que importa é onde o objeto **toca o chão**, não o centro do sprite nem o pivô. Uma árvore de 3 metros medida pelo meio some atrás de coisas que deveria cobrir. O padrão é a base do collider sólido (a parte por onde o jogador esbarra); sem collider, a base da arte; sem arte utilizável, o pivô. Medido **uma vez** por objeto — a distância do pivô até o pé não muda quando ele anda.

**Sorting Layer manda mais que Sorting Order.** Com o Josh em `Objects`, os NPCs em `NPCs` e as árvores em `Default`, nenhum personagem consegue passar atrás de uma árvore por mais que o Y diga que deveria. Por isso `forceSortingLayer` move tudo para uma camada só (`Objects`). Os Tilemaps (chão, penhasco) são `TilemapRenderer`, ficam em `Default` e não são tocados — `Objects` está acima de `Default`, então nada afunda no chão. Se um dia o penhasco precisar se entrelaçar com personagens, o caminho é o Mode `Individual` do Tilemap Renderer, não isto aqui.

**QUEM ENTRA — a pergunta mais importante do sistema.** Só entra o objeto que tem **collider sólido** *ou* **Rigidbody2D**. Todo o resto fica **intacto**: nem a ordem nem a camada são tocadas.

As duas portas são físicas, não palpite sobre nome:
1. **Collider sólido** = "ocupa espaço no mundo" = "o personagem esbarra nisto, logo passa atrás disto". Árvore, casa, cerca, pedra, móvel, estátua.
2. **Rigidbody2D** = quem se *move* pelo mundo mesmo sem colidir. Isto é o que salva a **Haze**: sendo o fantasma, ela atravessa tudo e só tem collider de trigger — pela porta 1 sozinha, um personagem principal ficaria de fora.

**Por que isso importa (erro real, cometido e corrigido):** a primeira versão ordenava tudo que tinha sprite, e varreu junto `RoadsAndGrassSprites` (38), `InteriorSprites` (8) e as poças. **Chão ordenado por Y passa por CIMA do personagem**, porque na tela o chão está embaixo dele — a regra "quem está mais embaixo desenha na frente", aplicada a uma coisa *deitada*, produz o oposto exato do que se quer. Estrada e poça não têm collider; é por isso que o discriminador físico funciona e o palpite por nome não.

Hoje na MainScene: **311 instâncias entram**, 12 ficam intactas (`portal`, `Fur`, `Flower_a/b`, `Bible`, `Candle`, `Chair2`, `WritingTable`) — mais os 52 sprites de chão/interior da própria cena. `portal` e `Fur` ficarem de fora está certo, são tapetes. `Chair2`/`WritingTable` são os candidatos a `Incluir Sem Collider` se algum dia precisarem se entrelaçar.

**Fica de fora também:** camada `Dialogue` (balão de fala, prompt de interação), qualquer coisa dentro de um `Canvas`, e nomes contendo `fog`/`mist`/`nevoa`/`vignette`.

**Cuidado com esse filtro por nome.** `haze` já esteve nessa lista por parecer névoa — e **Haze é um NPC principal**, então teria tirado um personagem do Y-Sort inteiro, silenciosamente. Filtrar por nome é frágil num jogo cujo tema *é* a névoa. Na dúvida, deixa o objeto entrar no sistema e marca `Não ordenar` no `YSort` dele: é explícito, aparece no Inspector, e não pega ninguém de surpresa.

**Quando colocar um `YSort` na mão** (só nesses dois casos):
1. A linha do chão está errada — collider que é gatilho grande, corrimão, colisão no meio do tronco. O gizmo amarelo mostra onde o sistema acha que está o chão; `Ancora: Pivo` e/ou `anchorOffsetY` arrumam.
2. O objeto não deveria ser ordenado: `Não ordenar`.
3. O objeto **precisa** entrar mas não tem collider sólido nem Rigidbody2D: `Incluir Sem Collider`.

**Scene View x Play.** Mexer em `sortingOrder` por script não faz a Scene View se redesenhar sozinha — os números ficam certos e a imagem continua a antiga até você clicar em algo. Era por isso que o Play mostrava a ordem certa e a cena não. `YSortEditorDriver` chama `SceneView.RepaintAll()` quando algum objeto muda de ordem.

**Preview no editor.** Ligado por padrão (`previewInEditor`): a Scene View mostra a ordem certa sem entrar em Play. O preço é que salvar a cena grava os Sorting Orders calculados no arquivo — não quebra nada (tudo é recalculado do zero a cada quadro), mas engorda o diff. Desligar no asset se atrapalhar o trabalho junto com o Gabriel.

**Custo.** Ordenação roda todo quadro, mas com early-out por âncora: objeto parado custa uma comparação de float. Só quem se moveu escreve. Não existe distinção Static/Dynamic para configurar — "se moveu" é detectado, não declarado. A varredura por objetos novos roda a cada `rescanInterval` (padrão 2s; 0 = só ao carregar a cena).

**Interruptor geral:** apagar `Assets/Resources/YSortSettings.asset` desliga o sistema inteiro e o jogo volta a desenhar pelos Sorting Orders escritos na mão. Nome de Sorting Layer inexistente também não move ninguém (devolveria `Default`, a mesma camada dos Tilemaps, e metade dos objetos sumiria debaixo do chão).

**Diagnóstico em Play — use isto antes de deduzir qualquer coisa:**
- Ao entrar em Play, uma linha `[Y-Sort] ATIVO. N objetos ordenados…` diz se o motor está de pé e com que números. `N = 0` significa que nada entrou.
- **F3** despeja todo sprite a 12 unidades do Josh, **ordenado por ordem de desenho, de trás para a frente**, marcando `[Y-SORT]` ou `[intacto]` e mostrando a **raiz** de cada um. É literalmente a ordem que a tela usa. Foi esse dump que encontrou o bug dos interiores em um minuto, depois de horas de palpite.

**`orderBase` (padrão 20000):** a conta crua é `-Y * Precision`, então dentro de uma casa em Y=137 o personagem receberia `-13700` — abaixo de qualquer arte com ordem escrita à mão (perto de zero) e abaixo de valores antigos carimbados no arquivo. O deslocamento joga tudo que é ordenado por Y para uma faixa positiva, sempre acima da arte de chão. A ordem relativa entre os ordenados não muda.

**Cuidado com bounds de objeto desativado.** Renderer e collider de objeto inativo devolvem bounds **zerados**. Medir a âncora nesse momento dava `0 - posiçãoY` — um personagem em Y=137 ganhava âncora 0, como se estivesse na origem do mundo — e a medida ficava cacheada para sempre. Hoje a medida só é aceita se o objeto estiver ativo e os bounds forem reais; enquanto não for, a `Entry` remede a cada quadro.

**Testado em Play e funcionando** na casa do Elder (verificado pelo dump do F3).
