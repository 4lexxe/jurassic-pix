# Jurassic Pix — Diagramas UML y aplicación de SOLID

| | |
| --- | --- |
| **Institución** | Universidad Nacional de Jujuy — Facultad de Ingeniería |
| **Carrera** | Tecnicatura Universitaria en Diseño Integral de Videojuegos |
| **Asignatura** | Programación de Videojuegos II — Período lectivo 2026 |
| **Docente** | Samuel Bustamante |
| **Integrantes** | Awki Gimenez — Calatayud Alex |
| **Proyecto** | Jurassic Pix (Unity 6000.6.0f1, C#, Built-in Render Pipeline) |
| **Estado del código relevado** | 23/11/2026, actualización de la entrega |

La **parte A** modela la implementación y la evalúa con los cinco principios SOLID. La **parte B** describe un diseño objetivo y los pasos de migración que todavía pueden ampliar esa separación.

Los diagramas están escritos en [Mermaid](https://mermaid.js.org) dentro de este archivo y exportados como imagen en la carpeta [`uml/`](uml/) (SVG y PNG) para pegarlos en el informe. Después de editar un diagrama, `python Tools/render_uml.py` vuelve a exportar las imágenes.

## Índice

1. [Notación](#1-notación)
2. [Parte A — Arquitectura actual](#2-parte-a--arquitectura-actual)
3. [Evaluación SOLID del código actual](#3-evaluación-solid-del-código-actual)
4. [Parte B — Diseño propuesto aplicando SOLID](#4-parte-b--diseño-propuesto-aplicando-solid)
5. [Plan de migración](#5-plan-de-migración)
6. [Vínculo con los ejes de la cátedra](#6-vínculo-con-los-ejes-de-la-cátedra)
7. [Referencias y uso de IA](#7-referencias-y-uso-de-ia)

## 1. Notación

Visibilidad de los miembros: `+` público, `-` privado, `~` interno al ensamblado (`internal` en C#). Un `$` al final marca un miembro estático y un `*` uno abstracto. El estereotipo `«MonoBehaviour»` indica que la clase hereda de `UnityEngine.MonoBehaviour`; esa herencia se dibuja una sola vez, en el diagrama A.2, para no repetir la flecha en todos los demás.

Los seis tipos de relación entre clases de UML, con el ejemplo del proyecto que se usa en los diagramas:

| Relación | Símbolo UML | Significado | Ejemplo en Jurassic Pix |
| --- | --- | --- | --- |
| Asociación | Línea llena con flecha abierta | A guarda una referencia a B y la usa | `Dinosaur` → `Sighting` |
| Agregación | Rombo vacío del lado del todo | A agrupa a B, pero B vive sin A | `WildlifeHerd` ◇— `Dinosaur` |
| Composición | Rombo lleno del lado del todo | A crea a B y B no existe sin A | `Dinosaur` ◆— `DinosaurVitals` |
| Generalización (herencia) | Triángulo vacío con línea llena | B es un A | `Dinosaur` —▷ `MonoBehaviour` |
| Realización (implementación) | Triángulo vacío con línea punteada | B cumple el contrato de la interfaz I | No hay en el código actual; aparece en la parte B (`WildlifeHerd` ···▷ `IHerd`) |
| Dependencia | Flecha abierta con línea punteada | A usa a B de paso (parámetro, llamada estática), sin guardarlo | `ExpeditionNetwork` ···> `ExpeditionPackets` |

## 2. Parte A — Arquitectura actual

### A.0 Vista rápida

Mapa general del juego en once piezas. Cada caja dice su nombre en español y, en cursiva, la clase real del código que la implementa. Las líneas muestran quién usa o contiene a quién; el detalle completo está en los diagramas siguientes.

<!-- diagrama: A0_vista_rapida -->
```mermaid
%%{init: {"curve": "linear", "flowchart": {"curve": "linear"}, "state": {"curve": "linear"}, "class": {"curve": "linear"}}}%%
classDiagram
    direction TB
    class MenuInicio["Menú de inicio"] {
        <<ExpeditionMenu>>
        elegir especie
    }
    class Partida["Partida"] {
        <<Expedition>>
        cámara, HUD y controles
    }
    class Red["Red"] {
        <<ExpeditionNetwork>>
        multijugador
    }
    class Criatura["Criatura"] {
        <<Dinosaur>>
        hambre, sed y madurez
    }
    class Salud["Salud"] {
        <<DinosaurVitals>>
        vida y daño
    }
    class Movimiento["Movimiento"] {
        <<DinosaurMotion>>
        motor y animación
    }
    class Comportamiento["Comportamiento"] {
        <<DinosaurBehavior>>
        la IA de la fauna
    }
    class Manada["Manada"] {
        <<WildlifeHerd / TriceratopsHerd>>
        líder y tácticas
    }
    class Isla["Isla"] {
        <<IslandLayout>>
        lagos, regiones y relieve
    }
    class Clima["Día y clima"] {
        <<ForestLighting + ValleyWeather>>
        sol, lluvia y viento
    }
    class Sonido["Sonido"] {
        <<DinosaurAudio>>
        voces y pasos
    }
    MenuInicio --> Partida : elige especie e inicia
    Partida o-- Criatura : dirige jugador y fauna
    Partida --> Clima : avanza el tiempo
    Partida o-- Red : sincroniza
    Criatura *-- Salud
    Criatura *-- Movimiento
    Criatura o-- Comportamiento : solo la fauna
    Comportamiento --> Manada : sigue al líder
    Comportamiento --> Isla : agua y comida
    Criatura --> Sonido : emite
```

*Imagen exportada: [PNG](uml/A0_vista_rapida.png) · [SVG](uml/A0_vista_rapida.svg)*

Cómo leerlo: la **Partida** es el director de orquesta — maneja la cámara, la interfaz y qué criatura controla el jugador. Cada **Criatura** es un dinosaurio con tres piezas internas: su **Salud**, su **Movimiento** y, si es fauna, un **Comportamiento** que decide qué hace consultando a la **Isla** (dónde hay agua y comida) y a su **Manada** (a quién seguir o de quién huir). El **Clima** pone el día, la noche y las tormentas; el **Sonido**, las voces de cada animal. La **Red** copia la partida a otra computadora cuando hay dos jugadores.

### A.1 Paquetes y dependencias

Las flechas sólidas muestran dependencias de código. El ensamblado `JurassicPix.Core` contiene las reglas de salud, los datos de control y pose y los contratos. `JurassicPix.Runtime` lo referencia y contiene las capas lógicas de presentación, simulación y servicios técnicos. El código de Editor arma los componentes de la escena y las pruebas usan un ensamblado propio.

<!-- diagrama: A1_paquetes -->
```mermaid
%%{init: {"curve": "linear", "flowchart": {"curve": "linear"}, "state": {"curve": "linear"}, "class": {"curve": "linear"}}}%%
flowchart TB
    EDITOR["<b>«Editor» Generación de escena y assets</b><br/>ExpeditionScene · ForestWorld · ExpeditionAudioSetup<br/>TriceratopsGrowthAssets · TriceratopsNavigationBake · AndroidBuild"]
    PRES["<b>Partida e interfaz</b><br/>Expedition · CoopMenu · ExpeditionMap<br/>FieldNotices · HudIcons · PsxView"]
    RED["<b>Red</b><br/>ExpeditionNetwork · ExpeditionPackets · ExpeditionRelay · LanInvite"]
    AUDIO["<b>Audio</b><br/>DinosaurAudio · GroundTremor<br/>FlightWind · ForestAmbience"]
    FAUNA["<b>Fauna</b><br/>Dinosaur · DinosaurBehavior · DinosaurMotion<br/>TriceratopsLife · TriceratopsHerd · WildlifeHerd"]
    MUNDO["<b>Mundo y ambiente</b><br/>IslandLayout · ForestLighting · ValleyWeather<br/>ForestCanopy · MeadowGrass · TriceratopsNavigation"]
    CORE["<b>JurassicPix.Core</b><br/>IPlayerControl · IReplicatedCreature<br/>DinosaurVitals · DinosaurInput · DinosaurPose"]
    UNITY["<b>Motor</b><br/>Unity 6 · NavMesh · Netcode for GameObjects · Unity Transport"]

    EDITOR -->|construye y configura| PRES
    EDITOR --> RED
    EDITOR --> AUDIO
    EDITOR --> FAUNA
    EDITOR --> MUNDO
    PRES --> RED
    PRES --> AUDIO
    PRES --> FAUNA
    PRES --> MUNDO
    PRES --> CORE
    RED --> CORE
    RED --> MUNDO
    RED -. "evento SeatChanged" .-> PRES
    AUDIO --> FAUNA
    AUDIO --> MUNDO
    FAUNA --> MUNDO
    FAUNA --> CORE
    FAUNA -. "lectura de _ForestNight" .-> MUNDO
    RED --> UNITY
    FAUNA --> UNITY
    MUNDO --> UNITY
    CORE -->|tipos de datos y matemática| UNITY
```

*Imagen exportada: [PNG](uml/A1_paquetes.png) · [SVG](uml/A1_paquetes.svg)*

Lectura del diagrama:

- El código de juego nunca depende del paquete **Editor**: la escena `Expedicion.unity` es un artefacto que `ExpeditionScene.Rebuild` genera por código.
- Presentación y red comparten contratos del núcleo. `ExpeditionNetwork` recibe `IReplicatedCreature` para enviar comandos y guardar poses. `MeadowGrass` consulta la capa física en `IslandLayout`.
- Las líneas punteadas marcan flujos indirectos. `SeatChanged` lleva a la interfaz los cambios de asignación. La fauna consulta el valor global `_ForestNight` que publica `ForestLighting`.
- **Dentro** del paquete Fauna las dependencias son circulares (`Dinosaur` ↔ `DinosaurBehavior` ↔ `TriceratopsLife` ↔ `TriceratopsHerd`, y `IslandSites` conoce a las manadas que a su vez lo consultan). Según la clasificación de Rabin, el conjunto es **modular por capas**, con un núcleo de fauna de acoplamiento **ad-hoc**.

### A.2 Herencia: un solo nivel

<!-- diagrama: A2_herencia -->
```mermaid
%%{init: {"curve": "linear", "flowchart": {"curve": "linear"}, "state": {"curve": "linear"}, "class": {"curve": "linear"}}}%%
classDiagram
    direction LR
    class MonoBehaviour {
        <<Unity>>
    }
    class AssetPostprocessor {
        <<UnityEditor>>
    }
    MonoBehaviour <|-- Expedition
    MonoBehaviour <|-- ExpeditionNetwork
    MonoBehaviour <|-- PsxView
    MonoBehaviour <|-- Dinosaur
    MonoBehaviour <|-- Sighting
    MonoBehaviour <|-- TriceratopsLife
    MonoBehaviour <|-- TriceratopsHerd
    MonoBehaviour <|-- WildlifeHerd
    MonoBehaviour <|-- TriceratopsNavigation
    MonoBehaviour <|-- DinosaurAudio
    MonoBehaviour <|-- GroundTremor
    MonoBehaviour <|-- FlightWind
    MonoBehaviour <|-- ForestAmbience
    MonoBehaviour <|-- ForestLighting
    MonoBehaviour <|-- ValleyWeather
    MonoBehaviour <|-- ForestCanopy
    MonoBehaviour <|-- MeadowGrass
    AssetPostprocessor <|-- ExpeditionAudioImport
```

*Imagen exportada: [PNG](uml/A2_herencia.png) · [SVG](uml/A2_herencia.svg)*

Las 17 clases de juego que heredan lo hacen directamente de `MonoBehaviour` y todas son `sealed`: la jerarquía propia tiene **profundidad 1**. No hay clases abstractas ni interfaces propias. Las cinco especies **no** son subclases (`Triceratops : Dinosaur`): son la misma clase `Dinosaur` configurada con datos (`Dinosaur.Profile`) y, en el caso del Triceratops, con un componente más (`TriceratopsLife`). Es composición por componentes, la opción que la bibliografía recomienda frente a jerarquías profundas.

### A.3 Núcleo de fauna

<!-- diagrama: A3_fauna -->
```mermaid
%%{init: {"curve": "linear", "flowchart": {"curve": "linear"}, "state": {"curve": "linear"}, "class": {"curve": "linear"}}}%%
classDiagram
    direction LR
    class Dinosaur {
        <<MonoBehaviour>>
        +Traits: Profile
        +Motion: DinosaurMotion
        +Herd: WildlifeHerd
        +Life: TriceratopsLife
        +Playable: bool
        +IsControlled: bool
        +IsFlying: bool
        +IsDead: bool
        ~Maturity: float
        ~Thirst: float
        ~Hunger: float
        ~Stamina: float
        ~State: BehaviorState
        ~Focus: Vector3$
        ~Sight: float$
        ~Throttle: bool$
        +SetControl(controlled)
        +SetInput(direction, run, lift, guard, crouch, act)
        +TryAttack() bool
        +ToggleFlight()
        +SetNeighbors(animals)
        ~BeBorn()
        ~SetLeading(lead)
        ~Opportunity() DinosaurDeed
        ~ReceiveHit(attacker, damage, shove) int
        ~Heal(health)
        ~CapturePose() DinosaurPose
        ~ApplyReplica(pose, delta)
        -Survive(delta)
        -Mature(value)
        -MoveBody(delta)
        -DeliverImpact()
        -Cling(delta)
        -UpdateDeath(delta)
    }
    class Profile {
        <<Serializable>>
        +Walk: float
        +Run: float
        +Turn: float
        +MaxHealth: int
        +Damage: int
        +Armor: float
        +GuardMultiplier: float
        +Stamina: float
        +AttackReach: float
        +FlightSpeed: float
        +Nature: Temperament
        +TailStrike: bool
        +CanFly: bool
    }
    class Temperament {
        <<enumeration>>
        Herd
        Pack
        Solitary
        Flyer
    }
    class DinosaurVitals {
        +Maximum: int
        +Health: int
        +Dead: bool
        +Ratio: float
        +Hit(damage, resistance, guardMultiplier) int
        +SetHealth(health)
        +Resize(limit)
    }
    class DinosaurBehavior {
        ~State: BehaviorState
        ~Activity: LifeActivity
        ~WantsStrike: bool
        ~WantsCrouch: bool
        ~WantsFeed: bool
        ~Thirst: float
        ~Hunger: float
        +Steer(animals) Vector3
        +Provoke(source)
        ~Killed(prey)
        +Reset()
        -Sense(animals, here, packmates)
        -Hunt(here, packmates) Vector3
        -HoldGround(here, packmates) Vector3
        -Soar(animals, here) Vector3
        -Errands(here) Vector3
        -Settle(direction, run)
    }
    class BehaviorState {
        <<enumeration>>
        Idle
        Travel
        Drink
        Eat
        Rest
        Stalk
        Chase
        Fight
        Flee
        Guard
    }
    class DinosaurMotion {
        <<Serializable>>
        +Animator: Animator
        +Idle: AnimationClip
        +Walk: AnimationClip
        +Attack: AnimationClip
        +Initialize()
        +Evaluate(delta, gait, airborne, attackTime, dive)
        +AttackDuration(airborne) float
        ~SetYouth(value)
        ~SetGrowth(scale)
        ~Fall(delta, stature)
        ~Show(visible)
        +Dispose()
    }
    class Sighting {
        <<MonoBehaviour>>
        +Species: string
        +FieldNote: string
        +Focus: Transform
    }
    class TriceratopsLife {
        <<MonoBehaviour>>
        ~Stage: TriceratopsStage
        ~Activity: LifeActivity
        ~Age: float
        ~Hunger: float
        ~Thirst: float
        ~Maturity: float
        ~Scale: float
        ~Advance(delta)
        ~Steer() Vector3
        ~Mature(maturity)
        ~Sustain(delta, drinking, eating)
        ~LayAt(location, incubation, mother)
        ~Retire()
    }
    class TriceratopsHerd {
        <<MonoBehaviour>>
        ~Tactic: HerdTactic
        ~Errand: HerdErrand
        ~Leader: TriceratopsLife
        ~Center: Vector3
        ~Threat: Dinosaur
        ~Bind(population)
        ~Warn(attacker)
        ~FollowPost(member) Vector3
        ~StampedePost(member) Vector3
        ~FindProtector(child) TriceratopsLife
        ~TryLay(parent) bool
        ~TryResource(member) bool
    }
    class WildlifeHerd {
        <<MonoBehaviour>>
        ~Leader: Dinosaur
        ~Errand: HerdErrand
        ~Middle: Vector3
        ~Thirst: float
        ~Hunger: float
        ~Foe: Dinosaur
        ~Menace: Dinosaur
        ~Meal: Dinosaur
        ~Bind(population)
        ~Steer(animal) Vector3
        ~Pursue(prey, seconds)
        ~Dread(danger, seconds)
        ~Feast(carcass, seconds)
        ~Abandon(rest)
    }
    class TriceratopsRoute {
        +Steer(position, destination, urgent, slot) Vector3
        +Reset()
    }
    class IslandSites {
        <<static>>
        ~Communities: List~TriceratopsHerd~$
        ~Herds: List~WildlifeHerd~$
        ~Water(from) Vector3$
        ~Dry(from) Vector3$
        ~GiantTree(from, home, last) bool$
        ~Prey(hunter, from, shunned) bool$
    }
    class IslandLayout {
        <<static>>
        +RegionAt(position) int$
        +ShoreDistance(point) float$
        +LagoonDistance(point) float$
        +SurfaceLevel(point) float$
    }

    Dinosaur "1" *-- "1" Profile : Traits
    Dinosaur "1" *-- "1" DinosaurMotion : Motion
    Dinosaur "1" *-- "1" DinosaurVitals : crea en Awake
    Dinosaur "1" *-- "1" DinosaurBehavior : crea en Awake
    Dinosaur "1" --> "1" Sighting : mismo GameObject
    Dinosaur "1" --> "0..1" TriceratopsLife : Life
    Dinosaur "1" --> "0..*" Dinosaur : neighbors
    Profile --> Temperament
    DinosaurBehavior --> "1" Dinosaur : animal, target, meal
    DinosaurBehavior --> BehaviorState
    DinosaurBehavior ..> TriceratopsLife : delega Steer
    DinosaurBehavior ..> WildlifeHerd : delega Steer
    DinosaurBehavior ..> IslandSites
    TriceratopsLife "1" --> "1" Dinosaur : animal
    TriceratopsLife "1" *-- "1" TriceratopsRoute : route
    TriceratopsLife "0..*" --> "0..1" TriceratopsLife : protector
    TriceratopsHerd "1" o-- "1..*" TriceratopsLife : members
    TriceratopsHerd --> "0..*" Dinosaur : predators, threat
    WildlifeHerd "0..1" o-- "1..*" Dinosaur : members
    IslandSites o-- TriceratopsHerd : registro estático
    IslandSites o-- WildlifeHerd : registro estático
    Dinosaur ..> IslandLayout
    IslandSites ..> IslandLayout
```

*Imagen exportada: [PNG](uml/A3_fauna.png) · [SVG](uml/A3_fauna.svg)*

Puntos a notar:

- `Dinosaur` **compone** cuatro partes: los datos de la especie (`Profile`), la animación (`DinosaurMotion`), la vida (`DinosaurVitals`) y la decisión (`DinosaurBehavior`). Las dos últimas las crea en `Awake` y nadie más las ve.
- Hay **dos clases de grupo sin relación entre sí**: `TriceratopsHerd` (manada con ciclo de vida, nidos y tácticas) y `WildlifeHerd` (el resto). Como no comparten un tipo, quien necesita "una manada cualquiera" guarda un `object` y pregunta de qué clase es (`quarry is TriceratopsHerd community ? community.Center : ((WildlifeHerd)quarry).Middle`, en `WildlifeHerd` y en `DinosaurBehavior`).
- El Triceratops es el caso especial: `Dinosaur` pregunta `Life != null` para decidir de dónde saca la sed, el hambre, la madurez, la velocidad y la actividad.

### A.4 Partida, interfaz y red

<!-- diagrama: A4_partida_red -->
```mermaid
%%{init: {"curve": "linear", "flowchart": {"curve": "linear"}, "state": {"curve": "linear"}, "class": {"curve": "linear"}}}%%
classDiagram
    direction LR
    class Expedition {
        <<MonoBehaviour, partial>>
        -eyes: Camera
        -lighting: ForestLighting
        -network: ExpeditionNetwork
        -weather: ValleyWeather
        -tremor: GroundTremor
        -flightWind: FlightWind
        -habitats: Habitat[]
        -creatures: Dinosaur[]
        -population: Dinosaur[]
        -active: Dinosaur
        -menu: bool
        -panel: bool
        -ReadDesktop()
        -ReadTouch()
        -ReadMenuKeys()
        -DriveCreature(movement, running, touch)
        -SendInput(input)
        -ChooseCreature(index)
        -ConfirmSpecies()
        -TrackLife()
        -SetLeading(lead)
        -FollowCreature(cut)
        -Showcase()
        -UpdateAtmosphere()
        -OnGUI()
        -DrawMenu()
        -DrawFieldHud()
        -DrawVitals()
        -DrawPanel()
        -DrawDeath()
        -DrawTouchControls()
        -OnSeatChanged(seat)
    }
    class Habitat {
        <<Serializable>>
        +Name: string
        +Route: string
        +Fog: Color
        +Light: Color
        +FogRange: Vector2
    }
    class CoopMenu {
        +IsOpen: bool
        +Toggle()
        +Draw(preferredSeat)
    }
    class ExpeditionMap {
        <<static>>
        +Draw(terrain, area, animals, active, observer, follow)$
    }
    class HudIcons {
        <<static>>
        +Build() Texture2D[]$
    }
    class PsxView {
        <<MonoBehaviour>>
        +Viewport: Rect$
        +DrawViewport()
    }
    class ExpeditionNetwork {
        <<MonoBehaviour>>
        +DefaultPort: ushort$
        ~Online: bool
        ~IsGuest: bool
        ~Dedicated: bool
        ~JoinCode: string
        ~PreparingInvite: bool
        ~LocalSeat: int
        ~Status: string
        ~SeatChanged: event Action~int~
        ~Initialize(creatures, seats, clock, conditions)
        ~Host(preferredSeat, port) bool
        ~HostInternet(preferredSeat, southAmerica)
        ~Serve(port) bool
        ~Join(address, port) bool
        ~JoinByCode(code)
        ~Leave()
        ~Select(seat)
        ~Attack()
        ~ToggleFlight()
        ~SetLeading(lead)
        ~SetInput(input)
        ~RequestDayNight()
        ~RequestWeather()
        -SendSnapshot()
        -ReceiveSnapshot(sender, reader)
    }
    class IPlayerControl {
        <<interface>>
        +SetInput(input)
        +Attack()
        +ToggleFlight()
        +SetLeading(lead)
    }
    class IReplicatedCreature {
        <<interface>>
        +SetControl(controlled)
        +BeBorn()
        +SetReplica(enabled)
        +CapturePose() DinosaurPose
        +ApplyReplica(pose, delta)
    }
    class FieldNotices {
        -pending: Queue~string~
        ~Enqueue(text)
        ~Read(now, remaining) string
        ~Clear()
    }
    class ExpeditionRelay {
        <<static>>
        ~Prepare() Task$
        ~Host(transport, seats, southAmerica) Task~string~$
        ~Join(transport, code) Task$
    }
    class LanInvite {
        <<static>>
        ~Encode(address, port) string$
        ~TryDecode(code, address, port) bool$
    }
    class ExpeditionPackets {
        <<static>>
        +SnapshotSize(seats, wildlife) int$
        +WriteInput(writer, seat, input)$
        +TryReadInput(reader) bool$
        +WritePose(writer, pose)$
        +ReadPose(reader) DinosaurPose$
    }
    class DinosaurPose {
        <<struct>>
        +Position: Vector3
        +Yaw: float
        +Gait: float
        +Health: int
        +Activity: LifeActivity
        +Blend(from, to, fraction) DinosaurPose$
    }
    class DinosaurInput {
        <<struct>>
        +Direction: Vector3
        +Run: bool
        +Guard: bool
        +Crouch: bool
        +Act: bool
        +Lift: float
    }
    class NetworkManager {
        <<Netcode>>
    }
    class UnityTransport {
        <<Netcode>>
    }
    class Dinosaur {
        <<MonoBehaviour>>
    }
    class DinosaurAudio {
        <<MonoBehaviour>>
    }
    class ForestLighting {
        <<MonoBehaviour>>
    }
    class ValleyWeather {
        <<MonoBehaviour>>
    }
    class GroundTremor {
        <<MonoBehaviour>>
    }
    class FlightWind {
        <<MonoBehaviour>>
    }

    Expedition "1" *-- "6" Habitat : habitats
    Expedition "1" *-- "1" CoopMenu : crea en Start
    Expedition "1" *-- "1" FieldNotices : avisos del HUD
    Expedition "1" o-- "112" Dinosaur : population
    Expedition "1" --> "0..1" Dinosaur : active
    Expedition --> "5" DinosaurAudio : voices
    Expedition --> ExpeditionNetwork : usa y se suscribe a SeatChanged
    Expedition --> ForestLighting
    Expedition --> ValleyWeather
    Expedition --> GroundTremor
    Expedition --> FlightWind
    Expedition --> PsxView
    Expedition ..> ExpeditionMap
    Expedition ..> HudIcons
    Expedition ..> DinosaurInput
    Expedition ..> IPlayerControl : comandos
    CoopMenu --> ExpeditionNetwork
    ExpeditionNetwork "1" o-- "112" IReplicatedCreature : animals
    IPlayerControl <|-- IReplicatedCreature
    IPlayerControl <|.. ExpeditionNetwork
    IReplicatedCreature <|.. Dinosaur
    ExpeditionNetwork --> ForestLighting : reloj
    ExpeditionNetwork --> ValleyWeather : clima
    ExpeditionNetwork --> NetworkManager
    ExpeditionNetwork --> UnityTransport
    ExpeditionNetwork ..> ExpeditionPackets
    ExpeditionNetwork ..> ExpeditionRelay : código y datos de sala
    ExpeditionNetwork ..> LanInvite : código de red local
    ExpeditionPackets ..> DinosaurPose
    ExpeditionPackets ..> DinosaurInput
    Dinosaur ..> DinosaurPose : CapturePose, ApplyReplica
```

*Imagen exportada: [PNG](uml/A4_partida_red.png) · [SVG](uml/A4_partida_red.svg)*

`Expedition` lee los controles, coordina la cámara y el ciclo de vida, y dibuja los menús y el HUD. El control local y el control de red se usan a través de `IPlayerControl`. La cola de avisos del HUD vive en `FieldNotices`.

### A.5 Mundo, ambiente y audio

<!-- diagrama: A5_mundo_audio -->
```mermaid
%%{init: {"curve": "linear", "flowchart": {"curve": "linear"}, "state": {"curve": "linear"}, "class": {"curve": "linear"}}}%%
classDiagram
    direction LR
    class ForestLighting {
        <<MonoBehaviour>>
        ~Hour: float
        ~NightBlend: float
        ~Sight: float
        +TimeLabel: string
        +ToggleDayNight()
        +Apply(fog, light, range, delta, advanceClock, rain, flash)
        +Preview(fog, light, range, previewHour)
    }
    class ValleyWeather {
        <<MonoBehaviour>>
        ~Rain: float
        ~Wind: float
        ~Flash: float
        ~Rumble: float
        ~Label: string
        ~NextPhase()
        ~Synchronize(serverElapsed)
    }
    class ShaderGlobals {
        <<variables globales de shader>>
        _ForestNight
        _HabitatFog
        _HabitatLight
        _HabitatFogRange
        _ForestSunDirection
        _ValleyWeather
        _StormBolt
        _IslandLakes
    }
    class IslandLayout {
        <<static>>
        +Scale: float$
        +Radius: float$
        +LakeCount: int$
        +RegionAt(position) int$
        +ShoreDistance(point) float$
        +LagoonDistance(point) float$
        +SurfaceLevel(point) float$
        +NearestLake(point) Vector2$
        +Clearing(point) bool$
        +LimitVelocity(point, velocity) Vector3$
        +Publish()$
    }
    class ForestCanopy {
        <<MonoBehaviour>>
        -kinds: Kind[]
        -places: Vector3[]
        -models: ushort[]
        -cells: List~Cell~
        +Configure(species, positions, sizes, indices, trunkHeights)
        -GrowTrunks() IEnumerator
    }
    class Kind {
        <<struct>>
        +Parts: Part[]
        +Far: Part[]
        +Reach: float
    }
    class Part {
        <<struct>>
        +Mesh: Mesh
        +Material: Material
        +Pose: Matrix4x4
    }
    class Cell {
        +Center: Vector3
        +Bounds: Bounds
        +Trees: List~int~
    }
    class MeadowGrass {
        <<MonoBehaviour>>
        -cells: Dictionary~Vector2Int, MeshFilter~
        -spare: Stack~MeshFilter~
        +Configure(kinds, surfaces)
        -Grow(cell) MeshFilter
        -Plot() MeshFilter
    }
    class TriceratopsNavigation {
        <<MonoBehaviour>>
        -island: NavMeshData
        +Configure(data)
    }
    class DinosaurAudio {
        <<MonoBehaviour>>
        -creature: Dinosaur
        -ground: GroundTremor
        -chorus: List~DinosaurAudio~$
        -walkers: List~DinosaurAudio~$
        ~SetListener(ears)
        ~SetFocus(own)
        -PlayFootstep(gait, side, front)
        -Vocalize(attacking)
    }
    class GroundTremor {
        <<MonoBehaviour>>
        ~Bind(population)
        ~Footfall(sender, gait, mass)
        ~Sample(camera, delta) Vector3
    }
    class FlightWind {
        <<MonoBehaviour>>
        ~Mix(current, cameraRight, delta)
    }
    class ForestAmbience {
        <<MonoBehaviour>>
        -lighting: ForestLighting
        -weather: ValleyWeather
        -listener: Transform
        -UpdateMix()
    }
    class Dinosaur {
        <<MonoBehaviour>>
    }
    class DinosaurBehavior
    class PsxView {
        <<MonoBehaviour>>
    }

    ForestCanopy "1" *-- "1..*" Kind : kinds
    Kind "1" *-- "1..*" Part
    ForestCanopy "1" *-- "0..*" Cell : cells
    ForestLighting ..> ShaderGlobals : escribe
    ValleyWeather ..> ShaderGlobals : escribe
    IslandLayout ..> ShaderGlobals : escribe
    DinosaurBehavior ..> ShaderGlobals : lee _ForestNight
    ForestCanopy ..> ShaderGlobals : lee la niebla
    PsxView ..> ShaderGlobals : lee sol y clima
    ForestAmbience --> ForestLighting
    ForestAmbience --> ValleyWeather
    ForestAmbience ..> IslandLayout
    ForestLighting ..> IslandLayout
    DinosaurAudio "1" --> "1" Dinosaur : creature
    DinosaurAudio --> GroundTremor : Footfall
    GroundTremor o-- Dinosaur : animals
    FlightWind ..> Dinosaur
```

*Imagen exportada: [PNG](uml/A5_mundo_audio.png) · [SVG](uml/A5_mundo_audio.svg)*

`ShaderGlobals` no es una clase del proyecto: representa las variables globales de shader, que funcionan como una pizarra compartida. Se dibuja para dejar a la vista ese acoplamiento. `ForestCanopy` aplica el patrón **Flyweight**: guarda una vez cada especie de árbol (`Kind`, con sus mallas y materiales) y, por cada uno de los más de 50.000 árboles, solo la posición, la forma y el índice de su especie. `MeadowGrass` recicla las mallas de pasto con una pila (`spare`), un **Object Pool**.

### A.6 Generación de la escena (editor)

<!-- diagrama: A6_editor -->
```mermaid
%%{init: {"curve": "linear", "flowchart": {"curve": "linear"}, "state": {"curve": "linear"}, "class": {"curve": "linear"}}}%%
classDiagram
    direction LR
    class ExpeditionScene {
        <<static>>
        +ScenePath: string$
        -Animals: Animal[]$
        +Rebuild()$
        -Profile(attack, size, walk, run, turn) Profile$
        -PlaceCreature(animal, parent, footprint) Sighting$
        -AddWildlife(roster, shadow, contact) List~Sighting~$
        -MakeHabitats() Habitat[]$
        -ConfigureImports()$
    }
    class Animal {
        <<struct>>
        +Model: string
        +Name: string
        +Position: Vector3
        +Length: float
        +Traits: Profile
    }
    class ForestWorld {
        ~Trunks: List$
        +Create(root) Texture2D$
        +Height(x, z) float$
        +LoadSky(name) Texture2D$
        -AddTerrain()
        -AddRelief()
        -AddVegetation()
        -AddMeadow()
        -AddCanopy()
        -AddWater(name, position, size, bed, lagoon)
    }
    class ExpeditionAudioSetup {
        +AddCreature(creature, model, head, tail, length, ground)
        +AddTremor() GroundTremor
        +AddFlightWind(listener) FlightWind
        +AddWeather(listener) ValleyWeather
        +AddInterface(listener) AudioSource
        +AddForest(clock, listener, weather)
    }
    class ExpeditionAudioImport {
        <<AssetPostprocessor>>
        -OnPreprocessAudio()
    }
    class TriceratopsGrowthAssets {
        +Install(animal, herd, years, female, dormant)
    }
    class TriceratopsNavigationBake {
        <<static>>
        +Create(world)$
    }
    class AndroidBuild {
        <<static>>
        +Build()$
    }
    class EscenaExpedicion {
        <<artefacto>>
        Expedicion.unity
        Assets/JurassicPix/Generated
    }

    ExpeditionScene "1" *-- "5" Animal : Animals
    ExpeditionScene ..> ForestWorld : Create
    ExpeditionScene ..> ExpeditionAudioSetup
    ExpeditionScene ..> TriceratopsGrowthAssets
    ExpeditionScene ..> TriceratopsNavigationBake
    ExpeditionScene ..> EscenaExpedicion : genera
    TriceratopsNavigationBake ..> ForestWorld : Trunks
    AndroidBuild ..> ExpeditionScene
```

*Imagen exportada: [PNG](uml/A6_editor.png) · [SVG](uml/A6_editor.svg)*

`ExpeditionScene` cumple el rol de **fábrica** de todo el juego: arma la isla, instancia las 112 criaturas (104 vivas al empezar y 8 lugares reservados para nacimientos) y conecta cada componente con sus dependencias a través de campos `[SerializeField]` y métodos `Configure`. Es **inyección de dependencias** hecha por el generador de la escena en lugar de por el Inspector.

### A.7 Estados

Etapas de vida del jugador (`Dinosaur.Maturity`, `BeBorn`, `Survive`, y `Expedition.TrackLife`):

<!-- diagrama: A7_ciclo_de_vida -->
```mermaid
%%{init: {"curve": "linear", "flowchart": {"curve": "linear"}, "state": {"curve": "linear"}, "class": {"curve": "linear"}}}%%
stateDiagram-v2
    direction LR
    state "Menú de especies" as Menu
    state "Muerto" as Muerto
    state "Con vida" as Viva {
        state "Cría" as Cria
        state "Juvenil" as Juvenil
        state "Adulto" as Adulto
        [*] --> Cria
        Cria --> Juvenil : madurez ≥ 30 %
        Juvenil --> Adulto : madurez = 100 %
    }
    [*] --> Menu
    Menu --> Viva : Nacer
    Viva --> Muerto : vida = 0
    Muerto --> Viva : a los 12 s, cría de la misma especie
    Viva --> Menu : V, elegir otra especie
    Muerto --> Menu : V
```

*Imagen exportada: [PNG](uml/A7_ciclo_de_vida.png) · [SVG](uml/A7_ciclo_de_vida.svg)*

La madurez solo avanza mientras la sed y el hambre estén por debajo del 80 %; de cero a adulto lleva unos 8 minutos (480 s). El Quetzalcoatlus puede volar desde juvenil y solo un adulto puede liderar a su grupo (**L**).

Máquina de estados de la fauna silvestre que no es Triceratops (`DinosaurBehavior.Settle`). Cada cuadro, la conducta propone un estado y `Settle` decide si se cambia. El diagrama agrupa los diez estados y muestra las transiciones típicas:

<!-- diagrama: A7_conducta -->
```mermaid
%%{init: {"curve": "linear", "flowchart": {"curve": "linear"}, "state": {"curve": "linear"}, "class": {"curve": "linear"}}}%%
stateDiagram-v2
    direction LR
    state "Rutina" as Rutina {
        Idle
        Travel
        Drink
        Eat
        Rest
    }
    state "Caza" as Caza {
        Stalk
        Chase
    }
    state "Defensa" as Defensa {
        Guard
        Flee
    }
    [*] --> Rutina
    Rutina --> Caza : hambre y presa a la vista
    Caza --> Rutina : la presa escapa o se rinde
    Caza --> Fight : quiere golpear
    Fight --> Caza : termina el ataque
    Fight --> Rutina : la presa muere, come
    Rutina --> Defensa : amenaza detectada o herida
    Caza --> Defensa : depredador mayor
    Defensa --> Fight : en guardia, quiere golpear
    Fight --> Defensa : sigue la amenaza
    Defensa --> Rutina : pasó el peligro
    note right of Defensa
        Fight y Flee interrumpen a cualquier estado.
        Los demás cambios esperan una permanencia mínima.
        Flee 2,5 s. Stalk y Guard 1,2 s.
        Drink, Eat y Rest 1,5 s. El resto 0,4 s.
    end note
```

*Imagen exportada: [PNG](uml/A7_conducta.png) · [SVG](uml/A7_conducta.svg)*

Táctica de una manada de Triceratops (`TriceratopsHerd.UpdateTactic`):

<!-- diagrama: A7_tactica_manada -->
```mermaid
%%{init: {"curve": "linear", "flowchart": {"curve": "linear"}, "state": {"curve": "linear"}, "class": {"curve": "linear"}}}%%
stateDiagram-v2
    direction LR
    [*] --> Calm
    Calm --> Alert : depredador detectado
    Alert --> Stampede : a menos de 27 m o hirió a un miembro
    Stampede --> Alert : la manada puso 46 m de distancia
    Alert --> Calm : se perdió la amenaza
    Stampede --> Calm : se perdió la amenaza
    note right of Stampede
        La manada huye detrás del líder.
        Uno o dos adultos sanos cubren la retirada.
    end note
```

*Imagen exportada: [PNG](uml/A7_tactica_manada.png) · [SVG](uml/A7_tactica_manada.svg)*

### A.8 Secuencia: un ataque del jugador en modo individual

<!-- diagrama: A8_secuencia_ataque -->
```mermaid
%%{init: {"curve": "linear", "flowchart": {"curve": "linear"}, "state": {"curve": "linear"}, "class": {"curve": "linear"}}}%%
sequenceDiagram
    autonumber
    actor Jugador
    participant Exp as Expedition
    participant Atq as Dinosaur (atacante)
    participant Pre as Dinosaur (presa)
    participant Vit as DinosaurVitals
    participant Beh as DinosaurBehavior (presa)
    participant Man as TriceratopsHerd
    participant Aud as DinosaurAudio

    Jugador->>Exp: clic izquierdo o F
    Exp->>Exp: ReadDesktop() marca attackRequested
    Exp->>Atq: SetInput(direction, run, ...)
    Exp->>Atq: TryAttack()
    Note over Atq: Update(), cuadros siguientes
    Atq->>Atq: AdvanceAttack(dt)
    Atq->>Atq: DeliverImpact() busca vecinos al alcance
    Atq->>Pre: ReceiveHit(attacker, damage, shove)
    Pre->>Vit: Hit(damage, armor, guardMultiplier)
    Vit-->>Pre: daño real
    Pre->>Beh: Provoke(attacker)
    opt la presa es un Triceratops
        Pre->>Man: Warn(attacker)
    end
    Pre-->>Atq: daño real, AttackConnected = true
    Note over Aud: LateUpdate()
    Aud->>Atq: consulta AttackConnected
    Aud->>Aud: reproduce el impacto
```

*Imagen exportada: [PNG](uml/A8_secuencia_ataque.png) · [SVG](uml/A8_secuencia_ataque.svg)*

En red, los pasos 3 y 4 cambian: `Expedition` llama a `ExpeditionNetwork.SetInput` y `Attack`, el anfitrión ejecuta el golpe y los invitados lo ven en la instantánea siguiente. Los últimos dos pasos muestran que el audio **consulta** el estado cada cuadro en lugar de recibir un aviso.

## 3. Evaluación SOLID del código actual

La evaluación combina el relevamiento del 22/11 con los contratos y la cola incorporados en la entrega del 23/11.

### S — Responsabilidad única

| | Clase | Observación |
| --- | --- | --- |
| Cumple | `DinosaurVitals` (37 líneas) | Solo vida: daño con armadura y guardia, límites, redimensionado. De Unity solo usa `Mathf`. |
| Cumple | `DinosaurMotion` | Solo animación y pose del esqueleto. |
| Cumple | `ExpeditionPackets` | Solo serializa entradas y poses. |
| Cumple | `FieldNotices` | Ordena los mensajes y controla su tiempo de exposición en el HUD. |
| Cumple | `IslandLayout`, `ForestLighting`, `ValleyWeather`, `ForestCanopy`, `MeadowGrass`, `GroundTremor`, `FlightWind` | Cada una resuelve un tema. |
| No cumple | `Expedition` (1.384 líneas en dos archivos, unos 65 métodos) | Entrada de escritorio y táctil, cámara, menú, HUD, panel, pantalla de muerte, flujo de la partida, atmósfera por región y enrutamiento a la red. Tiene por lo menos siete motivos distintos para cambiar. |
| No cumple | `Dinosaur` (860 líneas) | Movimiento, vuelo, aguante, sed y hambre, crecimiento, combate, agarre del Velociraptor, muerte y reaparición, y réplica de red. |
| No cumple | `DinosaurBehavior` (773 líneas) | Percepción, caza, defensa, vuelo, recados y evasión de obstáculos para los cuatro temperamentos en una sola clase. |
| Parcial | `TriceratopsHerd` (828 líneas) | Táctica, liderazgo, recados y reproducción. Es cohesiva (todo es "la manada"), pero la reproducción podría separarse. |

### O — Abierto/cerrado

| | Dónde | Observación |
| --- | --- | --- |
| Cumple | `Dinosaur.Profile` y `ExpeditionScene.Animals` | Los valores de cada especie son datos. Ajustar una especie, o agregar otra con un temperamento que ya existe, no toca la lógica. |
| Cumple | `ForestCanopy.Kind`, `MeadowGrass.Tuft` | Agregar un árbol o una mata es agregar datos. |
| Cumple | `IPlayerControl` | El código que emite los comandos funciona con cualquier implementación compatible. |
| No cumple | `switch (animal.Traits.Nature)` en `DinosaurBehavior.Steer` | Un temperamento nuevo obliga a modificar la clase. |
| No cumple | 54 líneas en 8 archivos que preguntan `Temperament`, `CanFly` o `TailStrike` | La lógica de "qué clase de animal es" está repartida entre `Dinosaur`, `DinosaurBehavior`, `Expedition`, `ExpeditionMap`, `ExpeditionMenu`, `IslandSites`, `TriceratopsHerd` y `WildlifeHerd`. |
| No cumple | 38 líneas en 8 archivos con `Life != null` | El Triceratops se trata como excepción en cada lugar donde importa. |
| No cumple | `ExpeditionMenu`: arreglos paralelos `Diets`, `Kinds`, `Blurbs`, `BirthTips`, `Difficulty` | Los textos de cada especie se emparejan por posición. Una sexta especie exige editar cinco arreglos en el orden exacto. |
| No cumple | `IslandSites.Prey`: `name == "Triceratops"` | Regla escrita contra el nombre de una especie. |

### L — Sustitución de Liskov

`Expedition` emite el mismo conjunto de comandos haya control local con `Dinosaur` o control en red con `ExpeditionNetwork`. Ambos aceptan `DinosaurInput` y ofrecen ataque, vuelo y liderazgo según las reglas del juego. Las dos clases de manada siguen usando referencias y reglas propias.

### I — Segregación de interfaces

El emisor de comandos depende de `IPlayerControl`, que ofrece cuatro operaciones. La red usa `IReplicatedCreature` para agregar asignación y poses. Otros consumidores todavía reciben el `Dinosaur` completo:

| Consumidor | Qué necesita en realidad |
| --- | --- |
| `ExpeditionMap` | Posición, rumbo, especie, grupo, si está muerto |
| `GroundTremor` | Posición y largo |
| `FlightWind` | Posición y si vuela |
| `DinosaurAudio` | Fases de paso, ataque, actividad y tamaño |

### D — Inversión de dependencias

| | Dónde | Observación |
| --- | --- | --- |
| Cumple | Campos `[SerializeField]` y métodos `Configure` | Las referencias entre componentes las inyecta `ExpeditionScene`; las clases no se buscan entre sí con `FindObjectOfType`. |
| Cumple | `ExpeditionNetwork.SeatChanged` | La red avisa por evento y no conoce a `Expedition`. |
| Cumple | `DinosaurBehavior(Dinosaur animal)`, `CoopMenu(ExpeditionNetwork network)` | Dependencias recibidas por constructor. |
| No cumple | `Dinosaur.Focus`, `Sight`, `Throttle` (estáticos) | `Expedition` los escribe cada cuadro y todas las criaturas los leen. Es estado global. |
| No cumple | `IslandSites.Communities` y `Herds` (listas estáticas) | Registro global que `Expedition.Start` limpia y las manadas llenan. |
| No cumple | `Shader.GetGlobalFloat("_ForestNight")` en `DinosaurBehavior`, `TriceratopsHerd` y `WildlifeHerd` | La IA averigua si es de noche leyendo una variable de shader que escribe `ForestLighting`. La dependencia real (IA → reloj) no figura en ninguna firma. |
| No cumple | `Camera.main` en `ForestCanopy` y `MeadowGrass` | Dependencia global del motor. |
| Cumple en control y réplica | `IPlayerControl` e `IReplicatedCreature` | Los contratos son del núcleo. Presentación y red dependen de ellos. La sesión se configura con el componente de red en la escena. |
| No cumple | `IslandLayout` estático | Las reglas de la isla no se pueden reemplazar por otras en una prueba. |

### Resumen

| Principio | Estado actual | Prioridad de mejora |
| --- | --- | --- |
| S | Bien en las clases chicas; mal en `Expedition`, `Dinosaur` y `DinosaurBehavior` | Alta |
| O | Bien en los datos; mal en la lógica por especie | Alta |
| L | El mismo consumidor usa control local y de red | Media |
| I | Contratos acotados para comandos y réplica | Media |
| D | Contratos del núcleo e inyección por escena en control y red. Queda estado global del mundo | Media |

## 4. Parte B — Diseño propuesto aplicando SOLID

Criterios del rediseño:

- **Componer, no heredar.** Se mantiene una sola clase `Dinosaur` y las diferencias entre especies pasan a ser piezas intercambiables detrás de interfaces. No se crea una jerarquía `Dinosaur → Herbívoro → Triceratops`: un pterosaurio cazador o un saurópodo que barre con la cola no entran limpios en un árbol de herencia.
- **Herencia solo de un nivel y solo para contratos** (interfaces y fábricas abstractas).
- **Respetar las restricciones del proyecto:** la escena se sigue generando por código, el anfitrión sigue simulando la fauna y se conserva la simulación a ritmo reducido de lo que no se ve.
- **No reintroducir** el modo explorador ni el cambio libre de criatura.

### B.1 La criatura por composición

<!-- diagrama: B1_criatura -->
```mermaid
%%{init: {"curve": "linear", "flowchart": {"curve": "linear"}, "state": {"curve": "linear"}, "class": {"curve": "linear"}}}%%
classDiagram
    direction LR
    class ICreatureView {
        <<interface>>
        +Position: Vector3
        +Facing: Vector3
        +Species: SpeciesDefinition
        +Size: float
        +IsDead: bool
        +IsFlying: bool
        +Group: IHerd
    }
    class IDamageable {
        <<interface>>
        +ReceiveHit(hit) int
    }
    class IControllable {
        <<interface>>
        +SetInput(input)
        +TryAttack() bool
        +ToggleFlight()
        +SetLeading(lead)
    }
    class Dinosaur {
        <<MonoBehaviour>>
        +Species: SpeciesDefinition
        +Damaged: event Action~HitResult~
        +Died: event Action~Dinosaur~
        +StageChanged: event Action~LifeStage~
        +Stepped: event Action~Footfall~
        +Initialize(species, brain, growth, services)
        -Update()
    }
    class SpeciesDefinition {
        <<ScriptableObject>>
        +DisplayName: string
        +Diet: Diet
        +MenuBlurb: string
        +BirthTip: string
        +Difficulty: int
        +Stats: SpeciesStats
        +Brain: BrainFactory
        +Attack: AttackFactory
        +CanFly: bool
    }
    class DinosaurVitals {
        +Health: int
        +Maximum: int
        +Hit(damage, resistance, guardMultiplier) int
    }
    class Stamina {
        +Level: float
        +Winded: bool
        +Tick(delta, sprinting, resting)
    }
    class Needs {
        +Thirst: float
        +Hunger: float
        +Starving: bool
        +Tick(delta, drinking, eating)
    }
    class DinosaurMotion {
        +Evaluate(delta, pose)
    }
    class IBrain {
        <<interface>>
        +Decide(delta) Intent
        +Provoke(source)
    }
    class ILocomotion {
        <<interface>>
        +Move(intent, delta)
    }
    class GroundLocomotion
    class FlightLocomotion
    class IAttack {
        <<interface>>
        +CanStart: bool
        +Begin()
        +Tick(delta)
    }
    class IGrowth {
        <<interface>>
        +Maturity: float
        +Stage: LifeStage
        +Scale: float
        +Advance(delta, needs)
    }
    class SurvivalGrowth
    class FixedGrowth
    class TriceratopsLife {
        <<MonoBehaviour>>
    }
    class IHerd {
        <<interface>>
        +Center: Vector3
        +Leader: ICreatureView
        +Count: int
        +Thirst: float
        +Hunger: float
        +PostFor(member) Vector3
        +Warn(attacker)
    }
    class TriceratopsHerd {
        <<MonoBehaviour>>
    }
    class WildlifeHerd {
        <<MonoBehaviour>>
    }

    ICreatureView <|.. Dinosaur
    IDamageable <|.. Dinosaur
    IControllable <|.. Dinosaur
    Dinosaur "0..*" --> "1" SpeciesDefinition : compartida
    Dinosaur "1" *-- "1" DinosaurVitals
    Dinosaur "1" *-- "1" Stamina
    Dinosaur "1" *-- "1" Needs
    Dinosaur "1" *-- "1" DinosaurMotion
    Dinosaur "1" --> "1" IBrain
    Dinosaur "1" --> "1..2" ILocomotion
    Dinosaur "1" --> "1" IAttack
    Dinosaur "1" --> "1" IGrowth
    Dinosaur "0..*" --> "0..1" IHerd
    ILocomotion <|.. GroundLocomotion
    ILocomotion <|.. FlightLocomotion
    IGrowth <|.. SurvivalGrowth
    IGrowth <|.. FixedGrowth
    IGrowth <|.. TriceratopsLife
    IHerd <|.. TriceratopsHerd
    IHerd <|.. WildlifeHerd
    IHerd o-- ICreatureView : miembros
    IAttack ..> IDamageable : golpea
```

*Imagen exportada: [PNG](uml/B1_criatura.png) · [SVG](uml/B1_criatura.svg)*

Qué cambia respecto de A.3:

- `Dinosaur` deja de calcular todo y pasa a **coordinar** piezas. `Stamina` y `Needs` salen de sus campos privados y quedan como clases chicas que se pueden probar sin escena, igual que hoy `DinosaurVitals`.
- `SpeciesDefinition` es un `ScriptableObject` **compartido** por todos los individuos de la especie (Flyweight). Reemplaza a `Dinosaur.Profile`, que hoy se serializa una vez por criatura, y a los cinco arreglos paralelos del menú.
- `IGrowth` reemplaza a los `Life != null ? ... : ...`: el jugador usa `SurvivalGrowth` (crece si come y bebe), la fauna silvestre `FixedGrowth` y el Triceratops su `TriceratopsLife`.
- `IHerd` da un tipo común a las dos manadas: desaparecen los `object` y los `is TriceratopsHerd`.
- Los consumidores piden la interfaz mínima: el minimapa, el temblor y el viento reciben `ICreatureView`; los ataques, `IDamageable`; la entrada y la red, `IControllable`.
- Los **eventos** (`Damaged`, `Died`, `StageChanged`, `Stepped`) reemplazan la consulta por cuadro de `DinosaurAudio`, `GroundTremor` y el HUD (patrón Observer con delegados de C#).

### B.2 Conducta y ataque: estrategias y fábricas

<!-- diagrama: B2_estrategias -->
```mermaid
%%{init: {"curve": "linear", "flowchart": {"curve": "linear"}, "state": {"curve": "linear"}, "class": {"curve": "linear"}}}%%
classDiagram
    direction TB
    class SpeciesDefinition {
        <<ScriptableObject>>
        +Brain: BrainFactory
        +Attack: AttackFactory
    }
    class BrainFactory {
        <<abstract, ScriptableObject>>
        +Create(body, services)* IBrain
    }
    class GrazerBrainFactory
    class PackHunterBrainFactory
    class SolitaryHunterBrainFactory
    class FlyerBrainFactory
    class TriceratopsBrainFactory
    class IBrain {
        <<interface>>
        +Decide(delta) Intent
        +Provoke(source)
    }
    class PlayerBrain {
        +SetInput(input)
    }
    class ReplicaBrain {
        +Apply(pose)
    }
    class GrazerBrain
    class PackHunterBrain
    class SolitaryHunterBrain
    class FlyerBrain
    class TriceratopsBrain
    class Senses {
        +Scan(neighbors) Perception
    }
    class Steering {
        +AvoidSlopes(heading) Vector3
        +AvoidTrunks(heading) Vector3
        +Separate(neighbors) Vector3
    }
    class IBehaviorState {
        <<interface>>
        +MinimumStay: float
        +Enter()
        +Tick(delta) Intent
    }
    class StateMachine {
        +Current: IBehaviorState
        +Propose(next)
    }
    class AttackFactory {
        <<abstract, ScriptableObject>>
        +Create(body)* IAttack
    }
    class IAttack {
        <<interface>>
        +CanStart: bool
        +Begin()
        +Tick(delta)
    }
    class BiteAttack
    class TailSweepAttack
    class PounceAttack
    class DiveAttack

    SpeciesDefinition --> BrainFactory
    SpeciesDefinition --> AttackFactory
    BrainFactory <|-- GrazerBrainFactory
    BrainFactory <|-- PackHunterBrainFactory
    BrainFactory <|-- SolitaryHunterBrainFactory
    BrainFactory <|-- FlyerBrainFactory
    BrainFactory <|-- TriceratopsBrainFactory
    BrainFactory ..> IBrain : crea
    IBrain <|.. PlayerBrain
    IBrain <|.. ReplicaBrain
    IBrain <|.. GrazerBrain
    IBrain <|.. PackHunterBrain
    IBrain <|.. SolitaryHunterBrain
    IBrain <|.. FlyerBrain
    IBrain <|.. TriceratopsBrain
    PackHunterBrain *-- StateMachine
    SolitaryHunterBrain *-- StateMachine
    GrazerBrain *-- StateMachine
    StateMachine o-- IBehaviorState
    PackHunterBrain --> Senses
    PackHunterBrain --> Steering
    SolitaryHunterBrain --> Senses
    SolitaryHunterBrain --> Steering
    GrazerBrain --> Senses
    GrazerBrain --> Steering
    FlyerBrain --> Senses
    AttackFactory ..> IAttack : crea
    IAttack <|.. BiteAttack
    IAttack <|.. TailSweepAttack
    IAttack <|.. PounceAttack
    IAttack <|.. DiveAttack
```

*Imagen exportada: [PNG](uml/B2_estrategias.png) · [SVG](uml/B2_estrategias.svg)*

- **Strategy:** cada temperamento es una clase que implementa `IBrain`. El `switch (Traits.Nature)` desaparece. `PlayerBrain` y `ReplicaBrain` cubren los casos que hoy se resuelven consultando `IsControlled` (26 líneas de `Dinosaur`) y `replica` (14 líneas).
- **Factory Method:** `BrainFactory.Create` y `AttackFactory.Create` son los métodos fábrica; cada subclase concreta decide qué estrategia instanciar. La especie elige su fábrica desde el asset, así que una especie nueva con conducta nueva se agrega **sin modificar** `Dinosaur` (principio abierto/cerrado).
- **State:** los diez valores de `BehaviorState` pasan a ser objetos con su permanencia mínima, y `StateMachine` conserva la regla que hoy vive en `Settle`.
- `Senses` y `Steering` agrupan lo que los cerebros comparten (percepción y evasión de pendientes, troncos y vecinos) para no duplicarlo.
- `PounceAttack` cubre el salto con agarre del Velociraptor, `TailSweepAttack` el barrido del Argentinosaurus (golpea a todos los que alcanza) y `DiveAttack` el picado del Quetzalcoatlus.

### B.3 Partida, entrada, interfaz y servicios

<!-- diagrama: B3_partida -->
```mermaid
%%{init: {"curve": "linear", "flowchart": {"curve": "linear"}, "state": {"curve": "linear"}, "class": {"curve": "linear"}}}%%
classDiagram
    direction LR
    class GameSession {
        <<MonoBehaviour>>
        +Phase: SessionPhase
        +Active: Dinosaur
        +ChooseSpecies(species)
        +Respawn()
        -Update()
    }
    class IPlayerInput {
        <<interface>>
        +Read() PlayerCommand
    }
    class DesktopInput
    class TouchInput
    class ICreatureControl {
        <<interface>>
        +Send(input)
        +Attack()
        +ToggleFlight()
        +SetLeading(lead)
        +Select(species)
    }
    class LocalControl
    class NetworkControl
    class INetSession {
        <<interface>>
        +Online: bool
        +LocalSeat: int
        +SeatChanged: event Action~int~
        +Host(seat, port) bool
        +Join(address, port) bool
        +Leave()
    }
    class ExpeditionNetwork {
        <<MonoBehaviour>>
    }
    class CreatureCamera {
        <<MonoBehaviour>>
        +Follow(target, cut)
        +Orbit(delta)
        +Showcase(target)
    }
    class AtmosphereDirector {
        +Tick(subject, delta)
    }
    class IScreen {
        <<interface>>
        +Draw()
        +HandlePress(point) bool
    }
    class SpeciesMenuScreen
    class FieldHud
    class PausePanel
    class DeathScreen
    class CoopMenu
    class IDayClock {
        <<interface>>
        +Hour: float
        +IsNight: bool
        +Sight: float
        +ToggleDayNight()
    }
    class ForestLighting {
        <<MonoBehaviour>>
    }
    class IWeather {
        <<interface>>
        +Rain: float
        +Wind: float
        +NextPhase()
    }
    class ValleyWeather {
        <<MonoBehaviour>>
    }
    class IIsland {
        <<interface>>
        +RegionAt(position) int
        +SurfaceLevel(point) float
        +NearestWater(from) Vector3
        +NearestDryLand(from) Vector3
    }
    class Island
    class IViewer {
        <<interface>>
        +Focus: Vector3
        +Sight: float
        +Throttle: bool
    }
    class IHerdRegistry {
        <<interface>>
        +All: IReadOnlyList~IHerd~
        +PreyFor(hunter) IHerd
    }
    class HerdRegistry
    class Dinosaur {
        <<MonoBehaviour>>
    }
    class Cerebros {
        <<implementaciones de IBrain>>
    }

    GameSession --> IPlayerInput
    GameSession --> ICreatureControl
    GameSession --> INetSession
    GameSession --> CreatureCamera
    GameSession --> AtmosphereDirector
    GameSession o-- IScreen : pantallas
    IPlayerInput <|.. DesktopInput
    IPlayerInput <|.. TouchInput
    ICreatureControl <|.. LocalControl
    ICreatureControl <|.. NetworkControl
    NetworkControl --> INetSession
    INetSession <|.. ExpeditionNetwork
    IScreen <|.. SpeciesMenuScreen
    IScreen <|.. FieldHud
    IScreen <|.. PausePanel
    IScreen <|.. DeathScreen
    IScreen <|.. CoopMenu
    AtmosphereDirector --> IDayClock
    AtmosphereDirector --> IWeather
    AtmosphereDirector --> IIsland
    IDayClock <|.. ForestLighting
    IWeather <|.. ValleyWeather
    IIsland <|.. Island
    IViewer <|.. CreatureCamera
    IHerdRegistry <|.. HerdRegistry
    LocalControl ..> Dinosaur : IControllable
    Dinosaur ..> IViewer : simulación reducida
    Cerebros ..> IDayClock : es de noche
    Cerebros ..> IIsland
    Cerebros ..> IHerdRegistry : presas
```

*Imagen exportada: [PNG](uml/B3_partida.png) · [SVG](uml/B3_partida.svg)*

- `Expedition` se divide en `GameSession` (el flujo menú → vida → muerte → renacer), una fuente de entrada (`IPlayerInput`), la cámara, la atmósfera y una pantalla por cada cosa que hoy dibuja `OnGUI`. Cada pieza tiene un solo motivo de cambio.
- `ICreatureControl` reemplaza las 16 preguntas a `network.Online`, `IsGuest` y `Dedicated`: en modo individual se instala `LocalControl` y en red `NetworkControl`, y el resto del código no se entera.
- Los **servicios** (`IDayClock`, `IWeather`, `IIsland`, `IViewer`, `IHerdRegistry`) son las abstracciones de las que pasa a depender la fauna. Reemplazan a `Shader.GetGlobalFloat("_ForestNight")`, a los estáticos `Dinosaur.Focus`, `Sight` y `Throttle`, a `IslandSites` y a las llamadas directas a `IslandLayout`. Las variables globales de shader siguen existiendo, pero solo para los shaders.
- La inyección la sigue haciendo `ExpeditionScene` al generar la escena, que es quien ya conoce todas las piezas.

### B.4 Qué principio resuelve cada decisión

| Principio | Problema actual (parte A) | Decisión de diseño (parte B) | Patrón |
| --- | --- | --- | --- |
| **S** | `Expedition` con siete responsabilidades | `GameSession`, `IPlayerInput`, `CreatureCamera`, `AtmosphereDirector`, una clase por pantalla | — |
| **S** | `Dinosaur` calcula aguante, sed, hambre y crecimiento | `Stamina`, `Needs`, `IGrowth`, `ILocomotion`, `IAttack` | Componentes |
| **S** | `DinosaurBehavior` con cuatro temperamentos | Un `IBrain` por temperamento, más `Senses` y `Steering` | Strategy |
| **O** | `switch` de temperamento y 54 consultas de tipo | `BrainFactory` y `AttackFactory` elegidas por la especie | Factory Method, Strategy |
| **O** | Arreglos paralelos del menú | `SpeciesDefinition` (`ScriptableObject`) | Flyweight, composición basada en datos |
| **O** | Diez estados en un `enum` con reglas en `Settle` | `IBehaviorState` y `StateMachine` | State |
| **L** | Manadas guardadas como `object` | `IHerd`, implementada por ambas manadas | — |
| **L** | Tres fuentes de crecimiento con ternarios | `IGrowth` con tres implementaciones intercambiables | — |
| **I** | Todos reciben el `Dinosaur` completo | `ICreatureView`, `IDamageable`, `IControllable` | — |
| **I** | El audio y el HUD consultan cada cuadro | Eventos `Damaged`, `Died`, `StageChanged`, `Stepped` | Observer |
| **D** | Estáticos `Focus`, `Sight`, `Throttle` | `IViewer` inyectado | Inyección de dependencias |
| **D** | La IA lee `_ForestNight` del shader | `IDayClock` inyectado | Inyección de dependencias |
| **D** | `Expedition` atado a `ExpeditionNetwork` | `ICreatureControl` e `INetSession` | Strategy |
| **D** | `IslandSites` e `IslandLayout` estáticos | `IHerdRegistry` e `IIsland` | — |

### B.5 Cuidados al implementarlo en Unity

- Unity no serializa campos de tipo interfaz. Las estrategias se eligen con fábricas `ScriptableObject` (como en B.2) o con `[SerializeReference]`; las dependencias entre componentes se siguen pasando con métodos `Configure` desde `ExpeditionScene`.
- Disparar un evento de C# no genera basura, pero suscribirse con una lambda que captura variables sí: conviene suscribir métodos con nombre en `OnEnable` y desuscribir en `OnDisable`.
- Con 112 criaturas, una llamada por interfaz por cuadro no es un costo medible. Lo que sí hay que conservar es la simulación reducida de lo que no se ve (`Throttle`), que en el diseño nuevo consulta a `IViewer`.
- Cambiar `Dinosaur.Profile` por `SpeciesDefinition` cambia datos serializados: hay que regenerar la escena.

## 5. Plan de migración

Pasos chicos; cada uno deja el juego funcionando. Están ordenados de menor a mayor riesgo.

| # | Paso | Principio | Riesgo | Cómo verificar |
| --- | --- | --- | --- | --- |
| 1 | Crear `IHerd` y hacer que `TriceratopsHerd` y `WildlifeHerd` la implementen. Reemplazar los `object quarry` | L, I | Bajo | Los depredadores siguen eligiendo manadas en el minimapa |
| 2 | Extraer `Stamina` y `Needs` de `Dinosaur`, con pruebas de edición (Unity Test Framework) junto a `DinosaurVitals` y `ExpeditionPackets` | S | Bajo | Pruebas unitarias |
| 3 | Agregar los eventos `Damaged`, `Died`, `StageChanged` y suscribir el HUD y el audio | I, D | Bajo | Los sonidos de impacto y los avisos de etapa siguen apareciendo |
| 4 | Separar `Expedition`: `IPlayerInput` (escritorio y táctil), `CreatureCamera`, pantallas `IScreen` | S | Medio | Recorrer todos los controles del README en PC |
| 5 | `ICreatureControl` con `LocalControl` y `NetworkControl` | D | Medio | Prueba con dos instancias en `127.0.0.1` |
| 6 | `SpeciesDefinition` como `ScriptableObject`, generado por `ExpeditionScene` | O | Medio | Regenerar la escena y comparar las barras del menú |
| 7 | `IDayClock`, `IWeather`, `IViewer`, `IIsland` inyectados; quitar estáticos | D | Medio | Medir con el Profiler que el cuadro siga en unos 21 ms |
| 8 | `IBrain` por temperamento y `StateMachine` | S, O | Alto | Comparar conductas con las descriptas en el README |
| 9 | `IAttack`, `ILocomotion`, `IGrowth` | S, O, L | Alto | Combate, vuelo y crecimiento de las cinco especies |

## 6. Vínculo con los ejes de la cátedra

| Eje de la Actividad N.º 1 | Dónde se ve en este documento |
| --- | --- |
| UML: los 6 tipos de relaciones | Sección 1 y todos los diagramas de clases |
| Herencia: peligro de las jerarquías profundas | A.2: profundidad 1; parte B: se decide no crear una jerarquía de especies |
| Polimorfismo e interfaces | `IPlayerControl` e `IReplicatedCreature` en A.4. `IBrain` e `IHerd` en el diseño propuesto |
| Composición sobre herencia, arquitectura por componentes | `Dinosaur` + `TriceratopsLife` + `DinosaurAudio` + `Sighting` en un mismo GameObject |
| Herencia versus componentes: criterio | Sección 4, criterios del rediseño |
| Patrón Observer y delegados de C# | `ExpeditionNetwork.SeatChanged` (actual); eventos de `Dinosaur` (propuesto) |
| Patrón Object Factory | `ExpeditionScene.PlaceCreature` (actual); `BrainFactory` y `AttackFactory` (propuesto) |
| Patrón Flyweight | `ForestCanopy.Kind` (actual); `SpeciesDefinition` (propuesto) |
| Patrón Singleton | No se usa. Su función la cumplen clases estáticas (`IslandLayout`, `IslandSites`), que la parte B reemplaza por servicios inyectados |
| Composición basada en datos, ScriptableObjects, Prefabs | `Dinosaur.Profile` y la escena generada (actual); `SpeciesDefinition` (propuesto) |
| `[SerializeField]` y flujo de trabajo de diseño | Los campos ajustables (`TriceratopsHerd.incubation`, `ValleyWeather.rainChance`, los de `PsxView`) |
| Modelos arquitectónicos: ad-hoc, modular, DAG, por capas | A.1 |
| Código de motor versus código de juego | A.1: el bloque "Motor" frente a los seis paquetes propios |
| Máquinas de estados | A.7 |

## 7. Referencias y uso de IA

1. Universidad Nacional de Jujuy, Facultad de Ingeniería (2026). *Actividad N.º 1: Conceptos introductorios y arquitectura de software*. Consigna de Programación de Videojuegos II.
2. Rabin, S. (ed.). *Introduction to Game Development*. Parte III: Game Programming.
3. Vega, A. A. *Principios SOLID: Single Responsibility Principle; Open Closed Principle y Factory Method; Sustitución de Liskov*. Material de cátedra.
4. Unity Technologies. *Level up your code with design patterns and SOLID*. E-book compartido como material de consulta.
5. Material de cátedra. *Introducción a los patrones de diseño*.
6. Código fuente de Jurassic Pix: `Assets/JurassicPix/Scripts` y `Assets/JurassicPix/Editor`.

**Uso de inteligencia artificial generativa.** El relevamiento del código, el armado de los diagramas en Mermaid y la redacción inicial de este documento se hicieron con un asistente de programación basado en IA (Devin), a partir del código del proyecto y de la consigna de la cátedra. Los integrantes deben revisar los diagramas contra el código, contrastar los conceptos con las fuentes citadas y ajustar el texto antes de entregarlo.
