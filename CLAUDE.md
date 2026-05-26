# ECDD Adventure Game — Design Document

This file is the living design reference for all collaborators (human and AI). Update it as the design evolves.

---

## REGRA ABSOLUTA PARA CLAUDE

**Claude só lê e modifica arquivos `.cs`.** Nada mais. Prefab, scene, anim, controller, meta, sprite — Claude NÃO lê e NÃO edita. Para qualquer coisa fora de `.cs`, Claude pergunta ao usuário.

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
