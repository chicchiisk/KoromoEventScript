# KoromoEventScript (KSE) Specification

KoromoEventScript (KSE) is a scenario DSL for RPG and visual novel style games. It is designed to let scenario writers write large amounts of dialogue and direction smoothly while keeping the grammar explicit, parseable, localizable, and friendly to Git review.

## Goals

- One event per `.kse` script file.
- Low typing burden for scenario writers.
- Avoid symbol-heavy syntax.
- Avoid relying on blank lines or indentation for semantics.
- Keep blocks explicit with `{}`.
- Separate scene metadata from runtime commands.
- Support localization from the language design level.
- Make resource preloading possible per scene.
- Provide a safe and readable parallel execution model.

## File Model

One event corresponds to one `.kse` file.

The script file name is the event name.

```txt
prologue_001.kse
```

The file above defines the event `prologue_001`.

## Top-Level Structure

A `.kse` file may contain:

- `use` declarations
- `actor` definitions
- `macro` definitions
- `scene` definitions

Example:

```kse
use common
use battle_common

actor A : Alice {
    nameKey char.alice
    sprite alice
}

actor G : Guard {
    nameKey char.guard
    sprite guard
}

scene arrival {
    setup {
        cast {
            A
            G
        }

        bg: royal_gate_evening
        transition: fade 1.0

        init {
            A: left normal hidden
            G: right serious hidden
            camera: wide
            bgm: capital_evening
        }
    }

    together {
        show A
        pan A 0.5
        se cloth
    }

    nar #arrival_001 {
        王都の門には、夕陽が長い影を落としていた。
    }

    say A #arrival_002 {
        ここが王都……。
    }
}
```

## Imports

```kse
use common
use battle_common
```

`use` imports shared macro libraries, actor definitions, and other reusable declarations.

## Actor Definitions

Actors define local script IDs that refer to game-side master character IDs.

```kse
actor A : Alice {
    nameKey char.alice
    sprite alice
}
```

| Element | Meaning |
|---|---|
| `A` | Local actor ID used in this script |
| `Alice` | Game-side master actor ID |
| `nameKey` | Localization key for display name |
| `sprite` | Default sprite set |

### Full Actor Example

```kse
actor A : Alice {
    nameKey char.alice
    sprite alice
    voice alice
    color "#ffccdd"

    default {
        sprite normal
        face normal
        pos left
    }

    sprites {
        normal  = alice_normal
        uniform = alice_uniform
        battle  = alice_battle
    }

    faces {
        normal
        smile
        angry
        sad
        serious = angry_02
    }

    render {
        offset x:0 y:20
        scale 0.95
        layer character
        z 10
        lipSync true
        blink true
    }

    tags [heroine, party]
}
```

### Actor Reuse

Multiple local actors may refer to the same master actor if they represent different costumes or runtime states.

```kse
actor A : Alice {
    sprite alice_normal
}

actor AB : Alice {
    sprite alice_battle
}
```

## Scene Structure

A scene is a scenario unit that takes place in one background/location.

Each scene must contain exactly one `setup` block at the beginning.

```kse
scene arrival {
    setup {
        ...
    }

    ...scene body...
}
```

The `setup` block uses header-specific syntax. Runtime commands such as `say`, `nar`, `show`, and `together` are not allowed inside `setup`, except where explicitly allowed in `init` as initial state declarations.

## Setup Block

The `setup` block describes scene metadata and initial state.

```kse
setup {
    cast {
        A
        G
    }

    bg: royal_gate_evening
    transition: fade 1.0

    init {
        A: left normal hidden
        G: right serious hidden
        camera: wide
        bgm: capital_evening
    }
}
```

### Cast

`cast` declares actors used in the scene.

```kse
cast {
    A
    G
}
```

This is used for:

- Sprite preloading
- Voice preloading
- Live2D / Spine preparation
- Scene analysis
- Build tooling
- Character appearance statistics

`cast` should list actor IDs already defined by `actor` declarations.

The compiler may also infer cast usage from the scene body, but explicit `cast` is useful as a resource loading hint.

### Background

Each scene must define exactly one background.

```kse
bg: royal_gate_evening
```

Changing background inside a scene body is forbidden. If the background changes, create a new scene.

### Scene Transition

`transition` defines how this scene is entered.

```kse
transition: fade 1.0
transition: dissolve 0.8
transition: wipe left 0.5
transition: none
```

The transition belongs to the destination scene, not the `goto` command.

```kse
goto enter_city
```

The `enter_city` scene decides how it appears.

### Init Block

`init` describes the static initial screen state.

```kse
init {
    A: left normal hidden
    G: right serious hidden
    camera: wide
    bgm: capital_evening
}
```

`init` is not for animated direction. It is for immediate state setup before the scene body begins.

Example actor initial state:

```kse
A: left normal hidden
B: center smile visible
```

Example non-actor initial state:

```kse
camera: wide
bgm: capital_evening
```

## Dialogue

### Say

`say` is used for actor dialogue.

```kse
say A #arrival_001 {
    ここが王都……。
}
```

Syntax:

```txt
say <actorId> <textId> { <body> }
```

### Narration

`nar` is used for narration.

```kse
nar #arrival_002 {
    王都の門には、夕陽が長い影を落としていた。
}
```

`text` is not used for narration. The DSL uses `nar` explicitly.

## Localization

Text-bearing commands should have stable text IDs.

```kse
say A #arrival_001 {
    ここが王都……。
}

nar #arrival_002 {
    王都の門には、夕陽が長い影を落としていた。
}
```

Localization tools should extract text by IDs such as:

```txt
prologue_001.arrival.arrival_001
prologue_001.arrival.arrival_002
```

### Placeholders

Use named placeholders for runtime values.

```kse
say A #arrival_003 {
    {playerName}、準備はいい？
}
```

Translation can change word order:

```txt
Are you ready, {playerName}?
```

### Plurals and Conditions

For pluralization and conditional grammar, KSE should follow ICU MessageFormat style where possible.

```kse
nar #item_count {
    {count, plural,
        one {ポーションを1個手に入れた。}
        other {ポーションを{count}個手に入れた。}
    }
}
```

## Parallel Execution

KSE does not use `cut` blocks.

Parallel execution is expressed explicitly with `together`.

```kse
together {
    show A
    pan A 0.5
    se cloth
}
```

All commands inside `together` start at the same time.

The script continues after all commands in the block complete.

This avoids the forgotten-`do` problem of delayed execution queues.

## Runtime Commands

### Character Commands

```kse
show A
show A left normal
hide A
face A angry
move A center 0.5
```

### Camera Commands

```kse
pan A 0.5
zoom 1.2 0.5
shake 0.5 0.3
fade in 1.0
fade out 1.0
```

### Audio Commands

```kse
bgm capital_evening
bgm stop
bgm stop 1.0
se armor
voice A alice_001
```

## Control Flow

### If

```kse
if has_pass {
    say A #arrival_010 {
        これでいい？
    }
} else {
    say G #arrival_011 {
        持っていないなら通すわけにはいかん。
    }
}
```

### Scene Jump

```kse
goto enter_city
```

### Event Call

```kse
call sub_event_001
return
```

### End

```kse
end
```

## Macros

Macros are reusable command blocks.

```kse
macro angry(actor) {
    face actor angry
    shake 0.3 0.2
}
```

Usage:

```kse
angry A
```

Macros execute immediately when called.

For parallel execution, call macros inside `together`.

```kse
together {
    enter A left normal
    look A
}
```

## Example

```kse
use common

actor A : Alice {
    nameKey char.alice
    sprite alice
}

actor G : Guard {
    nameKey char.guard
    sprite guard
}

macro enter(actor, pos, face) {
    show actor pos face
    se cloth
}

macro look(actor) {
    pan actor 0.5
    zoom 1.08 0.5
}

scene arrival {
    setup {
        cast {
            A
            G
        }

        bg: royal_gate_evening
        transition: fade 1.0

        init {
            A: left normal hidden
            G: right serious hidden
            camera: wide
            bgm: capital_evening
        }
    }

    together {
        enter A left normal
        look A
    }

    nar #arrival_001 {
        王都の門には、夕陽が長い影を落としていた。
    }

    say A #arrival_002 {
        ここが王都……。
    }

    together {
        enter G right serious
    }

    say G #arrival_003 {
        止まれ。身分証を見せろ。
    }

    if has_pass {
        say A #arrival_004 {
            これでいい？
        }

        together {
            face G normal
        }

        say G #arrival_005 {
            確認した。通ってよし。
        }

        goto enter_city
    } else {
        together {
            face G angry
        }

        say A #arrival_006 {
            身分証……？
        }

        say G #arrival_007 {
            持っていないなら通すわけにはいかん。
        }

        goto rejected
    }
}

scene enter_city {
    setup {
        cast {
            A
        }

        bg: capital_street_evening
        transition: dissolve 0.8

        init {
            A: center normal visible
            camera: wide
            bgm: capital_theme
        }
    }

    nar #enter_city_001 {
        アリスは王都へ足を踏み入れた。
    }

    end
}
```

## Grammar Sketch

```ebnf
file        ::= use_decl* actor_def* macro_def* scene_def*
use_decl    ::= "use" IDENT

actor_def   ::= "actor" IDENT ":" IDENT "{" actor_body* "}"

scene_def   ::= "scene" IDENT "{" setup_block scene_body* "}"
setup_block ::= "setup" "{" cast_block bg_stmt transition_stmt? init_block? "}"
cast_block  ::= "cast" "{" IDENT* "}"
bg_stmt     ::= "bg" ":" IDENT
transition_stmt ::= "transition" ":" IDENT value*
init_block  ::= "init" "{" init_stmt* "}"

scene_body  ::= say_stmt
              | nar_stmt
              | together_block
              | if_stmt
              | goto_stmt
              | call_stmt
              | return_stmt
              | end_stmt
              | command
              | macro_call

together_block ::= "together" "{" command* "}"
say_stmt       ::= "say" IDENT TEXT_ID "{" raw_text "}"
nar_stmt       ::= "nar" TEXT_ID "{" raw_text "}"
```

## Design Notes

KSE deliberately separates:

- `setup`: scene metadata
- `init`: static initial state
- `say` / `nar`: localizable text
- runtime commands: actual direction
- `together`: explicit parallel execution

This keeps the language readable for writers while remaining straightforward to parse and tool around.
