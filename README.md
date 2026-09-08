# Fizzy Moo: Soda Cow

A playable advertisement built as a Unity 3D game for ATLS/CSCI 4616 *Intro to Mixed Reality* (Mini Project Demo Day, Sept 8 2026).

You are Bessie. Eat limes, oranges and pineapples to build carbonation pressure, walk to the Fizzy Moo stand, and hold **Space** to pour a customer's order — release on the line. Score = fill accuracy × flavour purity, streaks multiply. Hit 100 PSI and she blows out.

**Everything is built from code.** The saved scene contains one empty GameObject (`Bootstrap`); the cow, the meadow, the stand, the UI sprites, the outline shader and all eight sound effects are constructed or synthesised at runtime. The only imported art is Fizzy Moo's own production assets (12 oz sleek-can models, label art, wordmark, typeface).

## Run

- Unity 6 (6000.6.0f1). Open the project, **Fizzy Moo ▸ 1 – Generate Scene**, press Play.
- **Fizzy Moo ▸ 2 – Build macOS Player**, or headless:

```bash
Unity -batchmode -quit -nographics -projectPath . \
      -executeMethod FizzyMoo.EditorTools.FizzyMooBuild.BuildMac
```

Controls: `WASD`/arrows move · `Space` vent/pour · `R` restart.
Player flags: `--demo` (autopilot attract mode) · `--record --seconds N --outdir DIR` (deterministic 60 fps JPEG capture + `events.tsv` audio event track).

## Layout

| Path | What |
|---|---|
| `Assets/Scripts/FizzyMoo/Bootstrap.cs` | Builds the whole world at runtime |
| `CowController.cs` / `CowRig.cs` | Carbonation model, blowout; the cow out of 60 primitives |
| `SodaStand.cs` / `Customer.cs` / `Can.cs` / `Bottle.cs` | Orders, judging, the real product |
| `Fruit.cs` | Limes / oranges / pineapples |
| `HUD.cs` / `UIKit.cs` | Code-rasterised sprites, gauge, brand typeface |
| `ProcAudio.cs` | All SFX synthesised into AudioClips at load |
| `CameraRig.cs` · `AutoPilot.cs` · `FrameRecorder.cs` | Feel, demo player, capture |
| `Assets/Editor/FizzyMooBuild.cs` | Scene generation, Always-Included Shaders, player build |
| `Assets/Shaders/FizzyOutline.shader` | Inverted-hull keyline (second material slot) |

Brand assets in `Assets/Resources/{Cans,Brand}` are © Fizzy Moo and are not licensed for reuse.
