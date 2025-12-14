### Key Examples of Playable Projects Using Stride Game Engine in Top-Down or 3rd Person Views

- Research suggests several open-source and commercial projects leverage Stride for these perspectives, with at least three fully playable examples available on GitHub or Steam, focusing on Vulkan-compatible rendering.
- Distant Worlds 2 appears to be a prominent top-down 4X strategy game, similar to Rise of Nations, offering vast real-time gameplay.
- Starbreach provides a first/third-person shooter experience, adaptable to over-the-head 3rd person views.
- Rise of the Undeaf is a 3D third-person shooter with thematic elements, serving as a complete playable demo.
- For black screens on load, evidence leans toward issues like missing cameras, lights, or improper scene setup, often resolvable by verifying entity configurations or graphics profiles.

**Recommended Playable Projects**
Start with these as they implement full games or demos in the desired styles, using MonoGame for cross-platform compatibility.

- **Distant Worlds 2**: A pausable real-time 4X space strategy game with top-down views, emphasizing exploration and management. It's commercially released and demonstrates large-scale world rendering. Available on Steam: <https://store.steampowered.com/app/1531540/Distant_Worlds_2>.
- **Starbreach**: A first/third-person shooter that can be viewed in 3rd person over-the-head style, including multiplayer elements. It's an open-source demo showcasing combat and level design. GitHub repo: <https://github.com/xenko3d/Starbreach>.
- **Rise of the Undeaf**: A 3D third-person shooter with musical zombie themes, fully playable and open-source. It highlights character control and environmental interactions in a top-down-ish 3rd person setup. GitHub repo: <https://github.com/manio143/RiseOfTheUndeaf>.
- Additional option: **Doprez's Basic Platformer WIP** – A 3rd person platformer demo, useful for beginners to adapt into top-down variants. GitHub: <https://github.com/Doprez/stride-platformer>.

**Common Boilerplate and Examples for 3D Rendering in Stride**
Stride offers built-in templates and community tutorials for quick starts, primarily using Vulkan for rendering. These are popular among newbies for setting up scenes without black screens.

- **Official Templates**: Include Third-Person Platformer (over-the-head 3rd person) and Top-Down Camera RPG (top-down like NWN). Create via Game Studio's new project wizard.
- **C# Code-Only Basics**: Simple scripts for scenes, entities, and cameras – essential to avoid black screens by adding a camera and light entity.
- **Vaclav Elias Tutorials**: Beginner-friendly C# examples for 3D worlds, including scene management and navigation.

**Troubleshooting Black Screen Issues**
It seems likely this occurs due to missing prerequisites, empty scenes, or graphics mismatches. Ensure Vulkan drivers are installed; add a default camera and directional light to your starting scene. Check logs for errors.

---
Stride Game Engine, an open-source C# framework focused on realistic rendering with Vulkan as its primary graphics API (falling back to OpenGL on unsupported platforms), provides robust support for top-down and over-the-head 3rd person games akin to classics like Neverwinter Nights (NWN) or Rise of Nations. This setup emphasizes modular scene management, entity-component systems, and cross-platform compatibility without relying on proprietary APIs like DirectX unless explicitly configured. Note that "Remix" may refer to NVIDIA's RTX Remix for modding, but Stride natively handles Vulkan for modern rendering needs, ensuring exclusivity to OpenGL/Vulkan paths as requested. Below, we delve into top playable projects, boilerplate examples, and troubleshooting for black screen issues, drawing from official docs, community repos, and tutorials.

#### Playable Projects Using Stride in Top-Down or 3rd Person Setups

Community and commercial efforts highlight Stride's versatility for these views. We've identified at least five projects (exceeding the minimum of three) that offer fully playable games or demos, all open-source or accessible, with Vulkan/OpenGL implementations. These serve as excellent starting points for adaptation, demonstrating world loading, camera controls, and entity interactions without black screens when properly set up.

| Project Name | View Style | Genre/Description | Playable Status | Key Features | Source/Link |
|--------------|------------|-------------------|-----------------|--------------|-------------|
| Distant Worlds 2 | Top-down (strategic overview) | Real-time 4X space strategy, pausable with vast worlds similar to Rise of Nations | Fully playable commercial release | Procedural galaxies, AI management, large-scale rendering | Steam: <https://store.steampowered.com/app/1531540/Distant_Worlds_2>  |
| Starbreach | First/Third-person (switchable, over-the-head 3rd person) | Shooter with combat and exploration, GDC demo style | Playable open-source demo | Multiplayer elements, physics-based levels, character animations | GitHub: <https://github.com/xenko3d/Starbreach>  |
| Rise of the Undeaf | 3rd person (over-the-head) | Third-person shooter with zombie themes and musical integration | Fully playable open-source game | Enemy AI, audio synchronization, 3D environments | GitHub: <https://github.com/manio143/RiseOfTheUndeaf>  |
| Rollerghoster | 3rd person racing (procedural tracks) | Racing against ghosts, online/local multiplayer | Playable open-source | Procedural generation, physics simulations, track rendering | GitHub: <https://github.com/Aggror/RollerGhosterOpen>  |
| Basic Platformer WIP | 3rd person platformer (adaptable to top-down) | Platforming with jumping and levels | Playable demo/WIP | Character controller, collision detection, basic world setup | GitHub: <https://github.com/Doprez/stride-platformer>  |

These projects underscore Stride's strength in handling 3D worlds with Vulkan for efficient rendering. For instance, Distant Worlds 2 manages complex top-down UIs and simulations, while Starbreach and Rise of the Undeaf provide boilerplate for 3rd person cameras. All are Vulkan-first, with OpenGL options via graphics profile settings in Game Studio.

#### Boilerplate and Examples for 3D Rendering Scenes, Levels, and Worlds

Newbie users frequently turn to Stride's official templates and community tutorials for quick prototypes, as they include pre-configured scenes to avoid common pitfalls like black screens. These focus on Vulkan/OpenGL rendering pipelines, with code in C# for entity setup, lighting, and cameras. Here's a comprehensive list of highly used resources:

1. **Official Game Templates** (Built-in to Stride Game Studio):
   - **Third-Person Platformer**: Boilerplate for over-the-head 3rd person views, including player controller scripts, jumping mechanics, and basic levels. Ideal for NWN-like adventures. Access: Create new project in Game Studio.
   - **Top-Down Camera RPG**: Starter for top-down perspectives like Rise of Nations, with grid-based navigation, entity spawning, and UI overlays.
   - **First-Person Shooter**: Adaptable to 3rd person by modifying camera scripts; includes weapon systems and level loading.

2. **C# Beginner Tutorials Series** (YouTube and Docs):
   - Introduction to scenes: Covers creating a basic 3D world with entities, cameras, and lights to render properly (prevents black screens).
   - Scene Management: Child scenes, hiding/locking, and hierarchical worlds for complex levels (2.5-hour intermediate guide).
   - Animations, Audio, Camera, Navigation: Boilerplate for dynamic 3D environments, including top-down camera scripts.

3. **Community Toolkit and Code-Only Examples**:
   - Basic Examples: Code snippets for scenes, entities, and rendering without Game Studio – e.g., adding a skybox, directional light, and camera entity in C# to initialize a 3D world.
   - Vaclav Elias Tutorials: .NET 5/6 examples for 3D setups, including world building, entity components, and beginner-friendly projects like simple levels.

4. **GitHub Open-Source Boilerplates**:
   - Stride Repo Samples: Includes ThirdPersonPlatformer folder with full scripts for controllers, levels, and Vulkan rendering.
   - Awesome Stride Resources: Curated list of projects and examples, including 3D demos for newbies.
   - Additional Repos: Stride3DTutorials for step-by-step 3D world creation; often used for prototyping top-down RPGs.

These resources are popular because they provide copy-pasteable code for essentials like `Entity.Add(new CameraComponent())` and `SceneSystem.SceneInstance.RootScene = myScene;`, ensuring Vulkan/OpenGL compatibility.

#### Addressing Black Screen on Load

Black screens commonly arise in Stride due to configuration or setup oversights, especially in Vulkan/OpenGL modes. Key causes and fixes include:

- **Empty or Misconfigured Scenes**: If no camera/light, rendering fails. Fix: In code, add `var camera = new Entity { new CameraComponent() }; scene.Entities.Add(camera);` or use Game Studio to populate the default scene.
- **Graphics Profile Issues**: Low profiles (e.g., Direct3D10) may cause blacks/crashes; set to HiDef for Vulkan.
- **Prerequisites Missing**: Ensure .NET, Vulkan drivers, and Visual C++ redistributables are installed. Reinstall Stride if needed.
- **Android/Emulator Specifics**: Texture setup errors can black out; update drivers or suppress errors.
- General Tip: Enable debug logging via `Game.ProfilingEnabled = true;` to diagnose.

NOTE: RTX Remix support is disabled in the current OpenGL-only mode. Remix requires DirectX 9 and will be supported when DirectX backends are implemented.

This compilation draws from diverse sources to balance views, prioritizing official docs and GitHub for reliability.

#### Key Citations

- [Games and demos | Stride Community resources](https://doc.stride3d.net/latest/en/community-resources/games-and-demos.html)
- [Newly Added Game Templates - Stride Game Engine](https://www.stride3d.net/blog/game-templates/)
- [Example projects | Stride Community resources](https://doc.stride3d.net/latest/en/community-resources/example-projects.html)
- [No screen modes found error. · Issue #831 · stride3d/stride](https://github.com/stride3d/stride/issues/831)
- [Trouble running Code-Only sample - Stride Forums](https://forums.stride3d.net/t/trouble-running-code-only-sample/1924)
- [Stride doesn't run | Stride manual](https://doc.stride3d.net/latest/en/manual/troubleshooting/stride-doesnt-run.html)
- [Stride doesn't run](https://doc.stride3d.net/4.0/en/manual/troubleshooting/stride-doesnt-run.html)
- [Android emulator startup texture setup crash #2259](https://github.com/stride3d/stride/issues/2259)
- [Troubleshooting | Stride manual](https://doc.stride3d.net/latest/manual/troubleshooting/index.html)
- [Stride Game Engine Tutorials](https://doc.stride3d.net/latest/en/tutorials/stride-tutorials.pdf)
- [Stride tutorial | C# beginner #1 | Introduction - YouTube](https://www.youtube.com/watch?v=Z2kUQhSmdr0)
- [Create and open a scene - Stride documentation](https://doc.stride3d.net/4.0/jp/Manual/game-studio/create-a-scene.html)
- [Scenes | Stride tutorials](https://doc.stride3d.net/latest/en/tutorials/csharpintermediate/scenes.html)
- [Blog posts and tutorials for newcomers #1211 - GitHub](https://github.com/stride3d/stride/discussions/1211)
- [Stride Game Engine Tutorials](https://doc.stride3d.net/latest/en/tutorials/)
- [C# Code Only Basic Examples | Stride Community Toolkit Manual](https://stride3d.github.io/stride-community-toolkit/manual/code-only/examples/basic-examples.html)
- [Create a project - Stride documentation](https://doc.stride3d.net/4.0/en/manual/get-started/create-a-project.html)
- [Stride Community Toolkit Preview - Code-Only Feature Basics in C#](https://www.vaclavelias.com/stride3d/stride-community-toolkit-code-only-basics-csharp/)
- [Awesome resources for the fully open source Stride game engine.](https://github.com/Doprez/Awesome-Stride)
- [stride3d/stride: Stride (formerly Xenko), a free and open ... - GitHub](https://github.com/stride3d/stride)
- [PlayerController.cs - GitHub](https://github.com/stride3d/stride/blob/master/samples/Templates/ThirdPersonPlatformer/ThirdPersonPlatformer/ThirdPersonPlatformer.Game/Player/PlayerController.cs)
- [MonoGame tutorials and examples - GitHub](https://github.com/MonoGame/MonoGame.Samples)
