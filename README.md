# Followers

A Dalamud plugin that adds cute animated followers that follow your cursor around the entire screen, not limited to Dalamud windows.

## Features

* **Three Unique Followers:**
  * **Fish2D** - A smooth 2D fish with chain-based body animation, fins, and eyes
  * **Dragon** - A procedurally generated SVG-style dragon with segments, fins, and color gradients. Dashes to the cursor when it moves, then returns to idle behavior after a delay.
  * **Skeletile** - A procedurally generated skeleton creature with advanced hunting and stalking behaviors, inverse kinematics for leg movement, and personality-based AI

* **Procedural Generation** - Dragon and Skeletile followers can be regenerated with new random parameters (colors, sizes, segment counts, etc.)

* **Screen-Wide Rendering** - Followers render on the entire screen, not limited to Dalamud windows

* **Smooth Animation** - Interpolation and physics-based movement for natural following behavior

* **Persistent Settings** - Configuration saves automatically

## Commands

* `/pfollowers` - Opens the configuration window
* `/pfollowers regen` - Regenerates the current follower (if supported)

## Installation

### For Users

#### Option 1: Custom Repository (Recommended)

1. Launch the game and use `/xlplugins` to open the Plugin Installer
2. Go to **Settings** → **Custom Plugin Repositories**
3. Click **Add** and enter the following URL:
   ```
   https://raw.githubusercontent.com/pupwife/FollowersPlugin/master/repo.json
   ```
4. Click **Save** and return to the **Available Plugins** tab
5. Find "Followers" in the list and click **Install**
6. Use `/pfollowers` to open the configuration window and select your follower!

#### Option 2: Manual Installation

1. Download the latest release from the [Releases](https://github.com/pupwife/FollowersPlugin/releases) page
2. Extract `Followers.zip` to your Dalamud dev plugins directory (usually `%AppData%\XIVLauncher\addon\Hooks\dev\Plugins\`)
3. Launch the game and use `/xlplugins` to open the Plugin Installer
4. Find "Followers" in the list and enable it
5. Use `/pfollowers` to open the configuration window and select your follower!

### For Developers

#### Prerequisites

* XIVLauncher, FINAL FANTASY XIV, and Dalamud have all been installed and the game has been run with Dalamud at least once
* XIVLauncher is installed to its default directories and configurations
  * If a custom path is required for Dalamud's dev directory, it must be set with the `DALAMUD_HOME` environment variable
* A .NET Core 8 SDK has been installed and configured

#### Building

1. Open up `Followers.sln` in your C# editor of choice (likely [Visual Studio 2022](https://visualstudio.microsoft.com) or [JetBrains Rider](https://www.jetbrains.com/rider/))
2. Build the solution. By default, this will build a `Debug` build, but you can switch to `Release` in your IDE
3. The resulting plugin can be found at `bin/x64/Debug/Followers.dll` (or `Release` if appropriate)

#### Activating in-game

1. Launch the game and use `/xlsettings` in chat or `xlsettings` in the Dalamud Console to open up the Dalamud settings
   * In here, go to `Experimental`, and add the full path to the `Followers.dll` to the list of Dev Plugin Locations
2. Next, use `/xlplugins` (chat) or `xlplugins` (console) to open up the Plugin Installer
   * In here, go to `Dev Tools > Installed Dev Plugins`, and the `Followers` plugin should be visible. Enable it
3. Use `/pfollowers` to open the configuration window and start using your followers!

Note that you only need to add it to the Dev Plugin Locations once (Step 1); it is preserved afterwards. You can disable, enable, or load your plugin on startup through the Plugin Installer.

## Configuration

The configuration window allows you to:
* Enable/Disable followers
* Switch between different follower types
* Regenerate procedurally generated followers (Dragon and Skeletile)

Your selected follower preference is saved and will be restored when you re-enable followers.

## Skeletile Behavior

Skeletile is a procedurally generated skeleton creature with advanced AI behaviors:

### Personality System
* **Brute (≤4 legs)** - Direct, aggressive approach. Charges at the cursor when idle.
* **Sneaky (≥8 legs)** - Patient, indirect approach. Uses cunning tactics and tries to hide.
* **Balanced (5-7 legs)** - Mix of both behaviors.

### Hunting & Stalking
* **Stalking Behavior** - Skeletile follows behind the cursor at a distance, creating a stalking effect
* **Idle Detection** - Tracks cursor movement history to detect when the cursor is idle
* **Aggression System** - Builds aggression over time if it can't catch the cursor, making it more restless

### Pounce States
When the cursor is idle for 2.5+ seconds, Skeletile enters a pounce sequence:
1. **Approaching** - Moves towards the cursor
2. **Attacking** (Brute) - Direct charge to get on top of the cursor
3. **Wrapping** (Sneaky/Anaconda) - Circles around the cursor in an ouroboros pattern
4. **Coiling** - When close enough, coils around the cursor in a tight circle

### Inverse Kinematics
* Legs use IK to walk naturally, with stepping behavior
* Leg count scales with tail length (even number, half on each side)
* More legs = faster movement and more complex behavior

### Procedural Generation
Each Skeletile is unique with:
* Random leg count (2-12 legs, even numbers)
* Random tail length (scales with leg count)
* Random size variation
* Speed multipliers based on leg count

## Technical Details

* Built for Dalamud API 13
* Uses ImGui for rendering followers on screen
* Followers track cursor position using ImGui mouse position
* Smooth interpolation for natural movement

## License

AGPL-3.0-or-later

## Credits

* Author: pupwife
* Based on JavaScript followers converted to C# for Dalamud
